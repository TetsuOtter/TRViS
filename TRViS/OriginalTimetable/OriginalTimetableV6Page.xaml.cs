using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;

using TR.Maui.AnchorPopover;

using TRViS.Controls;
using TRViS.IO.Models;
using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS.OriginalTimetable;

// V6 Bold Editorial — 独自時刻表ページ骨格 (Phase 1 + Phase 2 + Phase 3).
//
// Phase 1 (commit 3a3305e): tablet+compact split, 5 sections — Masthead /
// Train stripe / Past chips / Current big block / Upcoming list.
//
// Phase 2 (this commit):
//   - MarkerPopover anchored to badge (current big block + each upcoming row)
//     + SwipeItem「マーカー」 also goes through OpenMarkerPopoverFromSwipe
//     (was Phase 1).
//   - MemoSheet bottom-sheet overlay (sibling of TabletGrid/CompactGrid).
//   - Inline NoteFold inside each upcoming row + always-visible NoteBody
//     in the current big block (read-only Remarks display).
//   - Past chip Tapped → vm.SetCurIdx(train.Id, origIndex): jump to that
//     station (past chips become navigation, not just history).
//
// Phase 3 (this commit):
//   - Tweaks panel (gear icon in train stripe → overlay) with ShowPasses
//     Switch + Density tri-state (狭/標準/広).
//   - Density-driven scaling applied to all 5 sections (Masthead route +
//     Train stripe number + Past chip font + Current big block station/time
//     + Upcoming row station/time/padding) so density visibly reflows
//     without an Items.Clear+Add for marker/memo/note/density paths.
//
// V6 CurIdxVersion strategy: past/current/upcoming split shifts whenever
// CurIdx changes, so we do full RebuildItems (unlike V1/V2/V4 which can
// in-place update). Scroll resets to top — acceptable for V6 because the
// current station is shown in the big block at the top of the page.
public partial class OriginalTimetableV6Page : ContentPage
{
	public static readonly string NameOfThisClass = nameof(OriginalTimetableV6Page);

	const double TabletBreakpoint = 600;

	readonly OriginalTimetableViewModel _vm;
	double _lastWidth = -1;
	bool _lastIsTablet;

	public ObservableCollection<V6RowItem> Items { get; } = new();
	public ObservableCollection<V6PastChipItem> PastChips { get; } = new();

	// Masthead bindings.
	public string DepotName { get; private set; } = "乗務区";
	public string RouteNameText { get; private set; } = string.Empty;
	public string WorkDateText { get; private set; } = string.Empty;

	// Train stripe bindings.
	public string TrainNumberText { get; private set; } = string.Empty;
	public string CarCountText { get; private set; } = string.Empty;
	public string MaxSpeedText { get; private set; } = string.Empty;
	public string DestinationText { get; private set; } = string.Empty;

	// Current big block bindings.
	public string CurrentStationName { get; private set; } = string.Empty;
	public string CurrentStationArrive { get; private set; } = string.Empty;
	public string CurrentStationDepart { get; private set; } = string.Empty;
	public string CurrentStationTrack { get; private set; } = string.Empty;
	public bool HasCurrentStation { get; private set; }
	public bool HasCurrentStationTrack => !string.IsNullOrEmpty(CurrentStationTrack);

	// Current big block — marker (Phase 2). Tap badge → OpenMarkerPopover
	// anchored at the badge.
	public bool HasCurrentMarker { get; private set; }
	public bool IsCurrentMarkerFlag { get; private set; }
	public bool IsCurrentMarkerCaution { get; private set; }
	public bool IsCurrentMarkerStar { get; private set; }
	public string CurrentMarkerText { get; private set; } = string.Empty;

	// Current big block — note (Phase 2). Always visible read-only when
	// current station has Remarks; not toggle-able (the upcoming-list note
	// fold is for navigation, this is just the live "you are here" remarks).
	public bool HasCurrentNote { get; private set; }
	public string CurrentNoteText { get; private set; } = string.Empty;

	// Page-level row-id of the current station, used as CommandParameter for
	// the big-block SwipeItems (the current row isn't in Items, so V1's
	// per-item Id binding can't reach it).
	public string CurrentRowId { get; private set; } = string.Empty;

	public bool HasActiveTrain { get; private set; }
	public bool HasNoActiveTrain => !HasActiveTrain;
	public bool HasNoPastChips => PastChips.Count == 0;

	// Phase 3 — page-level density-scaled font sizes (tablet/compact pairs).
	// Bound from XAML via {Binding ... Source={x:Reference Self}}.
	public double TabletRouteFontSize { get; private set; } = 22;
	public double CompactRouteFontSize { get; private set; } = 17;
	public double TabletTrainNumberFontSize { get; private set; } = 28;
	public double CompactTrainNumberFontSize { get; private set; } = 22;
	public double TabletCurrentStationFontSize { get; private set; } = 48;
	public double CompactCurrentStationFontSize { get; private set; } = 34;
	public double TabletCurrentTimeFontSize { get; private set; } = 28;
	public double CompactCurrentTimeFontSize { get; private set; } = 22;
	public double TabletPastChipFontSize { get; private set; } = 11;
	public double CompactPastChipFontSize { get; private set; } = 10;

	public ICommand ClearMarkerCommand { get; }
	public ICommand OpenMemoCommand { get; }
	public ICommand ToggleNoteCommand { get; }
	public ICommand OpenMarkerPopoverFromSwipeCommand { get; }

	// MemoSheet edit state (Phase 2).
	string? _memoRowId;

	// Current NoteBody HAL is constructed lazily — see EnsureCurrentNoteLabel
	// and the matching XAML comments on CurrentNoteHost / CompactCurrentNoteHost.
	// Both stay null until the first time HasCurrentNote flips to true; that
	// defers HAL construction out of the XAML inflation pass
	// (swiftlang#84228 workaround).
	HtmlAutoDetectLabel? _currentNoteLabel;
	HtmlAutoDetectLabel? _compactCurrentNoteLabel;

	public OriginalTimetableV6Page()
	{
		_vm = InstanceManager.OriginalTimetableViewModel;

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
			case nameof(OriginalTimetableViewModel.ShowPasses):
			// V6 specific: CurIdx 変化で past/current/upcoming の境界が動くため
			// 部分更新 (V1 の UpdateCurrentInPlace) では足りず、丸ごと再構築する。
			// upcoming list の scroll は top に戻る — V6 は current 駅を上部 big
			// block に常に表示しているので OK。
			case nameof(OriginalTimetableViewModel.CurIdxVersion):
				RebuildItems();
				AutoFollowScroll();
				break;
			// 以下は upcoming 行の中だけで完結するので in-place で OK。
			case nameof(OriginalTimetableViewModel.MarkersVersion):
				UpdateMarkersInPlace();
				RecomputeCurrentMarker();
				break;
			case nameof(OriginalTimetableViewModel.MemosVersion):
				UpdateMemosInPlace();
				break;
			case nameof(OriginalTimetableViewModel.NoteOpenVersion):
				UpdateNoteOpenInPlace();
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
			item.Marker = _vm.GetMarker(train.Id, item.Id);
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

	// Phase 3 — Density in-place. Row metrics on every Items entry + page
	// fonts via ApplyPageDensityScale. Does NOT mutate Items so scroll
	// position is preserved.
	void UpdateDensityInPlace()
	{
		var d = _vm.Density;
		ApplyPageDensityScale(d);
		foreach (var item in Items)
		{
			if (item.IsSectionBreakRow)
				continue;
			ApplyDensityScaledMetrics(item, d);
		}
	}

	static double DensityScale(Density d) => d switch
	{
		Density.Compact => 0.82,
		Density.Spacious => 1.12,
		_ => 1.0,
	};

	void ApplyPageDensityScale(Density density)
	{
		double s = DensityScale(density);
		double R(double v) => Math.Round(v * s, 1);
		TabletRouteFontSize = R(22);
		CompactRouteFontSize = R(17);
		TabletTrainNumberFontSize = R(28);
		CompactTrainNumberFontSize = R(22);
		TabletCurrentStationFontSize = R(48);
		CompactCurrentStationFontSize = R(34);
		TabletCurrentTimeFontSize = R(28);
		CompactCurrentTimeFontSize = R(22);
		TabletPastChipFontSize = R(11);
		CompactPastChipFontSize = R(10);
		OnPropertyChanged(nameof(TabletRouteFontSize));
		OnPropertyChanged(nameof(CompactRouteFontSize));
		OnPropertyChanged(nameof(TabletTrainNumberFontSize));
		OnPropertyChanged(nameof(CompactTrainNumberFontSize));
		OnPropertyChanged(nameof(TabletCurrentStationFontSize));
		OnPropertyChanged(nameof(CompactCurrentStationFontSize));
		OnPropertyChanged(nameof(TabletCurrentTimeFontSize));
		OnPropertyChanged(nameof(CompactCurrentTimeFontSize));
		OnPropertyChanged(nameof(TabletPastChipFontSize));
		OnPropertyChanged(nameof(CompactPastChipFontSize));
	}

	static void ApplyDensityScaledMetrics(V6RowItem item, Density density)
	{
		double s = DensityScale(density);
		double R(double v) => Math.Round(v * s, 1);
		// Tablet base 22/18 + Compact base 16/14.
		item.TabletStationFontSize = R(22);
		item.TabletTimeFontSize = R(18);
		item.CompactStationFontSize = R(16);
		item.CompactTimeFontSize = R(14);
		item.TabletRowPadding = density switch
		{
			Density.Compact => new Thickness(16, 6),
			Density.Spacious => new Thickness(16, 14),
			_ => new Thickness(16, 10),
		};
		item.CompactRowPadding = density switch
		{
			Density.Compact => new Thickness(12, 5),
			Density.Spacious => new Thickness(12, 11),
			_ => new Thickness(12, 8),
		};
	}

	// V6 upcoming list does not need to scroll-follow the current station
	// (current is rendered in its own big block above). But CurIdxVersion
	// rebuilds Items (boundary shifts), so we proactively reset the ScrollView
	// to top to avoid retaining a stale offset into the new list.
	void AutoFollowScroll()
	{
		try
		{
			var sv = TabletGrid.IsVisible ? TabletScroll : CompactScroll;
			sv.ScrollToAsync(0, 0, animated: false);
		}
		catch
		{
			// Swallow — ScrollView.ScrollToAsync can throw on certain platforms.
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

		TabletGrid.IsVisible = isTablet;
		CompactGrid.IsVisible = !isTablet;
	}

	void RebuildItems()
	{
		var train = _vm.ActiveTrain;
		HasActiveTrain = train is not null;

		// Masthead — TrainData は depot を持たないので placeholder. WorkName を
		// route 名、AffectDate を日付として使う。
		RouteNameText = train?.WorkName ?? string.Empty;
		WorkDateText = train?.AffectDate is DateOnly d
			? d.ToString("yyyy/MM/dd")
			: string.Empty;

		// Train stripe.
		TrainNumberText = train?.TrainNumber ?? string.Empty;
		CarCountText = train?.CarCount is int cc ? $"{cc}両" : "—";
		MaxSpeedText = train?.MaxSpeed is { Length: > 0 } ms ? $"{ms}" : "—";
		DestinationText = train?.Destination is { Length: > 0 } dest ? dest : "—";

		Items.Clear();
		PastChips.Clear();
		CurrentStationName = string.Empty;
		CurrentStationArrive = string.Empty;
		CurrentStationDepart = string.Empty;
		CurrentStationTrack = string.Empty;
		CurrentRowId = string.Empty;
		HasCurrentStation = false;
		HasCurrentMarker = false;
		IsCurrentMarkerFlag = false;
		IsCurrentMarkerCaution = false;
		IsCurrentMarkerStar = false;
		CurrentMarkerText = string.Empty;
		HasCurrentNote = false;
		CurrentNoteText = string.Empty;

		ApplyPageDensityScale(_vm.Density);

		if (train is null || train.Rows is null || train.Rows.Length == 0)
		{
			RaiseAllChanged();
			return;
		}

		int curOrigIdx = _vm.GetCurIdxOverride(train.Id) ?? 0;
		// Clamp to last row index so "all past" maps to the last row being current.
		curOrigIdx = Math.Clamp(curOrigIdx, 0, train.Rows.Length - 1);

		bool showPasses = _vm.ShowPasses;
		var density = _vm.Density;

		// Past chips — 全駅 (showPasses 関係なし; 過去 chip は履歴扱い).
		// Info rows と pass-only に該当する駅も chip としては出して良いが、
		// 情報行は station name を持たないので skip する。
		for (int i = 0; i < curOrigIdx; i++)
		{
			var r = train.Rows[i];
			if (r.IsInfoRow)
				continue;
			PastChips.Add(new V6PastChipItem
			{
				Id = r.Id,
				OrigIndex = i,
				StationName = r.StationName ?? string.Empty,
			});
		}

		// Current station.
		var cur = train.Rows[curOrigIdx];
		if (!cur.IsInfoRow)
		{
			CurrentStationName = cur.StationName ?? string.Empty;
			CurrentStationArrive = FormatHhMm(cur.ArriveTime);
			CurrentStationDepart = FormatHhMm(cur.DepartureTime);
			CurrentStationTrack = cur.TrackName ?? string.Empty;
			CurrentRowId = cur.Id;
			HasCurrentStation = true;
			CurrentNoteText = cur.Remarks ?? string.Empty;
			HasCurrentNote = !string.IsNullOrWhiteSpace(CurrentNoteText);
			if (HasCurrentNote)
				EnsureCurrentNoteLabels();
			RecomputeCurrentMarkerInternal(train.Id, cur.Id);
		}

		// Upcoming — currentIdx より後の駅。Section break は upcoming の中だけで
		// 比較 (cur の RunOutLimit と最初の upcoming の RunInLimit は比較しない)。
		var upcomingVisible = new List<(int origIdx, TimetableRow row)>();
		for (int i = curOrigIdx + 1; i < train.Rows.Length; i++)
		{
			var r = train.Rows[i];
			if (r.IsInfoRow)
				continue;
			if (!showPasses && r.IsPass)
				continue;
			upcomingVisible.Add((i, r));
		}

		int counter = 0;
		TimetableRow? prev = null;
		foreach (var (origIdx, row) in upcomingVisible)
		{
			if (prev is not null && prev.RunOutLimit != row.RunInLimit)
			{
				var newLimit = row.RunInLimit;
				var label = newLimit is int v
					? $"━━ 区間切替 — MAX {v}km/h ━━"
					: "━━ 区間切替 ━━";
				Items.Add(V6RowItem.SectionBreak(id: $"sb:{row.Id}", label: label));
			}

			counter++;
			bool hasNote = !string.IsNullOrWhiteSpace(row.Remarks);
			var marker = _vm.GetMarker(train.Id, row.Id);

			var item = new V6RowItem
			{
				Id = row.Id,
				OrigIndex = origIdx,
				StationName = row.StationName ?? string.Empty,
				CounterText = counter.ToString("D2"),
				ArriveText = FormatTimeOrDash(row.ArriveTime, row.IsPass),
				DepartText = FormatTimeOrDash(row.DepartureTime, row.IsPass),
				TrackName = row.TrackName ?? string.Empty,
				IsPass = row.IsPass,
				HasNote = hasNote,
				NoteText = row.Remarks ?? string.Empty,
				IsNoteOpen = hasNote && _vm.IsNoteOpen(train.Id, row.Id),
				Marker = marker,
				HasMemo = !string.IsNullOrWhiteSpace(_vm.GetMemo(train.Id, row.Id)),
				IsSectionBreakRow = false,
				SectionBreakLabel = string.Empty,
			};
			ApplyDerivedStyling(item);
			ApplyDensityScaledMetrics(item, density);
			Items.Add(item);

			prev = row;
		}

		RaiseAllChanged();
	}

	void RecomputeCurrentMarker()
	{
		var train = _vm.ActiveTrain;
		if (train is null || string.IsNullOrEmpty(CurrentRowId))
		{
			HasCurrentMarker = false;
			IsCurrentMarkerFlag = IsCurrentMarkerCaution = IsCurrentMarkerStar = false;
			CurrentMarkerText = string.Empty;
		}
		else
		{
			RecomputeCurrentMarkerInternal(train.Id, CurrentRowId);
		}
		OnPropertyChanged(nameof(HasCurrentMarker));
		OnPropertyChanged(nameof(IsCurrentMarkerFlag));
		OnPropertyChanged(nameof(IsCurrentMarkerCaution));
		OnPropertyChanged(nameof(IsCurrentMarkerStar));
		OnPropertyChanged(nameof(CurrentMarkerText));
	}

	// Lazy HAL construction for the current-station note (both tablet and
	// compact hosts). See _currentNoteLabel field comment and the matching
	// XAML comment on CurrentNoteHost. Idempotent — no-op once both labels
	// exist; the Text bindings then handle CurrentNoteText updates.
	void EnsureCurrentNoteLabels()
	{
		if (_currentNoteLabel is null)
		{
			_currentNoteLabel = new HtmlAutoDetectLabel
			{
				TextColor = V6RowTablet.LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
				FontSize = 13,
			};
			_currentNoteLabel.SetBinding(
				HtmlAutoDetectLabel.TextProperty,
				new Binding(nameof(CurrentNoteText), source: this));
			CurrentNoteHost.Content = _currentNoteLabel;
		}
		if (_compactCurrentNoteLabel is null)
		{
			_compactCurrentNoteLabel = new HtmlAutoDetectLabel
			{
				TextColor = V6RowTablet.LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
				FontSize = 12,
			};
			_compactCurrentNoteLabel.SetBinding(
				HtmlAutoDetectLabel.TextProperty,
				new Binding(nameof(CurrentNoteText), source: this));
			CompactCurrentNoteHost.Content = _compactCurrentNoteLabel;
		}
	}

	void RecomputeCurrentMarkerInternal(string trainId, string rowId)
	{
		var marker = _vm.GetMarker(trainId, rowId);
		HasCurrentMarker = marker != MarkerKind.None;
		IsCurrentMarkerFlag = marker == MarkerKind.Flag;
		IsCurrentMarkerCaution = marker == MarkerKind.Caution;
		IsCurrentMarkerStar = marker == MarkerKind.Star;
		CurrentMarkerText = marker switch
		{
			MarkerKind.Flag => "◆",
			MarkerKind.Caution => "!",
			MarkerKind.Star => "★",
			_ => string.Empty,
		};
	}

	void RaiseAllChanged()
	{
		OnPropertyChanged(nameof(HasActiveTrain));
		OnPropertyChanged(nameof(HasNoActiveTrain));
		OnPropertyChanged(nameof(DepotName));
		OnPropertyChanged(nameof(RouteNameText));
		OnPropertyChanged(nameof(WorkDateText));
		OnPropertyChanged(nameof(TrainNumberText));
		OnPropertyChanged(nameof(CarCountText));
		OnPropertyChanged(nameof(MaxSpeedText));
		OnPropertyChanged(nameof(DestinationText));
		OnPropertyChanged(nameof(CurrentStationName));
		OnPropertyChanged(nameof(CurrentStationArrive));
		OnPropertyChanged(nameof(CurrentStationDepart));
		OnPropertyChanged(nameof(CurrentStationTrack));
		OnPropertyChanged(nameof(HasCurrentStation));
		OnPropertyChanged(nameof(HasCurrentStationTrack));
		OnPropertyChanged(nameof(CurrentRowId));
		OnPropertyChanged(nameof(HasNoPastChips));
		OnPropertyChanged(nameof(HasCurrentMarker));
		OnPropertyChanged(nameof(IsCurrentMarkerFlag));
		OnPropertyChanged(nameof(IsCurrentMarkerCaution));
		OnPropertyChanged(nameof(IsCurrentMarkerStar));
		OnPropertyChanged(nameof(CurrentMarkerText));
		OnPropertyChanged(nameof(HasCurrentNote));
		OnPropertyChanged(nameof(CurrentNoteText));
	}

	static string FormatHhMm(TimeData? t)
	{
		if (t is null)
			return "—";
		if (t.Hour is int h && t.Minute is int m)
			return $"{h:D2}:{m:D2}";
		return string.IsNullOrEmpty(t.Text) ? "—" : t.Text;
	}

	static string FormatTimeOrDash(TimeData? t, bool isPass)
	{
		if (t is null)
			return isPass ? "↓" : "—";
		if (t.Hour is int h && t.Minute is int m)
			return $"{h:D2}:{m:D2}";
		return string.IsNullOrEmpty(t.Text) ? (isPass ? "↓" : "—") : t.Text;
	}

	static void ApplyDerivedStyling(V6RowItem item)
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

	void OnOpenMarkerPopoverFromSwipe(string? rowId)
	{
		if (string.IsNullOrEmpty(rowId))
			return;
		OpenMarkerPopover(RootGrid, rowId);
	}

	// ── Public entry points for V6Row* (Parent-walked invocations) ─────────
	//
	// V6RowTablet / V6RowCompact don't bind their SwipeItem.Command through
	// the row's BindingContext (the BindingContext is the per-row V6RowItem,
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
	// the tap gesture in V6Row*; we use it as the popover anchor so the
	// floating UI positions next to the visible marker.
	public void OpenMarkerPopoverFromBadge(View? anchor, string? rowId)
	{
		if (anchor is null || string.IsNullOrEmpty(rowId))
			return;
		OpenMarkerPopover(anchor, rowId);
	}

	void OnCurrentMarkerBadgeTapped(object? sender, TappedEventArgs e)
	{
		if (string.IsNullOrEmpty(CurrentRowId))
			return;
		var anchor = sender as View ?? RootGrid;
		OpenMarkerPopover(anchor, CurrentRowId);
	}

	// Past chip → jump to that station. Past chips carry origIndex so this
	// is just a SetCurIdx call; CurIdxVersion bumps and RebuildItems shifts
	// the split.
	void OnPastChipTapped(object? sender, TappedEventArgs e)
	{
		if (sender is not Border border)
			return;
		if (border.BindingContext is not V6PastChipItem chip)
			return;
		var train = _vm.ActiveTrain;
		if (train is null)
			return;
		_vm.SetCurIdx(train.Id, chip.OrigIndex);
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
			// Popover failures shouldn't crash the page.
		}
	}

	// MemoSheet wiring (Phase 2) ------------------------------------------

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

		var accentFg = Application.Current?.Resources["OT_AccentFg_Light"] as Color;
		var fg = Application.Current?.Resources["OT_Fg_Light"] as Color;
		DensityCompactLabel.TextColor = (d == Density.Compact ? accentFg : fg) ?? Colors.Black;
		DensityComfortableLabel.TextColor = (d == Density.Comfortable ? accentFg : fg) ?? Colors.Black;
		DensitySpaciousLabel.TextColor = (d == Density.Spacious ? accentFg : fg) ?? Colors.Black;
	}
}

// Past chip view-model. Tap-to-jump uses OrigIndex (Phase 2). PastChips is
// rebuilt wholesale in RebuildItems, so no INPC needed.
public class V6PastChipItem
{
	public string Id { get; set; } = string.Empty;
	public int OrigIndex { get; set; } = -1;
	public string StationName { get; set; } = string.Empty;

	public string ChipAutomationId => $"OriginalTimetable.V6.PastChip.{Id}";
}

// Upcoming-list row view-model. Mirrors V1RowItem's ObservableObject pattern
// so MarkersVersion / MemosVersion / NoteOpenVersion / Density can mutate
// visible props in place without touching Items (preserves CollectionView
// scroll position).
//
// Phase 2/3 additions:
//   - IsNoteOpen / NoteText / HasNote already in Phase 1 — wired through to
//     XAML NoteFold this commit.
//   - Tablet/Compact * Station/TimeFontSize + RowPadding [ObservableProperty]
//     so density reflows without Items.Clear.
public partial class V6RowItem : ObservableObject
{
	public string Id { get; set; } = string.Empty;
	public int OrigIndex { get; set; } = -1;
	public string StationName { get; set; } = string.Empty;
	public string CounterText { get; set; } = string.Empty;
	public string ArriveText { get; set; } = string.Empty;
	public string DepartText { get; set; } = string.Empty;
	public string TrackName { get; set; } = string.Empty;
	public bool IsPass { get; set; }

	[ObservableProperty]
	public partial bool IsCurrent { get; set; }

	public bool HasNote { get; set; }
	public string NoteText { get; set; } = string.Empty;

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

	// Density-scaled row metrics (Phase 3).
	[ObservableProperty]
	public partial double TabletStationFontSize { get; set; } = 22;
	[ObservableProperty]
	public partial double TabletTimeFontSize { get; set; } = 18;
	[ObservableProperty]
	public partial double CompactStationFontSize { get; set; } = 16;
	[ObservableProperty]
	public partial double CompactTimeFontSize { get; set; } = 14;
	[ObservableProperty]
	public partial Thickness TabletRowPadding { get; set; } = new Thickness(16, 10);
	[ObservableProperty]
	public partial Thickness CompactRowPadding { get; set; } = new Thickness(12, 8);

	public bool IsNormalRow => !IsSectionBreakRow;
	public bool HasTrackName => !string.IsNullOrEmpty(TrackName);

	const string AutomationIdPrefix = "OriginalTimetable.V6.Row.";
	public string RowAutomationId => $"{AutomationIdPrefix}{Id}";
	public string MarkerAutomationId => $"{AutomationIdPrefix}{Id}.Marker";
	public string MemoAutomationId => $"{AutomationIdPrefix}{Id}.Memo";
	public string ClearAutomationId => $"{AutomationIdPrefix}{Id}.Clear";
	public string MarkerBadgeAutomationId => $"{AutomationIdPrefix}{Id}.MarkerBadge";
	public string NoteBodyAutomationId => $"{AutomationIdPrefix}{Id}.NoteBody";

	public static V6RowItem SectionBreak(string id, string label) => new()
	{
		Id = id,
		IsSectionBreakRow = true,
		SectionBreakLabel = label,
	};
}
