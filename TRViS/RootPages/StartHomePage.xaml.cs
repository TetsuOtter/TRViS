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

public partial class StartHomePage : ContentPage
{
	public static readonly string NameOfThisClass = nameof(StartHomePage);
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	AppViewModel viewModel { get; }

	// Mode tracks whether the body shows Start (no Loader) or Home (Loader present).
	enum PageMode { Start, Home }
	PageMode _currentMode = PageMode.Start;

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

	// Animation tunables. Header is centered slightly above middle in Start mode;
	// pinned to top in Home mode.
	// START_HEADER_CENTER_FRACTION places the *center* of the header at this fraction
	// of the page height, so the visual "centeredness" stays consistent regardless of
	// header size (icon bigger/smaller, accessibility font scale).
	const double START_HEADER_CENTER_FRACTION = 0.32; // 0.5 = middle. 0.32 = "slightly above middle".
	// AppHeader has AnchorY=0 so Scale shrinks downward, keeping the visual top at the same y.
	// HOME_HEADER_SCALE controls the visual size of the Home-mode header band; smaller = more
	// vertical space for the WorkGroup/Work selection lists below.
	const double HOME_HEADER_SCALE = 0.55;
	const uint TRANSITION_MS = 380;
	// Below this short-side dimension we assume "smartphone" form factor. Above,
	// "tablet" — even in landscape orientation iPad mini's short side is well
	// over 700, so this picks the right side reliably without per-device
	// hard-coding.
	const double PHONE_SHORT_SIDE_MAX = 500;

	// Tracks whether we're currently laid out for phone-landscape (header on the
	// left, body on the right) or the default vertical layout. Updated from
	// ApplyOrientationLayout; consumed by ComputeStartHeaderTranslationY (which
	// returns 0 in landscape because the header has no vertical "centering"
	// translation to apply when it owns its own column) and ApplyHeaderLayoutInstant.
	bool _isLandscapePhone;

	double ComputeStartHeaderTranslationY()
	{
		// In landscape phone, AppHeader sits at the top of its dedicated left
		// column — no translation is needed to keep AppTitle visible.
		if (_isLandscapePhone)
			return 0;
		double pageHeight = Height > 0 ? Height : InstanceManager.AppViewModel.WindowHeight;
		double headerHeight = AppHeader.Height > 0 ? AppHeader.Height : 220;
		double bodyHeight = StartBody.Height > 0 ? StartBody.Height : 0;
		// RootGrid uses RowDefinitions="Auto,*"; AppHeader lives in Row 0, which is
		// (pageHeight - bodyHeight) tall. Center the header in that upper region
		// (clamped so a tall body on a small screen never pushes the header down
		// into StartBody's row, where its lower portion — including the AppTitle —
		// would be Z-ordered under StartBody and reported as visible=false).
		double upperRegion = Math.Max(headerHeight, pageHeight - bodyHeight);
		double centered = (upperRegion * START_HEADER_CENTER_FRACTION) - (headerHeight / 2);
		double maxTranslation = upperRegion - headerHeight;
		return Math.Clamp(centered, 0, Math.Max(0, maxTranslation));
	}

	public StartHomePage()
	{
		logger.Trace("Creating");

		InitializeComponent();

		viewModel = InstanceManager.AppViewModel;
		BindingContext = viewModel;

		WorkGroupListView.ItemsSource = _workGroupItems;
		WorkListView.ItemsSource = _workItems;

		// Apply initial header layout once we have a measured size.
		SizeChanged += OnSizeChangedFirstLayout;
		AppHeader.SizeChanged += (_, __) =>
		{
			UpdateHomeBodyTopSpacer();
			// AppHeader.Height is read by ComputeStartHeaderTranslationY; recompute
			// once it lands so the header centers correctly even on the first frame.
			RecomputeStartModeHeaderPosition();
		};
		// StartBody.Height feeds ComputeStartHeaderTranslationY too — without this
		// handler, the first SizeChanged on the page fires before StartBody is
		// measured, the formula falls back to bodyHeight=0, and translation ends
		// up large enough to push the header (and the AppTitle inside it) into
		// StartBody's row, where Z-order hides it on small screens.
		StartBody.SizeChanged += (_, __) => RecomputeStartModeHeaderPosition();

		logger.Trace("Created");
	}

	void UpdateHomeBodyTopSpacer()
	{
		// In landscape phone HomeBody sits in its own column to the right of the
		// header — no top spacer needed because the header is no longer above it.
		if (_isLandscapePhone)
		{
			HomeBodyTopSpacer.HeightRequest = 0;
			return;
		}
		// Reserve enough vertical space in HomeBody for the scaled-down header.
		// Falls back to a sane default before AppHeader has been measured.
		double measured = AppHeader.Height > 0 ? AppHeader.Height : 220;
		HomeBodyTopSpacer.HeightRequest = (measured * HOME_HEADER_SCALE) + 12;
	}

	void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(AppViewModel.Loader):
				logger.Debug("Loader changed -> evaluate page mode");
				// New loader -> wipe tentative state and rebuild the WorkGroup picker.
				ResetPendingSelection();
				RebuildWorkGroupItems();
				_ = ApplyModeForCurrentLoaderAsync();
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

	void OnSizeChangedFirstLayout(object? sender, EventArgs e)
	{
		if (Width <= 0 || Height <= 0)
			return;

		// Apply orientation first so RowDefinitions / ColumnDefinitions are settled
		// before we recompute the header translation (which depends on them).
		ApplyOrientationLayout();

		// Apply initial state without animation. We do this on every size change in case the
		// window resizes (desktop) or device rotates — but only animate when the *mode* changes;
		// pure size changes get a snap-update.
		ApplyHeaderLayoutInstant(_currentMode);
	}

	void RecomputeStartModeHeaderPosition()
	{
		if (_currentMode != PageMode.Start || Width <= 0 || Height <= 0)
			return;
		AppHeader.TranslationY = ComputeStartHeaderTranslationY();
	}

	/// <summary>
	/// Switches between vertical (header above body) and horizontal (header
	/// left, body right) layouts. The horizontal layout kicks in only on phones
	/// in landscape — where the vertical layout would otherwise overlap the
	/// AppHeader, the privacy banner, and StartBody's buttons all on top of
	/// each other (the page height is just too small to stack them).
	/// </summary>
	void ApplyOrientationLayout()
	{
		if (Width <= 0 || Height <= 0)
			return;
		bool isLandscapePhone = Width > Height && Math.Min(Width, Height) < PHONE_SHORT_SIDE_MAX;
		// Idempotent: skip the row/column reshuffle when the orientation hasn't
		// changed AND we have a populated grid (first call always populates).
		if (isLandscapePhone == _isLandscapePhone &&
			(RootGrid.RowDefinitions.Count > 0 || RootGrid.ColumnDefinitions.Count > 0))
			return;
		_isLandscapePhone = isLandscapePhone;
		RootGrid.RowDefinitions.Clear();
		RootGrid.ColumnDefinitions.Clear();
		if (isLandscapePhone)
		{
			RootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
			RootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
			Grid.SetRow(AppHeader, 0); Grid.SetRowSpan(AppHeader, 1);
			Grid.SetColumn(AppHeader, 0); Grid.SetColumnSpan(AppHeader, 1);
			Grid.SetRow(StartBody, 0); Grid.SetRowSpan(StartBody, 1);
			Grid.SetColumn(StartBody, 1); Grid.SetColumnSpan(StartBody, 1);
			// HomeBody also sits to the right of the header in landscape so the
			// WorkGroup/Work picker has its own column instead of sitting under
			// the header (which would compress it to a sliver in the lower half
			// of an already-short landscape viewport).
			Grid.SetRow(HomeBody, 0); Grid.SetRowSpan(HomeBody, 1);
			Grid.SetColumn(HomeBody, 1); Grid.SetColumnSpan(HomeBody, 1);
			Grid.SetRow(TestSeamHost, 0); Grid.SetColumn(TestSeamHost, 0);
			// StartBody anchored to the top of its column instead of the bottom —
			// VerticalOptions=End was useful when body sat below header in a row
			// layout, but in two-column layout End would push content below the
			// fold whenever the body is shorter than the column.
			StartBody.VerticalOptions = LayoutOptions.Start;
			// Home mode no longer needs the top spacer (header is on the left,
			// not on top) — recompute since UpdateHomeBodyTopSpacer keys off
			// _isLandscapePhone.
			UpdateHomeBodyTopSpacer();
		}
		else
		{
			RootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
			RootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
			Grid.SetRow(AppHeader, 0); Grid.SetRowSpan(AppHeader, 1);
			Grid.SetColumn(AppHeader, 0); Grid.SetColumnSpan(AppHeader, 1);
			Grid.SetRow(StartBody, 1); Grid.SetRowSpan(StartBody, 1);
			Grid.SetColumn(StartBody, 0); Grid.SetColumnSpan(StartBody, 1);
			Grid.SetRow(HomeBody, 0); Grid.SetRowSpan(HomeBody, 2);
			Grid.SetColumn(HomeBody, 0); Grid.SetColumnSpan(HomeBody, 1);
			Grid.SetRow(TestSeamHost, 0); Grid.SetColumn(TestSeamHost, 0);
			StartBody.VerticalOptions = LayoutOptions.End;
			UpdateHomeBodyTopSpacer();
		}
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		// Subscribe here (not in ctor) so each appearance pairs with an OnDisappearing
		// unsubscribe — avoids accumulating handlers if Shell recreates the page.
		viewModel.PropertyChanged -= OnViewModelPropertyChanged;
		viewModel.PropertyChanged += OnViewModelPropertyChanged;

		// TestSeamHost is always rendered; its 1×1 buttons are no-ops in
		// production builds (Click handlers gated on #if UI_TEST). No runtime
		// IsVisible flip — that pattern was unreliable across Mac Catalyst and
		// Windows MAUI accessibility trees.

		UpdatePrivacyDependentControls();
		UpdateHomeBodyTopSpacer();

		// Reflect any current loader / committed selection state. If we're returning
		// here from DTAC, the user's last commit becomes their initial pending state.
		RebuildWorkGroupItems();
		SyncPendingFromCommitted();

		// First-appearing default-timetable load (preserves SelectTrainPage behavior).
		// Only run when we don't yet have a Loader.
		if (viewModel.Loader is null)
		{
			logger.Info("Loader is null -> attempt default timetable load");

			try
			{
				(bool success, bool requiresFileSelection, string? selectedFilePath, string? errorMessage) =
					await viewModel.TryLoadDefaultTimetableAsync();

				if (success && !requiresFileSelection)
				{
					logger.Info("Default timetable loaded -> show Home picker (no auto-navigate)");
					await ApplyModeForCurrentLoaderAsync();
					return;
				}

				if (success && requiresFileSelection)
				{
					logger.Info("Multiple JSON files found - showing default-file selection sheet");
					await ShowFileSelectionDialogAsync();
					return;
				}

				if (errorMessage == "PrivacyPolicyNotAccepted")
				{
					logger.Info("Privacy policy not accepted -> stay on Start screen until user opens dialog");
					// Do NOT auto-load sample data here. The user must open the privacy dialog first.
					await ApplyModeForCurrentLoaderAsync();
					return;
				}

				// No default file. Stay on Start screen — do not auto-load sample data.
				logger.Info("No default timetable found -> stay on Start screen");
			}
			catch (Exception ex)
			{
				logger.Error(ex, "Error loading default timetable");
				InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.OnAppearing (TryLoadDefaultTimetableAsync failed)");
			}
		}

		await ApplyModeForCurrentLoaderAsync();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		viewModel.PropertyChanged -= OnViewModelPropertyChanged;
	}

	void UpdatePrivacyDependentControls()
	{
		// Until the privacy policy is accepted, none of the data-loading entry
		// points are usable: the reconfirm banner overlays the primary buttons
		// (so they're hidden behind it), and the demo-data button is hidden
		// outright since there is no second affordance to overlay it. After
		// acceptance the banner hides, the primary buttons reveal, and the demo
		// button reappears.
		bool accepted = InstanceManager.FirebaseSettingViewModel.IsPrivacyPolicyAccepted;
		PrivacyReconfirmBanner.IsVisible = !accepted;
		ConnectServerButton.IsEnabled = accepted;
		SelectFileButton.IsEnabled = accepted;
		LoadDemoButton.IsEnabled = accepted;
		LoadDemoButton.IsVisible = accepted;

		// Hide the Shell flyout (menu) toggle in the nav bar until the user accepts
		// the privacy policy — the body is essentially blocked by the reconfirm
		// banner anyway, and a reachable menu button is misleading. Skip Mac
		// Catalyst: its nav bar is built from FlyoutBehavior at Shell init time
		// only, so flipping post-init leaves the toggle stuck (see AppShell
		// comment). All other platforms (iOS/iPadOS, Android, Windows) re-render
		// the nav bar correctly when this flips.
		// Setting both the per-page attached property AND the Shell instance
		// property: on iOS the per-page attached property alone has been
		// observed not to repaint the existing nav bar after a value flip
		// (the toggle stays hidden after acceptance), so we also poke
		// Shell.Current.FlyoutBehavior to force a Shell-level redraw.
		if (!OperatingSystem.IsMacCatalyst())
		{
			var target = accepted ? FlyoutBehavior.Flyout : FlyoutBehavior.Disabled;
			Shell.SetFlyoutBehavior(this, target);
			if (Shell.Current is { } shell)
				shell.FlyoutBehavior = target;
		}
	}

	// ----- Mode / animation -----

	Task ApplyModeForCurrentLoaderAsync()
	{
		PageMode target = viewModel.Loader is null ? PageMode.Start : PageMode.Home;
		if (target == _currentMode)
		{
			// Refresh derived UI in case Loader was swapped to a new instance.
			UpdateLoaderInfoLabels();
			RefreshStepUi();
			return Task.CompletedTask;
		}
		return TransitionToAsync(target);
	}

	void ApplyHeaderLayoutInstant(PageMode mode)
	{
		if (mode == PageMode.Start)
		{
			AppHeader.TranslationY = ComputeStartHeaderTranslationY();
			AppHeader.Scale = 1.0;
			StartBody.IsVisible = true;
			StartBody.Opacity = 1;
			HomeBody.IsVisible = false;
			HomeBody.Opacity = 0;
		}
		else
		{
			AppHeader.TranslationY = 0;
			AppHeader.Scale = HOME_HEADER_SCALE;
			StartBody.IsVisible = false;
			StartBody.Opacity = 0;
			HomeBody.IsVisible = true;
			HomeBody.Opacity = 1;
		}
		_currentMode = mode;
		UpdateLoaderInfoLabels();
		RefreshStepUi();
	}

	async Task TransitionToAsync(PageMode target)
	{
		logger.Info("Transitioning page mode {0} -> {1}", _currentMode, target);

		double centeredOffset = ComputeStartHeaderTranslationY();

		double headerFromY, headerToY, headerFromScale, headerToScale;
		double startBodyFrom, startBodyTo, homeBodyFrom, homeBodyTo;

		if (target == PageMode.Home)
		{
			headerFromY = AppHeader.TranslationY;
			headerToY = 0;
			headerFromScale = AppHeader.Scale;
			headerToScale = HOME_HEADER_SCALE;
			startBodyFrom = StartBody.Opacity;
			startBodyTo = 0;
			homeBodyFrom = 0;
			homeBodyTo = 1;

			HomeBody.Opacity = 0;
			HomeBody.IsVisible = true;
		}
		else
		{
			headerFromY = AppHeader.TranslationY;
			headerToY = centeredOffset;
			headerFromScale = AppHeader.Scale;
			headerToScale = 1.0;
			startBodyFrom = StartBody.Opacity;
			startBodyTo = 1;
			homeBodyFrom = HomeBody.Opacity;
			homeBodyTo = 0;

			StartBody.Opacity = startBodyFrom;
			StartBody.IsVisible = true;
		}

		_currentMode = target;
		UpdateLoaderInfoLabels();

		var animation = new Animation
		{
			{ 0, 1, new Animation(v => AppHeader.TranslationY = v, headerFromY, headerToY, Easing.CubicInOut) },
			{ 0, 1, new Animation(v => AppHeader.Scale = v, headerFromScale, headerToScale, Easing.CubicInOut) },
			{ 0, 1, new Animation(v => StartBody.Opacity = v, startBodyFrom, startBodyTo, Easing.CubicOut) },
			{ 0, 1, new Animation(v => HomeBody.Opacity = v, homeBodyFrom, homeBodyTo, Easing.CubicOut) },
		};

		var tcs = new TaskCompletionSource<bool>();
		this.Animate(
			"StartHomePage.ModeTransition",
			animation,
			length: TRANSITION_MS,
			// finished signature: (final-progress, was-cancelled). When cancelled the
			// follow-up visibility flip below is skipped — but the *next* TransitionToAsync
			// runs ApplyHeaderLayoutInstant via fall-through if it's the same target,
			// or sets the right visibility on its own animation completion if different.
			finished: (_, cancelled) => tcs.TrySetResult(cancelled));
		bool wasCancelled = await tcs.Task;

		if (wasCancelled)
		{
			// A newer transition superseded us. Don't flip visibility here — the new
			// transition is already animating with its own from/to values.
			return;
		}

		if (target == PageMode.Home)
		{
			StartBody.IsVisible = false;
		}
		else
		{
			HomeBody.IsVisible = false;
		}
	}

	void UpdateLoaderInfoLabels()
	{
		ILoader? loader = viewModel.Loader;
		if (loader is null)
		{
			LoaderInfoTitleLabel.Text = "読み込み済みデータ";
			LoaderInfoDetailLabel.Text = "";
			LoaderInfoGlyphLabel.Text = ""; // description (generic file)
			return;
		}

		// Title = loader type, glyph = matching Material Icon, detail = source label
		// (file name, URL) set atomically with the loader via AppViewModel.SetLoader.
		(string title, string glyph) = loader switch
		{
			SampleDataLoader => ("デモデータ", ""),                  // settings_input_component
			LoaderJson => ("JSON ファイル", ""),                       // description
			LoaderSQL => ("SQLite ファイル", ""),                      // storage
			WebSocketNetworkSyncService => ("サーバー接続中", ""),     // wifi
			_ => (loader.GetType().Name, ""),
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
		var committedWG = viewModel.SelectedWorkGroup;
		var committedWork = viewModel.SelectedWork;

		if (!Equals(_pendingWorkGroup, committedWG))
		{
			_pendingWorkGroup = committedWG;
			RebuildWorkItems();
		}
		if (!Equals(_pendingWork, committedWork))
		{
			_pendingWork = committedWork;
		}

		SyncListViewSelections();
		RefreshStepUi();
	}

	void SyncListViewSelections()
	{
		// Reflect _pendingWorkGroup / _pendingWork onto the CollectionViews without
		// re-triggering OnXxxSelectionChanged (which would reset the user-pick flow).
		_suppressSelectionChanged = true;
		try
		{
			WorkGroupListView.SelectedItem = _pendingWorkGroup is null
				? null
				: _workGroupItems.FirstOrDefault(i => Equals(i.Source, _pendingWorkGroup));
			WorkListView.SelectedItem = _pendingWork is null
				? null
				: _workItems.FirstOrDefault(i => Equals(i.Source, _pendingWork));
		}
		finally
		{
			_suppressSelectionChanged = false;
		}
	}

	void RebuildWorkGroupItems()
	{
		_workGroupItems.Clear();
		var loader = viewModel.Loader;
		var groups = viewModel.WorkGroupList;
		if (loader is null || groups is null)
		{
			RebuildWorkItems();
			return;
		}
		foreach (var wg in groups)
		{
			int workCount;
			try { workCount = loader.GetWorkList(wg.Id).Count; }
			catch { workCount = 0; }
			string subtitle = $"Work 数: {workCount}";
			_workGroupItems.Add(new WorkGroupListItem(wg, wg.Name, subtitle));
		}
		RebuildWorkItems();
	}

	void RebuildWorkItems()
	{
		_workItems.Clear();
		var loader = viewModel.Loader;
		var wg = _pendingWorkGroup;
		if (loader is null || wg is null)
			return;

		IReadOnlyList<Work> works;
		try { works = loader.GetWorkList(wg.Id); }
		catch { works = Array.Empty<Work>(); }

		foreach (var w in works)
		{
			int trainCount;
			try { trainCount = loader.GetTrainDataList(w.Id).Count; }
			catch { trainCount = 0; }

			List<string> parts = new(2);
			if (w.AffectDate is { } d)
				parts.Add($"施行日: {d:yyyy/MM/dd}");
			parts.Add($"列車数: {trainCount}");
			_workItems.Add(new WorkListItem(w, w.Name, string.Join(" · ", parts)));
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
		WorkPendingHint.IsVisible = !hasWorkGroup;
		WorkChipNameLabel.Text = pendingW?.Name ?? string.Empty;
	}

	void OnWorkGroupSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (_suppressSelectionChanged)
			return;
		var item = WorkGroupListView.SelectedItem as WorkGroupListItem;
		_pendingWorkGroup = item?.Source;
		// Switching Work Group invalidates any prior Work pick and rebuilds the
		// Work list for the new group.
		_pendingWork = null;
		RebuildWorkItems();
		SyncListViewSelections();
		RefreshStepUi();
	}

	void OnWorkSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (_suppressSelectionChanged)
			return;
		var item = WorkListView.SelectedItem as WorkListItem;
		_pendingWork = item?.Source;
		RefreshStepUi();
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

	// ----- Button handlers -----

	async void OnConnectServerClicked(object sender, EventArgs e)
	{
		logger.Info("Connect Server clicked");

		try
		{
			await Navigation.PushModalAsync(new ConnectServerDialog());
		}
		catch (Exception ex)
		{
			InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.OnConnectServerClicked (PushModalAsync failed)");
			logger.Error(ex, "PushModalAsync failed");
			await Util.DisplayAlertAsync(this, "Open Popup Failed", ex.ToString(), "OK");
		}
	}

	async void OnSelectFileClicked(object sender, EventArgs e)
	{
		logger.Info("Select File clicked");

		try
		{
			await Navigation.PushModalAsync(new SelectFileDialog());
		}
		catch (Exception ex)
		{
			InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.OnSelectFileClicked (PushModalAsync failed)");
			logger.Error(ex, "PushModalAsync failed");
			await Util.DisplayAlertAsync(this, "Open Dialog Failed", ex.ToString(), "OK");
		}
	}

	async void OnLoadDemoClicked(object sender, EventArgs e)
	{
		logger.Info("Load Demo clicked");

		try
		{
			viewModel.Loader?.Dispose();
			viewModel.SetLoader(await SampleDataLoader.CreateAsync(), null);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Load demo failed");
			InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.OnLoadDemoClicked (CreateAsync failed)");
			await Util.DisplayAlertAsync(this, "エラー", $"サンプルデータの読み込みに失敗しました: {ex.Message}", "OK");
		}
	}

	async void OnPrivacyPolicyClicked(object sender, EventArgs e)
	{
		logger.Info("Privacy Policy clicked");
		try
		{
			await Navigation.PushModalAsync(new PrivacyPolicyDialog());
			// After modal closes, banner + button enabled-state may have changed.
			UpdatePrivacyDependentControls();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "PushModalAsync(PrivacyPolicyDialog) failed");
			InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.OnPrivacyPolicyClicked");
		}
	}

	async void OnThirdPartyLicensesClicked(object sender, EventArgs e)
	{
		logger.Info("Third Party Licenses clicked");
		try
		{
			// asModal:true makes the in-page Close header visible so the modal can be
			// dismissed reliably across platforms (NavigationPage toolbar items render
			// inconsistently on Mac Catalyst).
			var page = new ThirdPartyLicenses(asModal: true);
			await Navigation.PushModalAsync(page);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "PushModalAsync(ThirdPartyLicenses) failed");
			InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.OnThirdPartyLicensesClicked");
		}
	}

	async void OnOpenClicked(object sender, EventArgs e)
	{
		logger.Info("Open clicked");
		var pendingWG = _pendingWorkGroup;
		var pendingW = _pendingWork;

		if (pendingWG is null || pendingW is null)
		{
			logger.Info("Open ignored: pending selection incomplete (WG={0}, W={1})", pendingWG, pendingW);
			await Util.DisplayAlertAsync(this, "選択されていません", "Work Group と Work を選択してから「開く」を押してください。", "OK");
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
	void CommitPendingSelection(WorkGroup workGroup, Work work)
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

		bool confirm = await Util.DisplayAlertAsync(this, "確認", "現在のデータを閉じますか？", "閉じる", "キャンセル");
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

	// ----- Helpers -----

	private static async Task NavigateToDTACAsync()
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

	private async Task ShowFileSelectionDialogAsync()
	{
		try
		{
			var jsonFiles = DefaultTimetableFileLoader.GetAvailableJsonFiles();
			if (jsonFiles.Length == 0)
			{
				logger.Warn("No JSON files found for default selection");
				return;
			}

			var fileNames = jsonFiles.Select(f => f.Name).ToArray();
			var filePaths = jsonFiles.Select(f => f.FullName).ToArray();

			string? selected = await DisplayActionSheetAsync(
				"どのファイルを開きますか？",
				"キャンセル",
				null,
				fileNames);

			if (string.IsNullOrEmpty(selected) || selected == "キャンセル")
				return;

			int idx = Array.IndexOf(fileNames, selected);
			if (idx < 0 || idx >= filePaths.Length)
				return;

			bool loaded = await viewModel.LoadSelectedTimetableFileAsync(filePaths[idx]);
			if (loaded)
				await ApplyModeForCurrentLoaderAsync();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "ShowFileSelectionDialogAsync failed");
			InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.ShowFileSelectionDialogAsync");
		}
	}

	// ----- Test seams -----

	void TestSeedButton_Clicked(object sender, EventArgs e)
	{
#if UI_TEST
		logger.Info("TestSeedButton clicked: seeding URL history fixtures");
		viewModel.SeedUrlHistoryForTesting(new[]
		{
			"https://example.com/timetable-a.json",
			"https://example.com/timetable-b.json",
		});
#endif
	}

	async void TestAutoOpenButton_Clicked(object sender, EventArgs e)
	{
#if UI_TEST
		logger.Info("TestAutoOpenButton clicked: auto-pick first WG/Work and navigate to DTAC");
		try
		{
			// Mimic the user flow exactly: pick first WG, materialize its Work list,
			// pick first Work, then commit through the same code path 開く uses.
			// This keeps the seam honest as a "skip the picker UI" shortcut, not a
			// parallel path that could survive a refactor of the real Open button.
			var groups = viewModel.WorkGroupList;
			var firstGroup = groups?.FirstOrDefault();
			if (firstGroup is null)
			{
				logger.Warn("TestAutoOpenButton: no WorkGroup available — ignoring");
				return;
			}
			var loader = viewModel.Loader;
			if (loader is null)
				return;
			var firstWork = loader.GetWorkList(firstGroup.Id)?.FirstOrDefault();
			if (firstWork is null)
			{
				logger.Warn("TestAutoOpenButton: first WorkGroup has no Work — aborting navigate");
				return;
			}

			CommitPendingSelection(firstGroup, firstWork);
			await NavigateToDTACAsync();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "TestAutoOpenButton failed");
		}
#else
		await Task.CompletedTask;
#endif
	}

	void TestSeedGpsButton_Clicked(object sender, EventArgs e)
	{
#if UI_TEST
		logger.Info("TestSeedGpsButton clicked: pushing fixture GPS coord");
		try
		{
			var locationService = InstanceManager.LocationService;
			locationService.SetLonLatLocationService();
			locationService.SetGpsLocation(longitude: 139.790, latitude: 35.700, accuracy: 10.0, useAverageDistance: false);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "TestSeedGpsButton failed");
		}
#endif
	}
}
