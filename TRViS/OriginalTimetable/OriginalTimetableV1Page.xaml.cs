using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;

using TR.Maui.AnchorPopover;

using TRViS.IO.Models;
using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS.OriginalTimetable;

// V1 Modern Classic — 独自時刻表ページ骨格 (Phase 1: tablet ≥600pt only).
//
// Phase 1 covers: real-data row mapping, sticky header, section-break rows
// (RunOutLimit ↔ RunInLimit transition), marker badges, memo dots, note
// toggle button, SwipeItem CycleMarker/ClearMarker commands, and width-based
// tablet/compact split.
//
// History: an early iteration used a hand-rolled tablet Layout that hit an
// ApplyStyleSheets NRE on iPad mini A17; that was replaced by a CollectionView
// + DataTemplate which then hit Apple's ObservationTracking._AccessList /
// _NativeDictionary.copy use-after-free during cell recycling (swiftlang#84228 —
// triggered by HtmlAutoDetectLabel inside the recycled cells). The current
// shape replaces CollectionView with BindableLayout-on-VerticalStackLayout
// inside a ScrollView: V1RowTablet / V1RowCompact (programmatic View subclasses
// in V1Row.cs) are instantiated per item by the BindableLayout. Children are
// added as regular VerticalStackLayout siblings, so no UICollectionView /
// SwiftUI ViewGraph is involved on iOS and the crash path is gone.
public partial class OriginalTimetableV1Page : ContentPage
{
	public static readonly string NameOfThisClass = nameof(OriginalTimetableV1Page);

	// Tablet ≥600pt (Material breakpoint; iPad mini portrait 744pt counts).
	const double TabletBreakpoint = 600;

	readonly OriginalTimetableViewModel _vm;
	double _lastWidth = -1;
	bool _lastIsTablet;

	public ObservableCollection<V1RowItem> Items { get; } = new();

	// Train-header bindings (refreshed by RebuildItems). The header mirrors the
	// design's 3 sections: brand row (depot + train pager + route name), train
	// header (type chip / large number / 両数 / 最高速度) and clock + direction.
	public string DepotName { get; private set; } = string.Empty;
	public string RouteNameText { get; private set; } = string.Empty;
	// Train pager — dot indicator (current train = wide accent pill, others = dots).
	public IReadOnlyList<bool> PagerDots { get; private set; } = [];
	public bool HasMultipleTrains { get; private set; }
	public string HeaderTypeText { get; private set; } = string.Empty;
	public bool HasHeaderType { get; private set; }
	public string HeaderTrainNumberText { get; private set; } = string.Empty;
	public string HeaderCarCountText { get; private set; } = string.Empty;
	public string HeaderMaxSpeedText { get; private set; } = string.Empty;
	public string OriginDestText { get; private set; } = string.Empty;
	// Live wall-clock (現在時刻), driven by _clockTimer off InstanceManager.TimeProvider
	// so the UI_TEST clock-freeze seam keeps screenshots deterministic.
	public string CurrentTimeText { get; private set; } = string.Empty;
	public bool HasActiveTrain { get; private set; }
	public bool HasNoActiveTrain => !HasActiveTrain;

	// SwipeItem commands invoked from inside the DataTemplate.
	// Use {Source={x:Reference Self}, ...} bindings inside the template; the
	// CommandParameter is the row id so the command can address markers.
	public ICommand CycleMarkerCommand { get; }
	public ICommand ClearMarkerCommand { get; }
	public ICommand OpenMemoCommand { get; }
	public ICommand ToggleNoteCommand { get; }
	public ICommand OpenMarkerPopoverFromSwipeCommand { get; }

	// Sheet-edit state. _memoRowId is set when MemoSheetOverlay opens; cleared
	// on save/cancel/delete. Kept private and synchronous — no INPC needed.
	string? _memoRowId;

	// Live-clock ticker for the header 現在時刻. Started on appear, stopped on
	// disappear; reads the freezable InstanceManager.TimeProvider.
	IDispatcherTimer? _clockTimer;

	public OriginalTimetableV1Page()
	{
		_vm = InstanceManager.OriginalTimetableViewModel;

		CycleMarkerCommand = new Command<string>(OnCycleMarker);
		ClearMarkerCommand = new Command<string>(OnClearMarker);
		OpenMemoCommand = new Command<string>(OnOpenMemo);
		ToggleNoteCommand = new Command<string>(OnToggleNote);
		OpenMarkerPopoverFromSwipeCommand = new Command<string>(OnOpenMarkerPopoverFromSwipe);

		InitializeComponent();
		BindingContext = _vm;

#if UI_TEST
		AddTestSeamButtons();
#endif
	}

#if UI_TEST
	// UI_TEST-only seams: SwipeView's SwipeItem doesn't reliably surface as a
	// tappable Button in XCUITest's accessibility tree (it derives from MenuItem),
	// so each test that exercises the marker-cycle path through the SwipeView's
	// Command binding gets a parallel invocation path that drives the exact same
	// _vm.CycleMarker/ClearMarker handlers used by the SwipeItem. The test thus
	// covers the View→VM wiring without depending on simulated swipe gesture
	// reliability across platforms / OS versions.
	const string AutomationIdValueForTestCycleMarkerRow0 = "OriginalTimetable.V1.Test.CycleMarkerRow0";
	const string AutomationIdValueForTestClearMarkerRow0 = "OriginalTimetable.V1.Test.ClearMarkerRow0";

	void AddTestSeamButtons()
	{
		// Invisible 24×24 buttons placed at the bottom-right corner of RootGrid
		// (above the visual tree but transparent). Tests find them by AutomationId
		// and tap them to invoke the same OnCycleMarker / OnClearMarker handlers
		// the SwipeView's Command binding points at.
		var cycle = new Button
		{
			AutomationId = AutomationIdValueForTestCycleMarkerRow0,
			HorizontalOptions = LayoutOptions.End,
			VerticalOptions = LayoutOptions.End,
			WidthRequest = 24,
			HeightRequest = 24,
			BackgroundColor = Colors.Transparent,
			BorderColor = Colors.Transparent,
			Padding = 0,
			Margin = new Thickness(0, 0, 0, 0),
		};
		cycle.Clicked += (_, _) => CycleMarkerOnRow0ForTesting();
		RootGrid.Children.Add(cycle);

		var clear = new Button
		{
			AutomationId = AutomationIdValueForTestClearMarkerRow0,
			HorizontalOptions = LayoutOptions.End,
			VerticalOptions = LayoutOptions.End,
			WidthRequest = 24,
			HeightRequest = 24,
			BackgroundColor = Colors.Transparent,
			BorderColor = Colors.Transparent,
			Padding = 0,
			Margin = new Thickness(0, 0, 28, 0),
		};
		clear.Clicked += (_, _) => ClearMarkerOnRow0ForTesting();
		RootGrid.Children.Add(clear);
	}

	void CycleMarkerOnRow0ForTesting()
	{
		var firstNormal = FindFirstNormalRowId();
		if (firstNormal is not null)
			OnCycleMarker(firstNormal);
	}

	void ClearMarkerOnRow0ForTesting()
	{
		var firstNormal = FindFirstNormalRowId();
		if (firstNormal is not null)
			OnClearMarker(firstNormal);
	}

	string? FindFirstNormalRowId()
	{
		foreach (var i in Items)
		{
			if (!i.IsSectionBreakRow)
				return i.Id;
		}
		return null;
	}
#endif

	protected override void OnAppearing()
	{
		base.OnAppearing();
		InstanceManager.OrientationService.SetOrientation(AppDisplayOrientation.Portrait);
		_vm.PropertyChanged += OnVmPropertyChanged;
		// iOS safe-area Top inset: RootGrid would otherwise underlap the status bar /
		// notch. AppShell.SafeAreaMargin is zero on non-iOS, so this is cross-platform safe.
		if (Shell.Current is AppShell appShell)
		{
			appShell.SafeAreaMarginChanged += AppShell_SafeAreaMarginChanged;
			AppShell_SafeAreaMarginChanged(appShell, new(), appShell.SafeAreaMargin);
		}
		ApplyLayoutForWidth(Width);
		try { RebuildItems(); }
		catch (Exception ex) { Console.Error.WriteLine($"[V1] RebuildItems: {ex}"); }
		StartClock();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		InstanceManager.OrientationService.SetOrientation(AppDisplayOrientation.All);
		_vm.PropertyChanged -= OnVmPropertyChanged;
		StopClock();
		if (Shell.Current is AppShell appShell)
		{
			appShell.SafeAreaMarginChanged -= AppShell_SafeAreaMarginChanged;
		}
	}

	// ── Header 現在時刻 live clock ──────────────────────────────────────────────
	void StartClock()
	{
		UpdateClock();
		_clockTimer ??= Application.Current?.Dispatcher.CreateTimer();
		if (_clockTimer is null)
			return;
		_clockTimer.Interval = TimeSpan.FromSeconds(1);
		_clockTimer.Tick -= OnClockTick;
		_clockTimer.Tick += OnClockTick;
		_clockTimer.Start();
	}

	void StopClock() => _clockTimer?.Stop();

	void OnClockTick(object? sender, EventArgs e) => UpdateClock();

	void UpdateClock()
	{
		int sec = ((InstanceManager.TimeProvider.GetCurrentTimeSeconds() % 86400) + 86400) % 86400;
		string next = $"{sec / 3600:D2}:{sec % 3600 / 60:D2}:{sec % 60:D2}";
		if (next == CurrentTimeText)
			return;
		CurrentTimeText = next;
		OnPropertyChanged(nameof(CurrentTimeText));
	}

	// ── Train pager (‹ idx/count ›) — switches the active train within the work ─
	void OnPrevTrainTapped(object? sender, TappedEventArgs e) => _vm.PrevTrain();
	void OnNextTrainTapped(object? sender, TappedEventArgs e) => _vm.NextTrain();

	// One bool per train; the active index is true (rendered as the wide pill).
	static IReadOnlyList<bool> BuildPagerDots(int count, int activeIdx)
	{
		if (count <= 0)
			return [];
		var dots = new bool[count];
		if (activeIdx >= 0 && activeIdx < count)
			dots[activeIdx] = true;
		return dots;
	}

	// DTAC sample data packs multi-line strings into scalar fields (SpeedType /
	// MaxSpeed). The header wants a single headline value, so take the first line.
	static string FirstLine(string? s)
	{
		if (string.IsNullOrEmpty(s))
			return string.Empty;
		int nl = s.IndexOfAny(new[] { '\n', '\r' });
		return (nl < 0 ? s : s[..nl]).Trim();
	}

	static string BuildOriginDestText(TrainData? train)
	{
		if (train?.Rows is null || train.Rows.Length == 0)
			return string.Empty;
		string origin = string.Empty, dest = string.Empty;
		foreach (var r in train.Rows)
		{
			if (r.IsInfoRow || string.IsNullOrEmpty(r.StationName))
				continue;
			origin = r.StationName!;
			break;
		}
		for (int i = train.Rows.Length - 1; i >= 0; i--)
		{
			var r = train.Rows[i];
			if (r.IsInfoRow || string.IsNullOrEmpty(r.StationName))
				continue;
			dest = r.StationName!;
			break;
		}
		return (origin.Length == 0 && dest.Length == 0) ? string.Empty : $"{origin} → {dest}";
	}

	void AppShell_SafeAreaMarginChanged(object? sender, Thickness oldValue, Thickness newValue)
	{
		// Top inset only — the flyout-toggle / Tweaks gear sit in the header at
		// the top of RootGrid. Bottom/sides are bezel-safe without margin.
		RootGrid.Margin = new Thickness(0, newValue.Top, 0, 0);
	}

	// Flyout-toggle (hamburger) handler. NavBar is hidden via Shell.NavBarIsVisible="False"
	// on each Vx page, so we surface our own toggle in the page's custom header.
	void OnFlyoutToggleTapped(object? sender, TappedEventArgs e)
	{
		if (Shell.Current is not null)
			Shell.Current.FlyoutIsPresented = !Shell.Current.FlyoutIsPresented;
	}

	void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(OriginalTimetableViewModel.ActiveTrain):
			// Phase 3 — ShowPasses tweak toggles whether IsPass rows survive
			// RebuildItems' filter. The visible row set changes, so we have
			// to rebuild (scroll reset is acceptable here).
			case nameof(OriginalTimetableViewModel.ShowPasses):
				RebuildItems();
				break;
			// Partial in-place updates — V1RowItem is an ObservableObject so
			// mutating its props refreshes the bound visuals without ever
			// touching Items, which preserves ScrollView scroll position.
			case nameof(OriginalTimetableViewModel.MarkersVersion):
				UpdateMarkersInPlace();
				break;
			case nameof(OriginalTimetableViewModel.MemosVersion):
				UpdateMemosInPlace();
				break;
			case nameof(OriginalTimetableViewModel.NoteOpenVersion):
				UpdateNoteOpenInPlace();
				break;
			case nameof(OriginalTimetableViewModel.CurIdxVersion):
				UpdateCurrentInPlace();
				AutoFollowScroll();
				break;
			case nameof(OriginalTimetableViewModel.Density):
				UpdateDensityInPlace();
				break;
		}
	}

	void UpdateMarkersInPlace()
	{
		var train = _vm.ActiveTrain;
		if (train is null)
			return;
		foreach (var item in Items)
		{
			if (item.IsSectionBreakRow)
				continue;
			var marker = _vm.GetMarker(train.Id, item.Id);
			item.Marker = marker;
			ApplyDerivedStyling(item);
		}
	}

	void UpdateMemosInPlace()
	{
		var train = _vm.ActiveTrain;
		if (train is null)
			return;
		foreach (var item in Items)
		{
			if (item.IsSectionBreakRow)
				continue;
			item.HasMemo = !string.IsNullOrWhiteSpace(_vm.GetMemo(train.Id, item.Id));
		}
	}

	void UpdateNoteOpenInPlace()
	{
		var train = _vm.ActiveTrain;
		if (train is null)
			return;
		foreach (var item in Items)
		{
			if (item.IsSectionBreakRow)
				continue;
			item.IsNoteOpen = item.HasNote && _vm.IsNoteOpen(train.Id, item.Id);
		}
	}

	void UpdateCurrentInPlace()
	{
		var train = _vm.ActiveTrain;
		if (train is null)
			return;
		int curOrigIdx = _vm.GetCurIdxOverride(train.Id) ?? 0;
		foreach (var item in Items)
		{
			if (item.IsSectionBreakRow)
				continue;
			item.IsCurrent = item.OrigIndex == curOrigIdx;
		}
	}

	void UpdateDensityInPlace()
	{
		var (tabletPad, compactPad, tabletStation, compactStation) = DensityMetrics(_vm.Density);
		foreach (var item in Items)
		{
			if (item.IsSectionBreakRow)
				continue;
			item.TabletRowPadding = tabletPad;
			item.CompactRowPadding = compactPad;
			item.TabletStationFontSize = tabletStation;
			item.CompactStationFontSize = compactStation;
		}
	}

	// state.jsx densityScale: compact=0.82, comfortable=1.0, spacious=1.12.
	// Approach (c): scale row padding (vertical breathing room) and the
	// station-name font size only; smaller numeric columns stay fixed so the
	// time grid keeps a stable rhythm. Padding base values mirror the original
	// XAML literals (Tablet 12,8 / Compact 10,7).
	static (Thickness tabletPad, Thickness compactPad, double tabletStation, double compactStation)
		DensityMetrics(Density d)
	{
		double scale = d switch
		{
			Density.Compact => 0.82,
			Density.Spacious => 1.12,
			_ => 1.0,
		};
		var tabletPad = new Thickness(12, Math.Round(8 * scale));
		var compactPad = new Thickness(10, Math.Round(7 * scale));
		double tabletStation = Math.Round(32 * scale, 1);
		double compactStation = Math.Round(20 * scale, 1);
		return (tabletPad, compactPad, tabletStation, compactStation);
	}

	// Auto-follow: when Follow=true, scroll the visible row host to the
	// current-station row (center). Called after the CurIdxVersion in-place
	// pass has flipped IsCurrent on each row.
	//
	// Post-refactor: rows live inside a BindableLayout-driven VerticalStackLayout
	// (TabletRowsHost / CompactRowsHost) wrapped in a ScrollView. BindableLayout
	// preserves Items order so the child at index i corresponds to Items[i].
	// We scroll to that child via ScrollView.ScrollToAsync (the View overload).
	async void AutoFollowScroll()
	{
		if (!_vm.Follow)
			return;
		var train = _vm.ActiveTrain;
		if (train is null)
			return;
		int curOrigIdx = _vm.GetCurIdxOverride(train.Id) ?? 0;
		int curIdx = -1;
		for (int i = 0; i < Items.Count; i++)
		{
			var it = Items[i];
			if (!it.IsSectionBreakRow && it.OrigIndex == curOrigIdx)
			{
				curIdx = i;
				break;
			}
		}
		if (curIdx < 0)
			return;

		bool tablet = TabletGrid.IsVisible;
		ScrollView scroll = tablet ? TabletScroll : CompactScroll;
		VerticalStackLayout host = tablet ? TabletRowsHost : CompactRowsHost;
		if (curIdx >= host.Children.Count)
			return;
		if (host.Children[curIdx] is not View target)
			return;
		try
		{
			await scroll.ScrollToAsync(target, ScrollToPosition.Center, animated: true);
		}
		catch
		{
			// ScrollToAsync can throw if the host hasn't measured yet — swallow
			// rather than crash auto-follow.
		}
	}

	void OnRootSizeChanged(object? sender, EventArgs e)
	{
		ApplyLayoutForWidth(Width);
	}

	void ApplyLayoutForWidth(double width)
	{
		if (width <= 0)
			return;
		bool isTablet = width >= TabletBreakpoint;
		if (Math.Abs(width - _lastWidth) < 0.5 && isTablet == _lastIsTablet)
			return;
		_lastWidth = width;
		_lastIsTablet = isTablet;

		// Both child Grids are declared in XAML — only flip IsVisible here to
		// keep imperative tree manipulation minimal (avoids the ApplyStyleSheets
		// NRE path that bit the previous V1 implementation). Phase 3 promoted
		// the former CompactPlaceholder Label into a real CompactGrid that
		// mirrors the tablet layout's row template at a 4-column scale.
		TabletGrid.IsVisible = isTablet;
		CompactGrid.IsVisible = !isTablet;
	}

	void RebuildItems()
	{
		var train = _vm.ActiveTrain;
		HasActiveTrain = train is not null;
		// Brand row: depot = the selected WorkGroup name; route = work name.
		DepotName = InstanceManager.AppViewModel.SelectedWorkGroup?.Name ?? string.Empty;
		RouteNameText = train?.WorkName ?? string.Empty;
		var trainList = InstanceManager.AppViewModel.OrderedTrainDataList;
		int trainCount = trainList?.Count ?? 0;
		HasMultipleTrains = trainCount > 1;
		PagerDots = BuildPagerDots(trainCount, _vm.ActiveTrainIdx);
		HeaderTypeText = FirstLine(train?.SpeedType);
		HasHeaderType = !string.IsNullOrEmpty(HeaderTypeText);
		HeaderTrainNumberText = train?.TrainNumber ?? string.Empty;
		HeaderCarCountText = train?.CarCount is int cc ? $"{cc}両" : "—";
		HeaderMaxSpeedText = FirstLine(train?.MaxSpeed) is { Length: > 0 } ms ? $"{ms}km/h" : "—";
		// Direction: first → last non-info station of the train's rows.
		OriginDestText = BuildOriginDestText(train);
		OnPropertyChanged(nameof(HasActiveTrain));
		OnPropertyChanged(nameof(HasNoActiveTrain));
		OnPropertyChanged(nameof(DepotName));
		OnPropertyChanged(nameof(RouteNameText));
		OnPropertyChanged(nameof(PagerDots));
		OnPropertyChanged(nameof(HasMultipleTrains));
		OnPropertyChanged(nameof(HeaderTypeText));
		OnPropertyChanged(nameof(HasHeaderType));
		OnPropertyChanged(nameof(HeaderTrainNumberText));
		OnPropertyChanged(nameof(HeaderCarCountText));
		OnPropertyChanged(nameof(HeaderMaxSpeedText));
		OnPropertyChanged(nameof(OriginDestText));

		Items.Clear();
		if (train is null || train.Rows is null || train.Rows.Length == 0)
			return;

		// Phase 1: skip info rows. Phase 3: also skip IsPass rows when the
		// tweaks-panel ShowPasses toggle is off. Track the *visible* index
		// for striping; CurIdx semantics stay tied to the underlying
		// ActiveTrain.Rows index (matches VM.Advance).
		bool showPasses = _vm.ShowPasses;
		var visibleRows = new List<(int origIdx, TimetableRow row)>(train.Rows.Length);
		for (int i = 0; i < train.Rows.Length; i++)
		{
			var r = train.Rows[i];
			if (r.IsInfoRow)
				continue;
			if (!showPasses && r.IsPass)
				continue;
			visibleRows.Add((i, r));
		}

		int curOrigIdx = _vm.GetCurIdxOverride(train.Id) ?? 0;
		var (tabletPad, compactPad, tabletStation, compactStation) = DensityMetrics(_vm.Density);

		int visibleSeq = 0;
		TimetableRow? prev = null;
		foreach (var (origIdx, row) in visibleRows)
		{
			// Section break: insert *before* the current row when the prior
			// row's RunOutLimit differs from this row's RunInLimit. Skip when
			// there is no prior (first visible row).
			if (prev is not null && prev.RunOutLimit != row.RunInLimit)
			{
				var newLimit = row.RunInLimit;
				var label = newLimit is int v
					? $"▼ 区間切替 — 最高 {v}km/h"
					: "▼ 区間切替";
				Items.Add(V1RowItem.SectionBreak(id: $"sb:{row.Id}", label: label));
			}

			bool isCurrent = origIdx == curOrigIdx;
			bool isAlt = (visibleSeq % 2) == 1;
			var marker = _vm.GetMarker(train.Id, row.Id);
			bool hasMemo = !string.IsNullOrWhiteSpace(_vm.GetMemo(train.Id, row.Id));
			bool hasNote = !string.IsNullOrWhiteSpace(row.Remarks);
			bool isNoteOpen = hasNote && _vm.IsNoteOpen(train.Id, row.Id);

			var item = new V1RowItem
			{
				Id = row.Id,
				OrigIndex = origIdx,
				StationName = row.StationName ?? string.Empty,
				KanaName = string.Empty,
				RunText = FormatRunMinutes(row.DriveTimeMM, row.DriveTimeSS),
				ArriveText = FormatHhMm(row.ArriveTime),
				DepartText = FormatHhMm(row.DepartureTime),
				TrackName = row.TrackName ?? string.Empty,
				LimitText = row.RunOutLimit is int rl ? rl.ToString() : string.Empty,
				IsPass = row.IsPass,
				IsCurrent = isCurrent,
				IsAlternateRow = isAlt,
				HasNote = hasNote,
				NoteText = row.Remarks ?? string.Empty,
				IsNoteOpen = isNoteOpen,
				Marker = marker,
				HasMemo = hasMemo,
				IsSectionBreakRow = false,
				SectionBreakLabel = string.Empty,
				TabletRowPadding = tabletPad,
				CompactRowPadding = compactPad,
				TabletStationFontSize = tabletStation,
				CompactStationFontSize = compactStation,
			};
			ApplyDerivedStyling(item);
			Items.Add(item);

			visibleSeq++;
			prev = row;
		}
	}

	static string FormatRunMinutes(int? mm, int? ss)
	{
		if (mm is null && ss is null)
			return string.Empty;
		int total = (mm ?? 0);
		// Show whole minutes only — V1 column is the leftmost "分" indicator.
		// Round up if there are residual seconds so the row hints "≥ n min".
		if ((ss ?? 0) > 0)
			total += 1;
		return total > 0 ? total.ToString() : string.Empty;
	}

	static string FormatHhMm(TimeData? t)
	{
		if (t is null)
			return string.Empty;
		if (t.Hour is int h && t.Minute is int m)
			return $"{h:D2}:{m:D2}";
		return t.Text ?? string.Empty;
	}

	static void ApplyDerivedStyling(V1RowItem item)
	{
		if (item.IsSectionBreakRow)
			return;

		item.HasMarker = item.Marker != MarkerKind.None;
		item.IsMarkerFlag = item.Marker == MarkerKind.Flag;
		item.IsMarkerCaution = item.Marker == MarkerKind.Caution;
		item.IsMarkerStar = item.Marker == MarkerKind.Star;
		item.MarkerText = item.Marker switch
		{
			MarkerKind.Flag => "◆",
			MarkerKind.Caution => "!",
			MarkerKind.Star => "★",
			_ => string.Empty,
		};
	}

	// ── Public entry points for V1Row* (Parent-walked invocations) ─────────
	//
	// V1RowTablet / V1RowCompact don't bind their SwipeItem.Command through
	// the row's BindingContext (the BindingContext is the per-row V1RowItem,
	// not the page). Instead, each row walks up its Parent chain to find this
	// page on first interaction and calls one of these methods directly.

	public void OpenMarkerPopoverFromSwipe(string? rowId)
		=> OnOpenMarkerPopoverFromSwipe(rowId);

	public void ClearMarkerFromRow(string? rowId)
		=> OnClearMarker(rowId);

	public void OpenMemoFromRow(string? rowId)
		=> OnOpenMemo(rowId);

	public void ToggleNoteForRow(string? rowId)
		=> OnToggleNote(rowId);

	// Badge tap (anchored popover). The View is the badge Border that owns
	// the tap gesture in V1Row*; we use it as the popover anchor so the
	// floating UI positions next to the visible marker.
	public void OpenMarkerPopoverFromBadge(View? anchor, string? rowId)
	{
		if (anchor is null || string.IsNullOrEmpty(rowId))
			return;
		OpenMarkerPopover(anchor, rowId);
	}

	void OnCycleMarker(string? rowId)
	{
		var train = _vm.ActiveTrain;
		if (train is null || string.IsNullOrEmpty(rowId))
			return;
		_vm.CycleMarker(train.Id, rowId);
	}

	void OnClearMarker(string? rowId)
	{
		var train = _vm.ActiveTrain;
		if (train is null || string.IsNullOrEmpty(rowId))
			return;
		_vm.ClearMarker(train.Id, rowId);
	}

	void OnOpenMemo(string? rowId)
	{
		if (string.IsNullOrEmpty(rowId))
			return;
		OpenMemoSheet(rowId);
	}

	void OnToggleNote(string? rowId)
	{
		var train = _vm.ActiveTrain;
		if (train is null || string.IsNullOrEmpty(rowId))
			return;
		_vm.ToggleNote(train.Id, rowId);
	}

	// MarkerPopover wiring -------------------------------------------------

	// SwipeItem "マーカー" entry point. The SwipeItem doesn't yield a View we
	// can anchor the popover to (it's a MenuItem under the hood), so we
	// anchor against RootGrid — AnchorPopover falls back to a centered
	// positioning, matching what users expect when they invoke from a swipe.
	void OnOpenMarkerPopoverFromSwipe(string? rowId)
	{
		if (string.IsNullOrEmpty(rowId))
			return;
		OpenMarkerPopover(RootGrid, rowId);
	}

	// (OnMarkerBadgeTapped removed — V1RowTablet / V1RowCompact wire the badge
	//  tap directly to OpenMarkerPopoverFromBadge() via Parent-walk.)

	async void OpenMarkerPopover(View anchor, string rowId)
	{
		var train = _vm.ActiveTrain;
		if (train is null || string.IsNullOrEmpty(rowId))
			return;

		try
		{
			var popover = AnchorPopover.Create();
			var current = _vm.GetMarker(train.Id, rowId);
			var content = new MarkerPopoverContent();
			content.Configure(popover, current, kind =>
			{
				_vm.SetMarker(train.Id, rowId, kind);
			});

			var options = new PopoverOptions
			{
				PreferredWidth = 240,
				PreferredHeight = 140,
				DismissOnTapOutside = true,
			};
			await popover.ShowAsync(content, anchor, options);
		}
		catch
		{
			// Swallow — popover failures shouldn't crash the page. The user
			// can still cycle/clear via the SwipeView's other items.
		}
	}

	// MemoSheet wiring -----------------------------------------------------

	void OpenMemoSheet(string rowId)
	{
		var train = _vm.ActiveTrain;
		if (train is null)
			return;
		_memoRowId = rowId;
		MemoEditor.Text = _vm.GetMemo(train.Id, rowId);
		MemoSheetOverlay.IsVisible = true;
	}

	void CloseMemoSheet()
	{
		MemoSheetOverlay.IsVisible = false;
		_memoRowId = null;
	}

	void OnMemoSaveClicked(object? sender, EventArgs e)
	{
		var train = _vm.ActiveTrain;
		if (train is null || _memoRowId is null)
		{
			CloseMemoSheet();
			return;
		}
		_vm.SetMemo(train.Id, _memoRowId, MemoEditor.Text);
		CloseMemoSheet();
	}

	void OnMemoDeleteClicked(object? sender, EventArgs e)
	{
		var train = _vm.ActiveTrain;
		if (train is null || _memoRowId is null)
		{
			CloseMemoSheet();
			return;
		}
		_vm.SetMemo(train.Id, _memoRowId, null);
		CloseMemoSheet();
	}

	void OnMemoCancelClicked(object? sender, EventArgs e) => CloseMemoSheet();

	void OnMemoSheetScrimTapped(object? sender, TappedEventArgs e) => CloseMemoSheet();

	// Sheet-body taps shouldn't dismiss — swallow the bubbling tap so the
	// scrim's TapGestureRecognizer doesn't fire when the user taps the
	// Editor / Buttons area.
	void OnMemoSheetBodyTapped(object? sender, TappedEventArgs e)
	{
		// Intentionally empty — handler presence stops the gesture bubbling.
	}

	// Tweaks panel wiring (Phase 3) ---------------------------------------

	void OnTweaksButtonTapped(object? sender, TappedEventArgs e)
	{
		TweaksOverlay.IsVisible = true;
		UpdateDensityHighlight();
	}

	void OnTweaksScrimTapped(object? sender, TappedEventArgs e)
		=> TweaksOverlay.IsVisible = false;

	// Same swallow-the-tap pattern as the memo sheet — keeps the scrim's
	// TapGestureRecognizer from dismissing when the user taps the panel body.
	void OnTweaksBodyTapped(object? sender, TappedEventArgs e)
	{
		// Intentionally empty — handler presence stops the gesture bubbling.
	}

	void OnDensityCompactTapped(object? sender, TappedEventArgs e)
	{
		_vm.Density = Density.Compact;
		UpdateDensityHighlight();
	}

	void OnDensityComfortableTapped(object? sender, TappedEventArgs e)
	{
		_vm.Density = Density.Comfortable;
		UpdateDensityHighlight();
	}

	void OnDensitySpaciousTapped(object? sender, TappedEventArgs e)
	{
		_vm.Density = Density.Spacious;
		UpdateDensityHighlight();
	}

	// Highlights the selected Density button with OT_Accent; unselected stay
	// on OT_BgSoft. The list rows now observe vm.Density via
	// OnVmPropertyChanged → UpdateDensityInPlace, which mutates each row's
	// TabletRowPadding / CompactRowPadding / *StationFontSize props so the
	// CollectionView reflows without an Items.Clear+Add (scroll preserved).
	void UpdateDensityHighlight()
	{
		var accent = (Brush?)Application.Current?.Resources["OT_Accent"];
		var soft = (Brush?)Application.Current?.Resources["OT_BgSoft"];

		var d = _vm.Density;
		DensityCompact.Background = d == Density.Compact ? accent : soft;
		DensityComfortable.Background = d == Density.Comfortable ? accent : soft;
		DensitySpacious.Background = d == Density.Spacious ? accent : soft;

		// Use the accent foreground color on the selected label for contrast.
		var accentFg = Application.Current?.Resources["OT_AccentFg_Light"] as Color;
		var fg = Application.Current?.Resources["OT_Fg_Light"] as Color;
		DensityCompactLabel.TextColor = (d == Density.Compact ? accentFg : fg) ?? Colors.Black;
		DensityComfortableLabel.TextColor = (d == Density.Comfortable ? accentFg : fg) ?? Colors.Black;
		DensitySpaciousLabel.TextColor = (d == Density.Spacious ? accentFg : fg) ?? Colors.Black;
	}
}

// Row VM consumed by the CollectionView's DataTemplate. ObservableObject so
// MarkersVersion / MemosVersion / NoteOpenVersion / CurIdxVersion / Density
// changes can mutate visible props in place (preserving CollectionView
// scroll position — Items is never Cleared for those updates). Identity
// props (Id, OrigIndex, station/time text) stay plain — those only ever
// change during full RebuildItems().
public partial class V1RowItem : ObservableObject
{
	public string Id { get; set; } = string.Empty;
	// Index in ActiveTrain.Rows (the unfiltered list). -1 for section breaks.
	// Used by AutoFollowScroll to locate the current-station item.
	public int OrigIndex { get; set; } = -1;
	public string StationName { get; set; } = string.Empty;
	public string KanaName { get; set; } = string.Empty;
	public string RunText { get; set; } = string.Empty;
	public string ArriveText { get; set; } = string.Empty;
	public string DepartText { get; set; } = string.Empty;
	public string TrackName { get; set; } = string.Empty;
	public string LimitText { get; set; } = string.Empty;
	public bool IsPass { get; set; }

	// Mutated in-place by Update*InPlace; must raise INPC so DataTriggers re-fire.
	[ObservableProperty]
	public partial bool IsCurrent { get; set; }

	public bool IsAlternateRow { get; set; }
	public bool HasNote { get; set; }
	// Inline note body fields (Phase 2). NoteText is row.Remarks verbatim
	// (rendered by HtmlAutoDetectLabel — supports BBCode + HTML); IsNoteOpen
	// is sourced from OriginalTimetableViewModel.IsNoteOpen and drives the
	// inline Border's IsVisible inside the row template.
	public string NoteText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial bool IsNoteOpen { get; set; }

	[ObservableProperty]
	public partial MarkerKind Marker { get; set; } = MarkerKind.None;

	[ObservableProperty]
	public partial bool HasMemo { get; set; }

	public bool IsSectionBreakRow { get; set; }
	public string SectionBreakLabel { get; set; } = string.Empty;

	// Derived (filled by ApplyDerivedStyling). Bools drive DataTriggers in XAML
	// so per-row Brush selection stays AppTheme-aware via StaticResource lookups.
	[ObservableProperty]
	public partial bool HasMarker { get; set; }
	[ObservableProperty]
	public partial bool IsMarkerFlag { get; set; }
	[ObservableProperty]
	public partial bool IsMarkerCaution { get; set; }
	[ObservableProperty]
	public partial bool IsMarkerStar { get; set; }
	[ObservableProperty]
	public partial string MarkerText { get; set; } = string.Empty;

	// Density-driven row metrics. Tablet and Compact share the same Items
	// collection, so we keep both. Updated in-place by UpdateDensityInPlace.
	[ObservableProperty]
	public partial Thickness TabletRowPadding { get; set; } = new Thickness(12, 8);
	[ObservableProperty]
	public partial Thickness CompactRowPadding { get; set; } = new Thickness(10, 7);
	[ObservableProperty]
	public partial double TabletStationFontSize { get; set; } = 32;
	[ObservableProperty]
	public partial double CompactStationFontSize { get; set; } = 20;

	public bool IsNormalRow => !IsSectionBreakRow;
	public bool HasTrackName => !string.IsNullOrEmpty(TrackName);
	public bool HasRunText => !string.IsNullOrEmpty(RunText);
	public bool HasLimitText => !string.IsNullOrEmpty(LimitText);

	// AutomationId helpers — generated from Id so each row has stable, distinct
	// accessibility identifiers for E2E tests. The SwipeView, each SwipeItem,
	// and the marker badge Border are all addressable by row.
	const string AutomationIdPrefix = "OriginalTimetable.V1.Row.";
	public string RowAutomationId => $"{AutomationIdPrefix}{Id}";
	public string MarkerAutomationId => $"{AutomationIdPrefix}{Id}.Marker";
	public string MemoAutomationId => $"{AutomationIdPrefix}{Id}.Memo";
	public string ClearAutomationId => $"{AutomationIdPrefix}{Id}.Clear";
	public string MarkerBadgeAutomationId => $"{AutomationIdPrefix}{Id}.MarkerBadge";
	public string NoteBodyAutomationId => $"{AutomationIdPrefix}{Id}.NoteBody";

	public static V1RowItem SectionBreak(string id, string label) => new()
	{
		Id = id,
		IsSectionBreakRow = true,
		SectionBreakLabel = label,
	};
}
