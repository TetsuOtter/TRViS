using System.Collections.ObjectModel;

using TRViS.DTAC;
using TRViS.IO;
using TRViS.IO.Models;
using TRViS.NetworkSyncService;
using TRViS.Services;
using TRViS.Utils;
using TRViS.ViewModels;

namespace TRViS.RootPages;

// Lightweight presenter records used by the WorkGroup / Work CollectionView
// templates. Subtitle aggregates whatever rich detail is available from the
// loader (Work count for groups; Train count + AffectDate for works) so the
// picker rows aren't just bare names.
public sealed record WorkGroupListItem(WorkGroup Source, string Name, string Subtitle);
public sealed record WorkListItem(Work Source, string Name, string Subtitle);

// Home-mode body extracted from StartHomePage. Owns the loader info card, the
// two-step WorkGroup / Work picker, and the Disconnect / Open buttons. Tentative
// selection state stays here — only the parent's CommitPendingSelection seam
// promotes it into AppViewModel. The parent drives lifecycle (OnPageAppearing /
// OnPageDisappearing) and forwards relevant viewModel PropertyChanged events.
public partial class HomeGridView : Grid
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	AppViewModel viewModel => InstanceManager.AppViewModel;

	// ----- Tentative (pre-Open) selection state -----
	// The Home page deliberately does NOT mirror its picker state into
	// AppViewModel.SelectedWorkGroup / SelectedWork. Those are the *committed*
	// selections used by DTAC; they only change when the user presses 開く.
	// This lets the user explore the picker without polluting DTAC, and gives
	// the 開く button real semantic weight ("commit my choice").
	WorkGroup? _pendingWorkGroup;
	Work? _pendingWork;
	readonly ObservableCollection<WorkGroupListItem> _workGroupItems = new();
	readonly ObservableCollection<WorkListItem> _workItems = new();
	// Per-loader caches for the subtitle counts. RebuildWorkGroupItems is called on
	// every WorkGroupList PropertyChanged (e.g. each websocket Refresh), and each
	// item's subtitle calls loader.GetWorkList(wg.Id).Count — a DB roundtrip on
	// LoaderSQL. With N WorkGroups and M Works per WG, an unguarded rebuild costs
	// O(N+N*M) DB reads per push. Cache by Id, scoped to the *current* loader so
	// a swap clears stale entries.
	ILoader? _countCacheLoader;
	readonly Dictionary<string, int> _workCountByGroupId = new();
	readonly Dictionary<string, int> _trainCountByWorkId = new();
	// Re-entrancy guard: we set CollectionView.SelectedItem programmatically when
	// restoring tentative state from the AppViewModel, and that fires SelectionChanged.
	// The flag prevents that synthetic change from being treated as a user pick.
	bool _suppressSelectionChanged;
	// Guard: 開く's commit sets SelectedWorkGroup -> the cascade auto-picks the first
	// Work of that group BEFORE we then set SelectedWork to the user's pending pick.
	// Without this guard, the intermediate SelectedWork PropertyChanged would yank
	// _pendingWork to the auto-picked one via SyncPendingFromCommitted, causing a
	// brief visible flicker.
	bool _committingOpen;

	public HomeGridView()
	{
		InitializeComponent();

		WorkGroupListView.ItemsSource = _workGroupItems;
		WorkListView.ItemsSource = _workItems;
	}

	// ----- Parent-driven lifecycle / hooks -----

	/// <summary>
	/// Called from the page's OnAppearing. Reflects current loader / committed
	/// selection state. If returning here from DTAC, the user's last commit
	/// becomes their initial pending state.
	/// </summary>
	public void OnPageAppearing()
	{
		RebuildWorkGroupItems();
		SyncPendingFromCommitted();
	}

	/// <summary>
	/// Forwarded from the page's viewModel.PropertyChanged. Mode-switch handling
	/// (Start↔Home) stays on the page; this method only refreshes the picker /
	/// loader-info state owned by Home mode.
	/// </summary>
	public void HandleViewModelPropertyChanged(string? propertyName)
	{
		switch (propertyName)
		{
			case nameof(AppViewModel.Loader):
				// New loader -> wipe tentative state and rebuild the WorkGroup picker.
				ResetPendingSelection();
				RebuildWorkGroupItems();
				RefreshStepUi();
				break;

			case nameof(AppViewModel.LoaderSourceLabel):
				UpdateLoaderInfoLabels();
				break;

			case nameof(AppViewModel.WorkGroupList):
				// WorkGroupList changes can come from a Refresh() (websocket) which
				// may also reset committed AppViewModel selections. Rebuild the items;
				// only sync tentative from committed when committed actually has a
				// value — otherwise a websocket Refresh would silently clobber the
				// user's mid-pick on Home (committed is null -> _pendingWorkGroup
				// would be forced to null).
				// A Refresh may also have added/removed Works/Trains under the
				// same loader instance, so drop count caches to force fresh reads.
				InvalidateCountCaches();
				RebuildWorkGroupItems();
				if (viewModel.SelectedWorkGroup is not null || viewModel.SelectedWork is not null)
					SyncPendingFromCommitted();
				else
					SyncListViewSelections();
				RefreshStepUi();
				break;

			case nameof(AppViewModel.SelectedWorkGroup):
			case nameof(AppViewModel.SelectedWork):
				// Committed selection moved underneath us (e.g. websocket Refresh chose
				// a different fallback). Re-sync the tentative state so the user sees
				// what's actually committed when they return to this page.
				// Skip during 開く's own commit, where the cascade between SetSelectedWorkGroup
				// and SetSelectedWork would otherwise overwrite the user's pending pick.
				if (!_committingOpen)
				{
					SyncPendingFromCommitted();
					RefreshStepUi();
				}
				break;
		}
	}

	// ----- Loader info -----

	/// <summary>
	/// Reflects the active loader's type and source label into the LoaderInfoCard.
	/// Called by the page on mode switch and on LoaderSourceLabel changes.
	/// </summary>
	public void UpdateLoaderInfoLabels()
	{
		ILoader? loader = viewModel.Loader;
		if (loader is null)
		{
			LoaderInfoTitleLabel.Text = "読み込み済みデータ";
			LoaderInfoDetailLabel.Text = "";
			LoaderInfoGlyphLabel.Text = MaterialIcons.Description;
			return;
		}

		// Title = loader type, glyph = matching Material Icon, detail = source label
		// (file name, URL) set atomically with the loader via AppViewModel.SetLoader.
		(string title, string glyph) = loader switch
		{
			SampleDataLoader => ("デモデータ", MaterialIcons.Science),
			LoaderJson => ("JSON ファイル", MaterialIcons.Description),
			LoaderSQL => ("SQLite ファイル", MaterialIcons.Storage),
			WebSocketNetworkSyncService => ("サーバー接続中", MaterialIcons.Wifi),
			_ => (loader.GetType().Name, MaterialIcons.Description),
		};
		LoaderInfoTitleLabel.Text = title;
		LoaderInfoGlyphLabel.Text = glyph;
		LoaderInfoDetailLabel.Text = viewModel.LoaderSourceLabel ?? string.Empty;
	}

	// ----- Two-step picker (WorkGroup -> Work) -----
	//
	// The CollectionViews bind to ObservableCollection<WorkGroupListItem> /
	// <WorkListItem> presenters built from the active loader's lists. Selection
	// is captured in _pendingWorkGroup / _pendingWork — those drive the chip vs
	// list visual state and the 開く commit.

	void ResetPendingSelection()
	{
		_pendingWorkGroup = null;
		_pendingWork = null;
		_workItems.Clear();
	}

	void SyncPendingFromCommitted()
	{
		// Mirror the AppViewModel's *committed* selection into our tentative state.
		// Called when we appear or when the committed state changes underneath us.
		// Does not propagate back — this is intentionally one-way.
		// Compare by Id rather than Equals: WorkGroup/Work are records whose
		// auto-generated equality compares positional members (incl. byte[]
		// AffixContent / ETrainTimetableContent on Work) by reference. After a
		// websocket Refresh, the manager's new instance won't reference-match
		// the cached one even though it represents the same logical row, so
		// Equals returns false and we'd needlessly rebuild lists.
		var committedWG = viewModel.SelectedWorkGroup;
		var committedWork = viewModel.SelectedWork;

		if (!IsSameWorkGroup(_pendingWorkGroup, committedWG))
		{
			_pendingWorkGroup = committedWG;
			RebuildWorkItems();
		}
		if (!IsSameWork(_pendingWork, committedWork))
		{
			_pendingWork = committedWork;
		}

		SyncListViewSelections();
		RefreshStepUi();
	}

	static bool IsSameWorkGroup(WorkGroup? a, WorkGroup? b)
	{
		if (ReferenceEquals(a, b))
			return true;
		if (a is null || b is null)
			return false;
		return string.Equals(a.Id, b.Id, StringComparison.Ordinal);
	}

	static bool IsSameWork(Work? a, Work? b)
	{
		if (ReferenceEquals(a, b))
			return true;
		if (a is null || b is null)
			return false;
		return string.Equals(a.Id, b.Id, StringComparison.Ordinal);
	}

	void SyncListViewSelections()
	{
		// Reflect _pendingWorkGroup / _pendingWork onto the CollectionViews without
		// re-triggering OnXxxSelectionChanged (which would reset the user-pick flow).
		// Match by Id (see SyncPendingFromCommitted for why record Equals is unsafe).
		_suppressSelectionChanged = true;
		try
		{
			WorkGroupListView.SelectedItem = _pendingWorkGroup is null
				? null
				: _workGroupItems.FirstOrDefault(i => IsSameWorkGroup(i.Source, _pendingWorkGroup));
			WorkListView.SelectedItem = _pendingWork is null
				? null
				: _workItems.FirstOrDefault(i => IsSameWork(i.Source, _pendingWork));
		}
		finally
		{
			_suppressSelectionChanged = false;
		}
	}

	void EnsureCountCacheLoader(ILoader? loader)
	{
		if (ReferenceEquals(_countCacheLoader, loader))
			return;
		_countCacheLoader = loader;
		InvalidateCountCaches();
	}

	void InvalidateCountCaches()
	{
		// Called whenever the underlying lists may have changed: loader swap or
		// WorkGroupList PropertyChanged (which fires after a websocket Refresh
		// that may have added/removed Works/Trains).
		_workCountByGroupId.Clear();
		_trainCountByWorkId.Clear();
	}

	int GetWorkCountCached(ILoader loader, string workGroupId)
	{
		if (_workCountByGroupId.TryGetValue(workGroupId, out int cached))
			return cached;
		int count;
		try { count = loader.GetWorkList(workGroupId).Count; }
		catch { count = 0; }
		_workCountByGroupId[workGroupId] = count;
		return count;
	}

	int GetTrainCountCached(ILoader loader, string workId)
	{
		if (_trainCountByWorkId.TryGetValue(workId, out int cached))
			return cached;
		int count;
		try { count = loader.GetTrainDataList(workId).Count; }
		catch { count = 0; }
		_trainCountByWorkId[workId] = count;
		return count;
	}

	void RebuildWorkGroupItems()
	{
		// See RebuildWorkItems for the iOS 12 detach/reattach rationale.
		bool detachForIos12CollectionViewCrash =
#if IOS
			!OperatingSystem.IsIOSVersionAtLeast(13);
#else
			false;
#endif
		if (detachForIos12CollectionViewCrash)
			WorkGroupListView.ItemsSource = null;
		try
		{
			_workGroupItems.Clear();
			var loader = viewModel.Loader;
			var groups = viewModel.WorkGroupList;
			EnsureCountCacheLoader(loader);
			if (loader is not null && groups is not null)
			{
				foreach (var wg in groups)
				{
					int workCount = GetWorkCountCached(loader, wg.Id);
					string subtitle = $"Work 数: {workCount}";
					_workGroupItems.Add(new WorkGroupListItem(wg, wg.Name, subtitle));
				}
			}
		}
		finally
		{
			if (detachForIos12CollectionViewCrash)
				WorkGroupListView.ItemsSource = _workGroupItems;
		}
		// Cascade to Work list — runs regardless of loader/groups state so the
		// Work list mirrors the (possibly cleared) WorkGroup list.
		RebuildWorkItems();
	}

	void RebuildWorkItems()
	{
		// iOS 12: detaching ItemsSource around the mutation forces MAUI to issue
		// a single ReloadData instead of the per-Add InsertItems calls that
		// ObservableItemsSource normally emits. The latter path crashes the app
		// the first time a WorkGroup is picked because WorkListView is
		// transitioning from IsVisible=false to true, and an InsertItems call
		// mid-layout-pass triggers UICollectionViewFlowLayout to be invalidated
		// with a (null) context — iOS 13+ tolerates that, iOS 12 rejects it as
		// NSInvalidArgumentException. See log 2026-05-11 15:35:39.
		bool detachForIos12CollectionViewCrash =
#if IOS
			!OperatingSystem.IsIOSVersionAtLeast(13);
#else
			false;
#endif
		if (detachForIos12CollectionViewCrash)
			WorkListView.ItemsSource = null;
		try
		{
			_workItems.Clear();
			var loader = viewModel.Loader;
			var wg = _pendingWorkGroup;
			if (loader is null || wg is null)
				return;
			EnsureCountCacheLoader(loader);

			IReadOnlyList<Work> works;
			try { works = loader.GetWorkList(wg.Id); }
			catch { works = Array.Empty<Work>(); }

			foreach (var w in works)
			{
				int trainCount = GetTrainCountCached(loader, w.Id);

				List<string> parts = new(2);
				if (w.AffectDate is { } d)
					parts.Add($"施行日: {d:yyyy/MM/dd}");
				parts.Add($"列車数: {trainCount}");
				_workItems.Add(new WorkListItem(w, w.Name, string.Join(" · ", parts)));
			}
		}
		finally
		{
			if (detachForIos12CollectionViewCrash)
				WorkListView.ItemsSource = _workItems;
		}
	}

	void RefreshStepUi()
	{
		var pendingWG = _pendingWorkGroup;
		var pendingW = _pendingWork;

		bool hasWorkGroup = pendingWG is not null;
		WorkGroupChip.IsVisible = hasWorkGroup;
		WorkGroupListBorder.IsVisible = !hasWorkGroup;
		WorkGroupChipNameLabel.Text = pendingWG?.Name ?? string.Empty;

		bool hasWork = pendingW is not null;
		WorkChip.IsVisible = hasWorkGroup && hasWork;
		WorkListBorder.IsVisible = hasWorkGroup && !hasWork;
		WorkChipNameLabel.Text = pendingW?.Name ?? string.Empty;

		UpdateHomeBodyRowDefinitions();
	}

	/// <summary>
	/// HomeBody is a 4-row Grid (label, chip-or-list, label, chip-or-list). For each
	/// chip-or-list row we pick Star when the CollectionView list is shown (so it
	/// soaks up remaining vertical space) and Auto when only the chip is shown (so
	/// the row collapses to chip height). The chip Border has VerticalOptions=Start
	/// so it never stretches when a row happens to be Star. This replaces the old
	/// pixel-arithmetic in UpdateListHeights, which was both fragile under font
	/// scaling (constant SectionLabelOverhead/ChipOverhead didn't track Subtitle
	/// font metrics) and silently understated the StepListBorder chrome — leading
	/// to the bottom of the Work list being clipped against HomeButtonsRow.
	/// </summary>
	void UpdateHomeBodyRowDefinitions()
	{
		if (HomeBody.RowDefinitions.Count < 4)
			return;
		HomeBody.RowDefinitions[1].Height = WorkGroupListBorder.IsVisible ? GridLength.Star : GridLength.Auto;
		HomeBody.RowDefinitions[3].Height = WorkListBorder.IsVisible ? GridLength.Star : GridLength.Auto;
	}

	void OnWorkGroupSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (_suppressSelectionChanged)
			return;
		try
		{
			var item = WorkGroupListView.SelectedItem as WorkGroupListItem;
			_pendingWorkGroup = item?.Source;
			// Switching Work Group invalidates any prior Work pick and rebuilds the
			// Work list for the new group.
			_pendingWork = null;
			RebuildWorkItems();
			// Symmetric with the single-WorkGroup auto-select in
			// TimetableSelectionManager: a Work list with exactly one entry is a
			// no-choice step, so auto-pick it here too — the user goes straight
			// to 開く instead of tapping a one-item list. (The committed-state and
			// single-WG load paths already get the only Work via
			// TimetableSelectionManager.OnWorkGroupChanged; this covers the
			// remaining gap: manually picking a WG in the multi-WG picker.)
			if (_workItems.Count == 1)
				_pendingWork = _workItems[0].Source;
			SyncListViewSelections();
			RefreshStepUi();
		}
		catch (Exception ex)
		{
			// UI-thread event handler — an exception that escapes here propagates
			// through Mono's unhandled exception hook and aborts the process. Log
			// + Crashlytics + sync-flush before rethrowing so the crash record is
			// recoverable. (See MauiProgram.CurrentDomain_UnhandledException for
			// the matching app-level flush.)
			logger.Error(ex, "OnWorkGroupSelectionChanged failed");
			InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.OnWorkGroupSelectionChanged");
			try { NLog.LogManager.Flush(TimeSpan.FromSeconds(2)); } catch { /* best-effort */ }
			throw;
		}
	}

	void OnWorkSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (_suppressSelectionChanged)
			return;
		try
		{
			var item = WorkListView.SelectedItem as WorkListItem;
			_pendingWork = item?.Source;
			RefreshStepUi();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "OnWorkSelectionChanged failed");
			InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.OnWorkSelectionChanged");
			try { NLog.LogManager.Flush(TimeSpan.FromSeconds(2)); } catch { /* best-effort */ }
			throw;
		}
	}

	void OnWorkGroupChipTapped(object? sender, TappedEventArgs e)
	{
		logger.Info("Work Group chip tapped -> clearing tentative selection");
		_pendingWorkGroup = null;
		_pendingWork = null;
		_workItems.Clear();
		SyncListViewSelections();
		RefreshStepUi();
	}

	void OnWorkChipTapped(object? sender, TappedEventArgs e)
	{
		logger.Info("Work chip tapped -> clearing tentative selection");
		_pendingWork = null;
		SyncListViewSelections();
		RefreshStepUi();
	}

	// ----- Open / Disconnect -----

	async void OnOpenClicked(object sender, EventArgs e)
	{
		logger.Info("Open clicked");
		var pendingWG = _pendingWorkGroup;
		var pendingW = _pendingWork;

		if (pendingWG is null || pendingW is null)
		{
			logger.Info("Open ignored: pending selection incomplete (WG={0}, W={1})", pendingWG, pendingW);
			await Util.DisplayAlertAsync("選択されていません", "Work Group と Work を選択してから「開く」を押してください。", "OK");
			return;
		}

		CommitPendingSelection(pendingWG, pendingW);
		await NavigateToDTACAsync();
	}

	// Commit tentative -> AppViewModel. Setting SelectedWorkGroup cascades
	// (TimetableSelectionManager.OnWorkGroupChanged auto-picks the first Work
	// of that group); we then immediately overwrite with the user's pending
	// Work, which cascades again to pick its first TrainData. Net effect:
	// committed (WG, W, first-Train) — the same shape DTAC has always seen.
	// Wrapped in _committingOpen so the cascade-fired SelectedWork PropertyChanged
	// doesn't yank our pending state via SyncPendingFromCommitted.
	//
	// Public so test seams in StartHomePage can short-circuit the picker.
	public void CommitPendingSelection(WorkGroup workGroup, Work work)
	{
		_committingOpen = true;
		try
		{
			viewModel.SelectedWorkGroup = workGroup;
			viewModel.SelectedWork = work;
		}
		finally
		{
			_committingOpen = false;
		}
	}

	async void OnDisconnectClicked(object sender, EventArgs e)
	{
		logger.Info("Disconnect/Close clicked");
		if (viewModel.Loader is null)
			return;

		bool confirm = await Util.DisplayAlertAsync("確認", "現在のデータを閉じますか？", "閉じる", "キャンセル");
		if (!confirm)
			return;

		// Re-read Loader AFTER the await: an AppLink or websocket reconnect could
		// have swapped (and disposed) the old one while the confirm dialog was
		// open. Disposing the captured value would then double-dispose the old
		// loader and orphan the new one.
		var loader = viewModel.Loader;
		if (loader is null)
			return;

		viewModel.Loader = null;
		loader.Dispose();
		// Loader change triggers OnViewModelPropertyChanged -> animate back to Start mode.
	}

	// ----- Navigation -----

	public static async Task NavigateToDTACAsync()
	{
		try
		{
			logger.Info("Navigating to DTAC page");
			await Shell.Current.GoToAsync($"//{ViewHost.NameOfThisClass}");
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Error navigating to DTAC page");
			InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.NavigateToDTACAsync failed");
		}
	}
}
