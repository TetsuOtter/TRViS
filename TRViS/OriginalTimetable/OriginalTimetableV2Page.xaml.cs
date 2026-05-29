using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;

using TR.Maui.AnchorPopover;

using TRViS.IO.Models;
using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS.OriginalTimetable;

// V2 Card Stack — 独自時刻表ページ骨格 (Phase 1 + Phase 2 + Phase 3).
//
// Phase 1: card visual (rounded outer Border, concentric inner platform tile,
// accent-soft background + 1.5px accent border for current, vertically-stacked
// 着/発 with mono numerals, IsCurrent-flipped metrics), tablet + compact split,
// real-data row mapping, sticky header, section-break rows, marker badges,
// memo dots, SwipeItem CycleMarker/ClearMarker commands.
//
// Phase 2: MarkerPopover anchored to badge / opened from swipe (reuses
// MarkerPopoverContent verbatim), MemoSheet overlay (bottom-sheet), inline
// NoteFold (Border placed *inside* the card outer Border so concentric radii
// are preserved — V2 difference from V1, which puts the note as a sibling
// underneath the row Border).
//
// Phase 3: Tweaks panel (gear icon → overlay) with ShowPasses Switch + Density
// tri-state (狭/標準/広); Density-driven scaling applied to every per-card
// metric (platform size + font, time font, station font) via
// ApplyCurrentAndDensityScaledMetrics, plus the card outer Margin
// (TabletCardMargin / CompactCardMargin) — so density visibly reflows the card
// stack without an Items.Clear+Add (scroll preserved); auto-follow to the
// current card on CurIdxVersion changes when vm.Follow.
//
// History/workaround: an earlier shape used CollectionView + DataTemplate
// (HtmlAutoDetectLabel inside the template). On iOS simulators this trips
// Apple's ObservationTracking._AccessList / _NativeDictionary.copy use-after-
// free during cell recycling (swiftlang#84228). Refactored to mirror the V1
// fix (commit 682e2d4): BindableLayout-on-VSL inside a ScrollView, with
// V2RowTablet / V2RowCompact (programmatic ContentView subclasses in V2Row.cs)
// instantiated per item. No UICollectionView / SwiftUI ViewGraph involved.
public partial class OriginalTimetableV2Page : ContentPage
{
	public static readonly string NameOfThisClass = nameof(OriginalTimetableV2Page);

	// Tablet >=600pt (Material breakpoint; iPad mini portrait 744pt counts).
	const double TabletBreakpoint = 600;

	readonly OriginalTimetableViewModel _vm;
	double _lastWidth = -1;
	bool _lastIsTablet;

	public ObservableCollection<V2RowItem> Items { get; } = new();

	// Train-header bindings (refreshed by RebuildItems). Mirrors design v2.jsx:
	// brand row (depot · route / pager / workDate) + summary (chip / number+meta / clock).
	public string BrandText { get; private set; } = string.Empty;
	public IReadOnlyList<bool> PagerDots { get; private set; } = [];
	public bool HasMultipleTrains { get; private set; }
	public string WorkDateText { get; private set; } = string.Empty;
	public string HeaderTypeText { get; private set; } = string.Empty;
	public bool HasHeaderType { get; private set; }
	public string HeaderTrainNumberText { get; private set; } = string.Empty;
	public string SummaryMetaText { get; private set; } = string.Empty;
	public bool HasActiveTrain { get; private set; }
	public bool HasNoActiveTrain => !HasActiveTrain;

	// Live wall-clock (現在時刻), driven by _clockTimer off InstanceManager.TimeProvider.
	public string CurrentTimeText { get; private set; } = string.Empty;
	IDispatcherTimer? _clockTimer;

	// SwipeItem / inline commands invoked from inside the DataTemplate.
	public ICommand CycleMarkerCommand { get; }
	public ICommand ClearMarkerCommand { get; }
	public ICommand OpenMemoCommand { get; }
	public ICommand ToggleNoteCommand { get; }
	public ICommand OpenMarkerPopoverFromSwipeCommand { get; }

	// Sheet-edit state. _memoRowId is set when MemoSheetOverlay opens;
	// cleared on save/cancel/delete. Kept private and synchronous.
	string? _memoRowId;

	public OriginalTimetableV2Page()
	{
		_vm = InstanceManager.OriginalTimetableViewModel;

		CycleMarkerCommand = new Command<string>(OnCycleMarker);
		ClearMarkerCommand = new Command<string>(OnClearMarker);
		OpenMemoCommand = new Command<string>(OnOpenMemo);
		ToggleNoteCommand = new Command<string>(OnToggleNote);
		OpenMarkerPopoverFromSwipeCommand = new Command<string>(OnOpenMarkerPopoverFromSwipe);

		InitializeComponent();
		BindingContext = _vm;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		InstanceManager.OrientationService.SetOrientation(AppDisplayOrientation.Portrait);
		_vm.PropertyChanged += OnVmPropertyChanged;
		ApplyLayoutForWidth(Width);
		RebuildItems();
		UpdateDensityHighlight();
		StartClock();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		InstanceManager.OrientationService.SetOrientation(AppDisplayOrientation.All);
		_vm.PropertyChanged -= OnVmPropertyChanged;
		StopClock();
	}

	// ── Live wall-clock (現在時刻) ───────────────────────────────────────────
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

	// ── Flyout toggle + train pager (‹ idx/count ›) ─────────────────────────
	void OnFlyoutToggleTapped(object? sender, TappedEventArgs e)
	{
		if (Shell.Current is not null)
			Shell.Current.FlyoutIsPresented = !Shell.Current.FlyoutIsPresented;
	}

	void OnPrevTrainTapped(object? sender, TappedEventArgs e) => _vm.PrevTrain();
	void OnNextTrainTapped(object? sender, TappedEventArgs e) => _vm.NextTrain();

	// Summary meta line: "{origin} → {dest} · {cars}両 · 最高 {maxSpeed}km/h".
	static string BuildSummaryMetaText(TrainData? train)
	{
		if (train is null)
			return string.Empty;
		var parts = new List<string>(3);
		string od = BuildOriginDestText(train);
		if (od.Length > 0) parts.Add(od);
		if (train.CarCount is int cc) parts.Add($"{cc}両");
		if (FirstLine(train.MaxSpeed) is { Length: > 0 } ms) parts.Add($"最高 {ms}km/h");
		return string.Join(" · ", parts);
	}

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

	// origin/dest = first & last non-info-row station names (mirrors V1).
	static string BuildOriginDestText(TrainData? train)
	{
		if (train?.Rows is null || train.Rows.Length == 0)
			return string.Empty;
		string origin = string.Empty, dest = string.Empty;
		foreach (var r in train.Rows)
		{
			if (r.IsInfoRow || string.IsNullOrEmpty(r.StationName)) continue;
			origin = r.StationName!; break;
		}
		for (int i = train.Rows.Length - 1; i >= 0; i--)
		{
			var r = train.Rows[i];
			if (r.IsInfoRow || string.IsNullOrEmpty(r.StationName)) continue;
			dest = r.StationName!; break;
		}
		return (origin.Length == 0 && dest.Length == 0) ? string.Empty : $"{origin} → {dest}";
	}

	void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			// ActiveTrain / ShowPasses change the *visible row set*, so a
			// full Items rebuild is required (scroll reset acceptable).
			case nameof(OriginalTimetableViewModel.ActiveTrain):
			case nameof(OriginalTimetableViewModel.ShowPasses):
				RebuildItems();
				break;
			// Partial in-place updates — V2RowItem is an ObservableObject so
			// mutating props refreshes bound visuals without touching Items,
			// preserving CollectionView scroll position.
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
				UpdateDensityHighlight();
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
			bool nowCurrent = item.OrigIndex == curOrigIdx;
			if (item.IsCurrent != nowCurrent)
			{
				item.IsCurrent = nowCurrent;
				ApplyCurrentAndDensityScaledMetrics(item, _vm.Density);
			}
		}
	}

	void UpdateDensityInPlace()
	{
		var d = _vm.Density;
		foreach (var item in Items)
		{
			if (item.IsSectionBreakRow)
				continue;
			ApplyCurrentAndDensityScaledMetrics(item, d);
		}
	}

	// state.jsx densityScale: compact=0.82, comfortable=1.0, spacious=1.12.
	static double DensityScale(Density d) => d switch
	{
		Density.Compact => 0.82,
		Density.Spacious => 1.12,
		_ => 1.0,
	};

	// Card outer Margin per density. V2 cards are floating tiles, so margin
	// is the breathing-room equivalent (vs V1's in-row padding).
	static (Thickness tablet, Thickness compact) DensityMargins(Density d) => d switch
	{
		Density.Compact => (new Thickness(4, 2), new Thickness(4, 2)),
		Density.Spacious => (new Thickness(12, 6), new Thickness(10, 5)),
		_ => (new Thickness(8, 4), new Thickness(6, 3)),
	};

	// Combined "current emphasis" + density scaling. Phase 1 only had IsCurrent
	// flip; Phase 3 multiplies every base value by the density scale and also
	// writes the card outer Margin (which V1 doesn't have because its cards
	// aren't floating tiles).
	static void ApplyCurrentAndDensityScaledMetrics(V2RowItem item, Density density)
	{
		double s = DensityScale(density);
		double R(double v) => Math.Round(v * s, 1);
		double RI(double v) => Math.Round(v * s);

		if (item.IsCurrent)
		{
			item.TabletPlatformSize = RI(70);
			item.TabletPlatformFontSize = R(40);
			item.TabletTimeFontSize = R(40);
			item.TabletStationFontSize = R(40);
			item.CompactPlatformSize = RI(52);
			item.CompactPlatformFontSize = R(26);
			item.CompactTimeFontSize = R(22);
			item.CompactStationFontSize = R(24);
		}
		else
		{
			item.TabletPlatformSize = RI(56);
			item.TabletPlatformFontSize = R(28);
			item.TabletTimeFontSize = R(24);
			item.TabletStationFontSize = R(30);
			item.CompactPlatformSize = RI(44);
			item.CompactPlatformFontSize = R(20);
			item.CompactTimeFontSize = R(16);
			item.CompactStationFontSize = R(20);
		}

		var (tabletMargin, compactMargin) = DensityMargins(density);
		item.TabletCardMargin = tabletMargin;
		item.CompactCardMargin = compactMargin;
	}

	// Auto-follow: when Follow=true, scroll the visible row host to the
	// current-station card (center). Called after the CurIdxVersion in-place
	// pass has flipped IsCurrent on each row.
	//
	// Post-refactor: rows live inside a BindableLayout-driven VerticalStackLayout
	// (TabletRowsHost / CompactRowsHost) wrapped in a ScrollView. BindableLayout
	// preserves Items order so the child at index i corresponds to Items[i].
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

		// Both child Grids are declared in XAML — only flip IsVisible to
		// avoid the imperative-tree-mutation NRE path on iPad.
		TabletGrid.IsVisible = isTablet;
		CompactGrid.IsVisible = !isTablet;
	}

	void RebuildItems()
	{
		var train = _vm.ActiveTrain;
		HasActiveTrain = train is not null;
		string depot = InstanceManager.AppViewModel.SelectedWorkGroup?.Name ?? string.Empty;
		string route = train?.WorkName ?? string.Empty;
		BrandText = (depot.Length, route.Length) switch
		{
			(0, 0) => string.Empty,
			(0, _) => route,
			(_, 0) => depot,
			_ => $"{depot} · {route}",
		};
		var trainList = InstanceManager.AppViewModel.OrderedTrainDataList;
		int trainCount = trainList?.Count ?? 0;
		HasMultipleTrains = trainCount > 1;
		PagerDots = BuildPagerDots(trainCount, _vm.ActiveTrainIdx);
		WorkDateText = train?.AffectDate is DateOnly date ? date.ToString("yyyy/MM/dd") : string.Empty;
		HeaderTypeText = FirstLine(train?.SpeedType);
		HasHeaderType = !string.IsNullOrEmpty(HeaderTypeText);
		HeaderTrainNumberText = train?.TrainNumber ?? string.Empty;
		SummaryMetaText = BuildSummaryMetaText(train);
		OnPropertyChanged(nameof(HasActiveTrain));
		OnPropertyChanged(nameof(HasNoActiveTrain));
		OnPropertyChanged(nameof(BrandText));
		OnPropertyChanged(nameof(PagerDots));
		OnPropertyChanged(nameof(HasMultipleTrains));
		OnPropertyChanged(nameof(WorkDateText));
		OnPropertyChanged(nameof(HeaderTypeText));
		OnPropertyChanged(nameof(HasHeaderType));
		OnPropertyChanged(nameof(HeaderTrainNumberText));
		OnPropertyChanged(nameof(SummaryMetaText));

		Items.Clear();
		if (train is null || train.Rows is null || train.Rows.Length == 0)
			return;

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
		var density = _vm.Density;

		TimetableRow? prev = null;
		foreach (var (origIdx, row) in visibleRows)
		{
			if (prev is not null && prev.RunOutLimit != row.RunInLimit)
			{
				var newLimit = row.RunInLimit;
				var label = newLimit is int v
					? $"▼ 区間切替 — 最高 {v}km/h"
					: "▼ 区間切替";
				Items.Add(V2RowItem.SectionBreak(id: $"sb:{row.Id}", label: label));
			}

			bool isCurrent = origIdx == curOrigIdx;
			var marker = _vm.GetMarker(train.Id, row.Id);
			bool hasMemo = !string.IsNullOrWhiteSpace(_vm.GetMemo(train.Id, row.Id));
			bool hasNote = !string.IsNullOrWhiteSpace(row.Remarks);
			bool isNoteOpen = hasNote && _vm.IsNoteOpen(train.Id, row.Id);

			var item = new V2RowItem
			{
				Id = row.Id,
				OrigIndex = origIdx,
				StationName = row.StationName ?? string.Empty,
				RunText = FormatRunMinutes(row.DriveTimeMM, row.DriveTimeSS),
				ArriveText = FormatHhMm(row.ArriveTime),
				DepartText = FormatHhMm(row.DepartureTime),
				TrackName = row.TrackName ?? string.Empty,
				IsPass = row.IsPass,
				IsCurrent = isCurrent,
				HasNote = hasNote,
				IsNoteOpen = isNoteOpen,
				NoteText = row.Remarks ?? string.Empty,
				Marker = marker,
				HasMemo = hasMemo,
				IsSectionBreakRow = false,
				SectionBreakLabel = string.Empty,
			};
			ApplyDerivedStyling(item);
			ApplyCurrentAndDensityScaledMetrics(item, density);
			Items.Add(item);

			prev = row;
		}
	}

	static string FormatRunMinutes(int? mm, int? ss)
	{
		if (mm is null && ss is null)
			return string.Empty;
		int total = (mm ?? 0);
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

	static void ApplyDerivedStyling(V2RowItem item)
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

	// ── Public entry points for V2Row* (Parent-walked invocations) ─────────
	//
	// V2RowTablet / V2RowCompact don't bind their SwipeItem.Command through
	// the row's BindingContext (the BindingContext is the per-row V2RowItem,
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
	// the tap gesture in V2Row*; we use it as the popover anchor so the
	// floating UI positions next to the visible marker.
	public void OpenMarkerPopoverFromBadge(View? anchor, string? rowId)
	{
		if (anchor is null || string.IsNullOrEmpty(rowId))
			return;
		OpenMarkerPopover(anchor, rowId);
	}

	// MarkerPopover wiring -------------------------------------------------

	// SwipeItem entry — SwipeItem isn't an anchor-eligible View, so we
	// anchor against RootGrid (AnchorPopover falls back to centered).
	void OnOpenMarkerPopoverFromSwipe(string? rowId)
	{
		if (string.IsNullOrEmpty(rowId))
			return;
		OpenMarkerPopover(RootGrid, rowId);
	}

	// (OnMarkerBadgeTapped removed — V2RowTablet / V2RowCompact wire the badge
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
			// Swallow — popover failures shouldn't crash the page.
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

	void OnTweaksBodyTapped(object? sender, TappedEventArgs e)
	{
		// Intentionally empty — handler presence stops the gesture bubbling.
	}

	void OnDensityCompactTapped(object? sender, TappedEventArgs e)
	{
		_vm.Density = Density.Compact;
	}

	void OnDensityComfortableTapped(object? sender, TappedEventArgs e)
	{
		_vm.Density = Density.Comfortable;
	}

	void OnDensitySpaciousTapped(object? sender, TappedEventArgs e)
	{
		_vm.Density = Density.Spacious;
	}

	void UpdateDensityHighlight()
	{
		var accent = (Brush?)Application.Current?.Resources["OT_Accent"];
		var soft = (Brush?)Application.Current?.Resources["OT_BgSoft"];

		var d = _vm.Density;
		DensityCompact.Background = d == Density.Compact ? accent : soft;
		DensityComfortable.Background = d == Density.Comfortable ? accent : soft;
		DensitySpacious.Background = d == Density.Spacious ? accent : soft;

		// テーマに応じて *_Light / *_Dark の対を切り替える。
		// 以前は常に *_Light を読んでいたためダークテーマでコントラストが崩れていた
		// (#286 Copilot review)。
		bool isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
		string accentFgKey = isDark ? "OT_AccentFg_Dark" : "OT_AccentFg_Light";
		string fgKey = isDark ? "OT_Fg_Dark" : "OT_Fg_Light";
		var accentFg = Application.Current?.Resources[accentFgKey] as Color;
		var fg = Application.Current?.Resources[fgKey] as Color;
		DensityCompactLabel.TextColor = (d == Density.Compact ? accentFg : fg) ?? Colors.Black;
		DensityComfortableLabel.TextColor = (d == Density.Comfortable ? accentFg : fg) ?? Colors.Black;
		DensitySpaciousLabel.TextColor = (d == Density.Spacious ? accentFg : fg) ?? Colors.Black;
	}
}

// V2 card-stack row VM consumed by the CollectionView's DataTemplate.
// ObservableObject so MarkersVersion / MemosVersion / NoteOpenVersion /
// CurIdxVersion / Density can mutate visible props in place (preserves
// CollectionView scroll position).
//
// V2 differs from V1RowItem: no alternating-row striping, no LimitText, and
// *PlatformSize / *TimeFontSize / *StationFontSize / *CardMargin are
// observable so the IsCurrent flip + density scale rescale every per-card
// metric without rebuilding Items. Inline NoteFold lives *inside* the card
// outer Border to preserve concentric radii (V1 stacks it as a sibling).
public partial class V2RowItem : ObservableObject
{
	public string Id { get; set; } = string.Empty;
	// Index in ActiveTrain.Rows (the unfiltered list). -1 for section breaks.
	// Used by AutoFollowScroll to locate the current-station item.
	public int OrigIndex { get; set; } = -1;
	public string StationName { get; set; } = string.Empty;
	public string RunText { get; set; } = string.Empty;
	public string ArriveText { get; set; } = string.Empty;
	public string DepartText { get; set; } = string.Empty;
	public string TrackName { get; set; } = string.Empty;
	public bool IsPass { get; set; }

	[ObservableProperty]
	public partial bool IsCurrent { get; set; }

	public bool HasNote { get; set; }
	public string NoteText { get; set; } = string.Empty;

	// Mutated in-place by UpdateNoteOpenInPlace; must raise INPC so the
	// inline NoteFold Border's IsVisible binding refreshes.
	[ObservableProperty]
	public partial bool IsNoteOpen { get; set; }

	[ObservableProperty]
	public partial MarkerKind Marker { get; set; } = MarkerKind.None;

	[ObservableProperty]
	public partial bool HasMemo { get; set; }

	public bool IsSectionBreakRow { get; set; }
	public string SectionBreakLabel { get; set; } = string.Empty;

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

	// Per-card emphasis metrics. ApplyCurrentAndDensityScaledMetrics rewrites
	// these whenever IsCurrent flips OR vm.Density changes — Density scales
	// each base value (compact 0.82 / comfortable 1.0 / spacious 1.12).
	[ObservableProperty]
	public partial double TabletPlatformSize { get; set; } = 56;
	[ObservableProperty]
	public partial double TabletPlatformFontSize { get; set; } = 22;
	[ObservableProperty]
	public partial double TabletTimeFontSize { get; set; } = 22;
	[ObservableProperty]
	public partial double TabletStationFontSize { get; set; } = 22;
	[ObservableProperty]
	public partial double CompactPlatformSize { get; set; } = 44;
	[ObservableProperty]
	public partial double CompactPlatformFontSize { get; set; } = 16;
	[ObservableProperty]
	public partial double CompactTimeFontSize { get; set; } = 14;
	[ObservableProperty]
	public partial double CompactStationFontSize { get; set; } = 16;

	// Card outer Margin — density's breathing-room equivalent (V2 cards are
	// floating tiles, so margin between cards is what reflows for density;
	// V1 has no equivalent because its rows are flush-edge in a list).
	[ObservableProperty]
	public partial Thickness TabletCardMargin { get; set; } = new Thickness(8, 4);
	[ObservableProperty]
	public partial Thickness CompactCardMargin { get; set; } = new Thickness(6, 3);

	public bool IsNormalRow => !IsSectionBreakRow;
	public bool HasTrackName => !string.IsNullOrEmpty(TrackName);

	const string AutomationIdPrefix = "OriginalTimetable.V2.Row.";
	public string RowAutomationId => $"{AutomationIdPrefix}{Id}";
	public string MarkerAutomationId => $"{AutomationIdPrefix}{Id}.Marker";
	public string MemoAutomationId => $"{AutomationIdPrefix}{Id}.Memo";
	public string ClearAutomationId => $"{AutomationIdPrefix}{Id}.Clear";
	public string MarkerBadgeAutomationId => $"{AutomationIdPrefix}{Id}.MarkerBadge";
	public string NoteBodyAutomationId => $"{AutomationIdPrefix}{Id}.NoteBody";

	public static V2RowItem SectionBreak(string id, string label) => new()
	{
		Id = id,
		IsSectionBreakRow = true,
		SectionBreakLabel = label,
	};
}
