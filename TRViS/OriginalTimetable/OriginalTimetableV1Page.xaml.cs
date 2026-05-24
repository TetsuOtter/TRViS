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
// Probe (37e5e44) verified that a CollectionView + DataTemplate + SwipeView
// renders without the ApplyStyleSheets NRE that crashed our prior custom
// V1TabletLayout on iPad mini A17. This page now feeds the same render path
// from real TrainData via OriginalTimetableViewModel.ActiveTrain.
//
// Phase 1 covers: real-data row mapping, sticky header, section-break rows
// (RunOutLimit ↔ RunInLimit transition), marker badges, memo dots, note
// toggle button, SwipeItem CycleMarker/ClearMarker commands, and width-based
// tablet/compact split. MarkerPopover / MemoSheet / NoteFold body / phone
// compact UI / tweaks panel / E2E test are Phase 2/3.
public partial class OriginalTimetableV1Page : ContentPage
{
	public static readonly string NameOfThisClass = nameof(OriginalTimetableV1Page);

	// Tablet ≥600pt (Material breakpoint; iPad mini portrait 744pt counts).
	const double TabletBreakpoint = 600;

	readonly OriginalTimetableViewModel _vm;
	double _lastWidth = -1;
	bool _lastIsTablet;

	public ObservableCollection<V1RowItem> Items { get; } = new();

	// Train-header bindings (refreshed by RebuildItems).
	public string HeaderTypeText { get; private set; } = string.Empty;
	public bool HasHeaderType { get; private set; }
	public string HeaderTrainNumberText { get; private set; } = string.Empty;
	public string HeaderDestinationText { get; private set; } = string.Empty;
	public string HeaderCarCountText { get; private set; } = string.Empty;
	public string HeaderMaxSpeedText { get; private set; } = string.Empty;
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
		ApplyLayoutForWidth(Width);
		RebuildItems();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		InstanceManager.OrientationService.SetOrientation(AppDisplayOrientation.All);
		_vm.PropertyChanged -= OnVmPropertyChanged;
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
			// touching Items, which preserves CollectionView scroll position.
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
		double tabletStation = Math.Round(18 * scale, 1);
		double compactStation = Math.Round(14 * scale, 1);
		return (tabletPad, compactPad, tabletStation, compactStation);
	}

	// Auto-follow: when Follow=true, scroll the visible CollectionView to the
	// current-station row (center). Called after the CurIdxVersion in-place
	// pass has flipped IsCurrent on each row.
	void AutoFollowScroll()
	{
		if (!_vm.Follow)
			return;
		var train = _vm.ActiveTrain;
		if (train is null)
			return;
		int curOrigIdx = _vm.GetCurIdxOverride(train.Id) ?? 0;
		V1RowItem? curItem = null;
		foreach (var i in Items)
		{
			if (!i.IsSectionBreakRow && i.OrigIndex == curOrigIdx)
			{
				curItem = i;
				break;
			}
		}
		if (curItem is null)
			return;
		var cv = TabletGrid.IsVisible ? TabletRowsList : CompactRowsList;
		try
		{
			cv.ScrollTo(curItem, position: ScrollToPosition.Center, animate: true);
		}
		catch
		{
			// CollectionView.ScrollTo can throw on certain platforms when the
			// list hasn't measured yet — swallow rather than crash auto-follow.
		}
	}

	void OnRootSizeChanged(object? sender, EventArgs e) => ApplyLayoutForWidth(Width);

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
		HeaderTypeText = train?.SpeedType ?? string.Empty;
		HasHeaderType = !string.IsNullOrEmpty(HeaderTypeText);
		HeaderTrainNumberText = train?.TrainNumber ?? string.Empty;
		HeaderDestinationText = train?.Destination is { Length: > 0 } d ? $"行先 {d}" : string.Empty;
		HeaderCarCountText = train?.CarCount is int cc ? $"{cc}両" : string.Empty;
		HeaderMaxSpeedText = train?.MaxSpeed is { Length: > 0 } ms ? $"最高 {ms}km/h" : string.Empty;
		OnPropertyChanged(nameof(HasActiveTrain));
		OnPropertyChanged(nameof(HasNoActiveTrain));
		OnPropertyChanged(nameof(HeaderTypeText));
		OnPropertyChanged(nameof(HasHeaderType));
		OnPropertyChanged(nameof(HeaderTrainNumberText));
		OnPropertyChanged(nameof(HeaderDestinationText));
		OnPropertyChanged(nameof(HeaderCarCountText));
		OnPropertyChanged(nameof(HeaderMaxSpeedText));

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

	// Tap on the visible marker badge inside the row. Uses the badge Border
	// (sender) as the anchor so the popover positions next to the marker.
	void OnMarkerBadgeTapped(object? sender, TappedEventArgs e)
	{
		if (sender is not Border border)
			return;
		if (border.BindingContext is not V1RowItem item)
			return;
		OpenMarkerPopover(border, item.Id);
	}

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
	public partial double TabletStationFontSize { get; set; } = 18;
	[ObservableProperty]
	public partial double CompactStationFontSize { get; set; } = 14;

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
