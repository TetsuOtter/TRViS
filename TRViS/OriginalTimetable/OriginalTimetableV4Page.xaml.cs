using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;

using TR.Maui.AnchorPopover;

using TRViS.IO.Models;
using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS.OriginalTimetable;

// V4 Next Big — 独自時刻表ページ骨格 (Phase 1 + Phase 2 + Phase 3).
//
// Phase 1 (commit 79e9cfc): tablet+compact split, hero block (next station,
// big 56pt), mini list with section breaks + marker badges + memo dots,
// SwipeItem cycle/clear/openMemo (open-memo was a no-op stub).
//
// Phase 2 (this commit): MarkerPopover anchored to badge (mini list)
// + hero marker badge / "+ マーカー" placeholder (anchors popover for the
// hero's next-station rowId), MemoSheet overlay (bottom-sheet), inline
// NoteFold inside each mini-list row (Border placed beneath the row Grid
// inside the row Border) + hero NoteBody (always-visible when next station
// has Remarks).
//
// Phase 3 (this commit): Tweaks panel (gear icon → overlay) with ShowPasses
// Switch + Density tri-state (狭/標準/広). Density-driven scaling applied to
// hero (HeroStationFontSize/HeroTimeFontSize/HeroPlatformSize/
// HeroPlatformFontSize + compact equivalents) AND every mini-row metric
// (Tablet/Compact * Station/TimeFontSize, RowPadding) via
// ApplyDensityScaledMetrics — so density visibly reflows without an
// Items.Clear+Add (CollectionView scroll preserved).
//
// V4-specific CurIdxVersion strategy: hero retargets AND mini-list's
// IsHiddenInList shifts (the previously-hero row becomes visible, the new
// next-station row hides) AND IsPassed flips per origIdx<curOrigIdx. We
// split RebuildItems into RecomputeHero(train) + the mini-list build, and
// on CurIdxVersion call RecomputeHero + UpdateCurrentInPlace + (if Follow)
// scroll mini list to top. Items collection is not cleared so scroll holds.
public partial class OriginalTimetableV4Page : ContentPage
{
	public static readonly string NameOfThisClass = nameof(OriginalTimetableV4Page);

	// Tablet ≥600pt (Material breakpoint; iPad mini portrait 744pt counts).
	const double TabletBreakpoint = 600;

	readonly OriginalTimetableViewModel _vm;
	double _lastWidth = -1;
	bool _lastIsTablet;

	public ObservableCollection<V4RowItem> Items { get; } = new();

	// Train-stripe bindings (refreshed by RebuildItems).
	public string HeaderTypeText { get; private set; } = string.Empty;
	public bool HasHeaderType { get; private set; }
	public string HeaderTrainNumberText { get; private set; } = string.Empty;
	public string HeaderDestinationText { get; private set; } = string.Empty;
	public string HeaderCarsAndSpeedText { get; private set; } = string.Empty;
	public string HeaderCarsAndSpeedTextCompact { get; private set; } = string.Empty;
	public bool HasActiveTrain { get; private set; }
	public bool HasNoActiveTrain => !HasActiveTrain;

	// Hero block bindings — populated by RecomputeHero whenever the next
	// station may have shifted (RebuildItems or CurIdxVersion bump).
	public string NextStationName { get; private set; } = string.Empty;
	public string NextStationArriveText { get; private set; } = string.Empty;
	public string NextStationDepartText { get; private set; } = string.Empty;
	public string NextStationTrack { get; private set; } = "–";
	public string NextPlusOneText { get; private set; } = string.Empty;
	public bool HasNextPlusOne { get; private set; }
	// Used to anchor the hero MarkerPopover and to read/write the hero's
	// marker via the VM. Empty when no next station (terminus / no train).
	public string? NextStationId { get; private set; }
	public bool HasHeroMarker { get; private set; }
	public bool HasNoHeroMarker => HasActiveTrain && !string.IsNullOrEmpty(NextStationId) && !HasHeroMarker;
	public string HeroMarkerText { get; private set; } = string.Empty;
	public string HeroNoteText { get; private set; } = string.Empty;
	public bool HasHeroNote { get; private set; }

	// Hero density-scaled font sizes (tablet base: 56/30/72/46).
	public double HeroStationFontSize { get; private set; } = 56;
	public double HeroTimeFontSize { get; private set; } = 30;
	public double HeroPlatformSize { get; private set; } = 72;
	public double HeroPlatformFontSize { get; private set; } = 46;
	// Hero density-scaled font sizes (compact base: 40/22/60/36).
	public double CompactHeroStationFontSize { get; private set; } = 40;
	public double CompactHeroTimeFontSize { get; private set; } = 22;
	public double CompactHeroPlatformSize { get; private set; } = 60;
	public double CompactHeroPlatformFontSize { get; private set; } = 36;

	// SwipeItem / inline commands invoked from inside the DataTemplate via
	// {Source={x:Reference Self}}.
	public ICommand CycleMarkerCommand { get; }
	public ICommand ClearMarkerCommand { get; }
	public ICommand OpenMemoCommand { get; }
	public ICommand ToggleNoteCommand { get; }
	public ICommand OpenMarkerPopoverFromSwipeCommand { get; }

	// Sheet-edit state.
	string? _memoRowId;

	public OriginalTimetableV4Page()
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
				// Visible row set changes — full rebuild required.
				RebuildItems();
				break;
			case nameof(OriginalTimetableViewModel.MarkersVersion):
				UpdateMarkersInPlace();
				RecomputeHeroMarker();
				break;
			case nameof(OriginalTimetableViewModel.MemosVersion):
				UpdateMemosInPlace();
				break;
			case nameof(OriginalTimetableViewModel.NoteOpenVersion):
				UpdateNoteOpenInPlace();
				break;
			case nameof(OriginalTimetableViewModel.CurIdxVersion):
				// V4 special handling: hero retargets + mini-list IsHidden/
				// IsPassed flips in place. No Items.Clear so scroll holds.
				var train = _vm.ActiveTrain;
				if (train is not null)
				{
					RecomputeHero(train);
					UpdateCurrentInPlace(train);
				}
				if (_vm.Follow)
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

	// CurIdxVersion in-place pass: shift IsPassed (origIdx<curOrigIdx) and
	// IsHiddenInList (origIdx==nextIdx) per row. Don't touch Items collection.
	void UpdateCurrentInPlace(TrainData train)
	{
		int curOrigIdx = _vm.GetCurIdxOverride(train.Id) ?? 0;
		var rows = train.Rows;
		if (rows is null)
			return;
		int nextIdx = FindNextStopIndex(rows, curOrigIdx);
		foreach (var item in Items)
		{
			if (item.IsSectionBreakRow)
				continue;
			item.IsPassed = item.OrigIndex < curOrigIdx;
			item.IsHiddenInList = item.OrigIndex == nextIdx;
		}
	}

	void UpdateDensityInPlace()
	{
		var d = _vm.Density;
		ApplyHeroDensityScale(d);
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

	static void ApplyDensityScaledMetrics(V4RowItem item, Density density)
	{
		double s = DensityScale(density);
		double R(double v) => Math.Round(v * s, 1);
		// Tablet base 22/17/14,8 + Compact base 16/13/12,6.
		item.TabletStationFontSize = R(22);
		item.TabletTimeFontSize = R(17);
		item.CompactStationFontSize = R(16);
		item.CompactTimeFontSize = R(13);
		item.TabletRowPadding = density switch
		{
			Density.Compact => new Thickness(14, 5),
			Density.Spacious => new Thickness(14, 12),
			_ => new Thickness(14, 8),
		};
		item.CompactRowPadding = density switch
		{
			Density.Compact => new Thickness(12, 4),
			Density.Spacious => new Thickness(12, 9),
			_ => new Thickness(12, 6),
		};
	}

	void ApplyHeroDensityScale(Density density)
	{
		double s = DensityScale(density);
		double R(double v) => Math.Round(v * s, 1);
		double RI(double v) => Math.Round(v * s);
		HeroStationFontSize = R(56);
		HeroTimeFontSize = R(30);
		HeroPlatformSize = RI(72);
		HeroPlatformFontSize = R(46);
		CompactHeroStationFontSize = R(40);
		CompactHeroTimeFontSize = R(22);
		CompactHeroPlatformSize = RI(60);
		CompactHeroPlatformFontSize = R(36);
		OnPropertyChanged(nameof(HeroStationFontSize));
		OnPropertyChanged(nameof(HeroTimeFontSize));
		OnPropertyChanged(nameof(HeroPlatformSize));
		OnPropertyChanged(nameof(HeroPlatformFontSize));
		OnPropertyChanged(nameof(CompactHeroStationFontSize));
		OnPropertyChanged(nameof(CompactHeroTimeFontSize));
		OnPropertyChanged(nameof(CompactHeroPlatformSize));
		OnPropertyChanged(nameof(CompactHeroPlatformFontSize));
	}

	// Mini list scroll — for V4 the simplest "follow" is scroll-to-top
	// (hero is fixed at top so the user always sees the relevant cards
	// starting just below it). Same intent as V1/V2 follow.
	void AutoFollowScroll()
	{
		try
		{
			var cv = TabletGrid.IsVisible ? TabletMiniList : CompactMiniList;
			cv.ScrollTo(index: 0, position: ScrollToPosition.Start, animate: true);
		}
		catch
		{
			// Swallow — CollectionView.ScrollTo can throw on certain platforms.
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
		HeaderTypeText = train?.SpeedType ?? string.Empty;
		HasHeaderType = !string.IsNullOrEmpty(HeaderTypeText);
		HeaderTrainNumberText = train?.TrainNumber ?? string.Empty;
		HeaderDestinationText = train?.Destination is { Length: > 0 } d ? $"行先 {d}" : string.Empty;
		var carsPart = train?.CarCount is int cc ? $"{cc}両" : string.Empty;
		var speedPart = train?.MaxSpeed is { Length: > 0 } ms ? $"最高 {ms}km/h" : string.Empty;
		HeaderCarsAndSpeedText = (carsPart, speedPart) switch
		{
			("", "") => string.Empty,
			("", _) => speedPart,
			(_, "") => carsPart,
			_ => $"{carsPart} · {speedPart}",
		};
		var speedPartCompact = train?.MaxSpeed is { Length: > 0 } ms2 ? $"最高{ms2}" : string.Empty;
		HeaderCarsAndSpeedTextCompact = (carsPart, speedPartCompact) switch
		{
			("", "") => string.Empty,
			("", _) => speedPartCompact,
			(_, "") => carsPart,
			_ => $"{carsPart} · {speedPartCompact}",
		};

		// Reset hero (overwritten by RecomputeHero when we have a train).
		NextStationName = string.Empty;
		NextStationArriveText = string.Empty;
		NextStationDepartText = string.Empty;
		NextStationTrack = "–";
		NextPlusOneText = string.Empty;
		HasNextPlusOne = false;
		NextStationId = null;
		HasHeroMarker = false;
		HeroMarkerText = string.Empty;
		HeroNoteText = string.Empty;
		HasHeroNote = false;

		Items.Clear();
		ApplyHeroDensityScale(_vm.Density);

		if (train is not null && train.Rows is not null && train.Rows.Length > 0)
		{
			RecomputeHero(train);
			BuildItems(train);
		}

		OnPropertyChanged(nameof(HasActiveTrain));
		OnPropertyChanged(nameof(HasNoActiveTrain));
		OnPropertyChanged(nameof(HeaderTypeText));
		OnPropertyChanged(nameof(HasHeaderType));
		OnPropertyChanged(nameof(HeaderTrainNumberText));
		OnPropertyChanged(nameof(HeaderDestinationText));
		OnPropertyChanged(nameof(HeaderCarsAndSpeedText));
		OnPropertyChanged(nameof(HeaderCarsAndSpeedTextCompact));
	}

	// Hero re-target only (no Items mutation). Called from RebuildItems and
	// from CurIdxVersion handler — that's how mini-list scroll is preserved.
	void RecomputeHero(TrainData train)
	{
		var rows = train.Rows;
		if (rows is null || rows.Length == 0)
			return;

		int curOrigIdx = _vm.GetCurIdxOverride(train.Id) ?? 0;
		int nextIdx = FindNextStopIndex(rows, curOrigIdx);
		int nextPlusOneIdx = nextIdx >= 0 ? FindNextStopIndex(rows, nextIdx) : -1;

		NextStationName = string.Empty;
		NextStationArriveText = string.Empty;
		NextStationDepartText = string.Empty;
		NextStationTrack = "–";
		NextPlusOneText = string.Empty;
		HasNextPlusOne = false;
		NextStationId = null;
		HeroNoteText = string.Empty;
		HasHeroNote = false;

		if (nextIdx >= 0)
		{
			var nextRow = rows[nextIdx];
			NextStationId = nextRow.Id;
			NextStationName = nextRow.StationName ?? string.Empty;
			NextStationArriveText = FormatHhMm(nextRow.ArriveTime);
			NextStationDepartText = FormatHhMm(nextRow.DepartureTime);
			NextStationTrack = string.IsNullOrEmpty(nextRow.TrackName) ? "–" : nextRow.TrackName!;
			HeroNoteText = nextRow.Remarks ?? string.Empty;
			HasHeroNote = !string.IsNullOrWhiteSpace(HeroNoteText);

			if (nextPlusOneIdx >= 0)
			{
				var afterRow = rows[nextPlusOneIdx];
				var afterTime = FormatHhMm(afterRow.DepartureTime);
				if (string.IsNullOrEmpty(afterTime))
					afterTime = FormatHhMm(afterRow.ArriveTime);
				NextPlusOneText = string.IsNullOrEmpty(afterTime)
					? (afterRow.StationName ?? string.Empty)
					: $"{afterRow.StationName} · {afterTime}";
				HasNextPlusOne = !string.IsNullOrEmpty(NextPlusOneText);
			}
		}

		RecomputeHeroMarker();

		OnPropertyChanged(nameof(NextStationName));
		OnPropertyChanged(nameof(NextStationArriveText));
		OnPropertyChanged(nameof(NextStationDepartText));
		OnPropertyChanged(nameof(NextStationTrack));
		OnPropertyChanged(nameof(NextPlusOneText));
		OnPropertyChanged(nameof(HasNextPlusOne));
		OnPropertyChanged(nameof(HeroNoteText));
		OnPropertyChanged(nameof(HasHeroNote));
	}

	void RecomputeHeroMarker()
	{
		var train = _vm.ActiveTrain;
		if (train is null || string.IsNullOrEmpty(NextStationId))
		{
			HasHeroMarker = false;
			HeroMarkerText = string.Empty;
		}
		else
		{
			var marker = _vm.GetMarker(train.Id, NextStationId);
			HasHeroMarker = marker != MarkerKind.None;
			HeroMarkerText = marker switch
			{
				MarkerKind.Flag => "◆",
				MarkerKind.Caution => "!",
				MarkerKind.Star => "★",
				_ => string.Empty,
			};
		}
		OnPropertyChanged(nameof(HasHeroMarker));
		OnPropertyChanged(nameof(HasNoHeroMarker));
		OnPropertyChanged(nameof(HeroMarkerText));
	}

	void BuildItems(TrainData train)
	{
		bool showPasses = _vm.ShowPasses;
		var rows = train.Rows!;
		int curOrigIdx = _vm.GetCurIdxOverride(train.Id) ?? 0;
		int nextIdx = FindNextStopIndex(rows, curOrigIdx);
		var density = _vm.Density;

		TimetableRow? prev = null;
		for (int i = 0; i < rows.Length; i++)
		{
			var r = rows[i];
			if (r.IsInfoRow)
				continue;
			if (!showPasses && r.IsPass)
				continue;

			if (prev is not null && prev.RunOutLimit != r.RunInLimit)
			{
				var newLimit = r.RunInLimit;
				var label = newLimit is int v
					? $"▼ 区間切替 — 最高 {v}km/h"
					: "▼ 区間切替";
				Items.Add(V4RowItem.SectionBreak(id: $"sb:{r.Id}", label: label));
			}

			var marker = _vm.GetMarker(train.Id, r.Id);
			bool hasMemo = !string.IsNullOrWhiteSpace(_vm.GetMemo(train.Id, r.Id));
			bool hasNote = !string.IsNullOrWhiteSpace(r.Remarks);
			bool isNoteOpen = hasNote && _vm.IsNoteOpen(train.Id, r.Id);

			var item = new V4RowItem
			{
				Id = r.Id,
				OrigIndex = i,
				StationName = r.StationName ?? string.Empty,
				ArriveText = FormatHhMm(r.ArriveTime),
				DepartText = FormatHhMm(r.DepartureTime),
				TrackName = r.TrackName ?? string.Empty,
				IsPass = r.IsPass,
				IsPassed = i < curOrigIdx,
				IsHiddenInList = i == nextIdx,
				Marker = marker,
				HasMemo = hasMemo,
				HasNote = hasNote,
				IsNoteOpen = isNoteOpen,
				NoteText = r.Remarks ?? string.Empty,
				IsSectionBreakRow = false,
				SectionBreakLabel = string.Empty,
			};
			ApplyDerivedStyling(item);
			ApplyDensityScaledMetrics(item, density);
			Items.Add(item);

			prev = r;
		}
	}

	int FindNextStopIndex(TimetableRow[] rows, int fromIdx)
	{
		bool showPasses = _vm.ShowPasses;
		for (int i = fromIdx + 1; i < rows.Length; i++)
		{
			var r = rows[i];
			if (r.IsInfoRow)
				continue;
			if (!showPasses && r.IsPass)
				continue;
			return i;
		}
		return -1;
	}

	static string FormatHhMm(TimeData? t)
	{
		if (t is null)
			return string.Empty;
		if (t.Hour is int h && t.Minute is int m)
			return $"{h:D2}:{m:D2}";
		return t.Text ?? string.Empty;
	}

	static void ApplyDerivedStyling(V4RowItem item)
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

	void OnOpenMarkerPopoverFromSwipe(string? rowId)
	{
		if (string.IsNullOrEmpty(rowId))
			return;
		OpenMarkerPopover(RootGrid, rowId);
	}

	void OnMarkerBadgeTapped(object? sender, TappedEventArgs e)
	{
		if (sender is not Border border)
			return;
		if (border.BindingContext is not V4RowItem item)
			return;
		OpenMarkerPopover(border, item.Id);
	}

	void OnHeroMarkerBadgeTapped(object? sender, TappedEventArgs e)
	{
		if (string.IsNullOrEmpty(NextStationId))
			return;
		var anchor = sender as View ?? RootGrid;
		OpenMarkerPopover(anchor, NextStationId);
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

		var accentFg = Application.Current?.Resources["OT_AccentFg_Light"] as Color;
		var fg = Application.Current?.Resources["OT_Fg_Light"] as Color;
		DensityCompactLabel.TextColor = (d == Density.Compact ? accentFg : fg) ?? Colors.Black;
		DensityComfortableLabel.TextColor = (d == Density.Comfortable ? accentFg : fg) ?? Colors.Black;
		DensitySpaciousLabel.TextColor = (d == Density.Spacious ? accentFg : fg) ?? Colors.Black;
	}
}

// V4 mini-list row VM. ObservableObject so MarkersVersion / MemosVersion /
// NoteOpenVersion / CurIdxVersion / Density can mutate visible props
// in place (CollectionView scroll preserved). NoteOpenVersion is new in
// Phase 2 — IsNoteOpen drives the inline NoteFold Border visibility.
//
// Phase 2/3 changes vs Phase 1:
//   - IsHiddenInList / IsPassed promoted to [ObservableProperty] (CurIdx
//     in-place flips them per-row) + NotifyPropertyChangedFor on the
//     derived IsVisibleNormalRow so Border IsVisible refreshes.
//   - HasNote / IsNoteOpen / NoteText added for inline NoteFold.
//   - Tablet/Compact * Station/TimeFontSize + RowPadding [ObservableProperty]
//     so density reflows without Items.Clear.
public partial class V4RowItem : ObservableObject
{
	public string Id { get; set; } = string.Empty;
	public int OrigIndex { get; set; } = -1;
	public string StationName { get; set; } = string.Empty;
	public string ArriveText { get; set; } = string.Empty;
	public string DepartText { get; set; } = string.Empty;
	public string TrackName { get; set; } = string.Empty;
	public bool IsPass { get; set; }

	// Updated in-place by UpdateCurrentInPlace on CurIdxVersion.
	[ObservableProperty]
	public partial bool IsPassed { get; set; }

	// Mini-row Border.IsVisible binds to IsVisibleNormalRow — that derives
	// off IsHiddenInList, so re-notify when IsHiddenInList changes.
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsVisibleNormalRow))]
	public partial bool IsHiddenInList { get; set; }

	[ObservableProperty]
	public partial MarkerKind Marker { get; set; } = MarkerKind.None;

	[ObservableProperty]
	public partial bool HasMemo { get; set; }

	// NoteFold (Phase 2).
	public bool HasNote { get; set; }
	public string NoteText { get; set; } = string.Empty;
	[ObservableProperty]
	public partial bool IsNoteOpen { get; set; }

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

	// Density-scaled mini-row metrics (Phase 3).
	[ObservableProperty]
	public partial double TabletStationFontSize { get; set; } = 22;
	[ObservableProperty]
	public partial double TabletTimeFontSize { get; set; } = 17;
	[ObservableProperty]
	public partial double CompactStationFontSize { get; set; } = 16;
	[ObservableProperty]
	public partial double CompactTimeFontSize { get; set; } = 13;
	[ObservableProperty]
	public partial Thickness TabletRowPadding { get; set; } = new Thickness(14, 8);
	[ObservableProperty]
	public partial Thickness CompactRowPadding { get; set; } = new Thickness(12, 6);

	public bool IsNormalRow => !IsSectionBreakRow;
	public bool IsVisibleNormalRow => IsNormalRow && !IsHiddenInList;
	public bool HasTrackName => !string.IsNullOrEmpty(TrackName);

	const string AutomationIdPrefix = "OriginalTimetable.V4.Row.";
	public string RowAutomationId => $"{AutomationIdPrefix}{Id}";
	public string MarkerAutomationId => $"{AutomationIdPrefix}{Id}.Marker";
	public string MemoAutomationId => $"{AutomationIdPrefix}{Id}.Memo";
	public string ClearAutomationId => $"{AutomationIdPrefix}{Id}.Clear";
	public string MarkerBadgeAutomationId => $"{AutomationIdPrefix}{Id}.MarkerBadge";
	public string NoteBodyAutomationId => $"{AutomationIdPrefix}{Id}.NoteBody";

	public static V4RowItem SectionBreak(string id, string label) => new()
	{
		Id = id,
		IsSectionBreakRow = true,
		SectionBreakLabel = label,
	};
}
