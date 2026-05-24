using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;

using TRViS.IO.Models;
using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS.OriginalTimetable;

// V4 Next Big — 独自時刻表ページ骨格 (Phase 1).
//
// Layout sourced from prototype/v4.jsx: a large hero block for the next
// station (56pt name + 着/発 + PLATFORM tile + NEXT+1 preview) sits above
// a compact mini list showing the rest of the train. The "current" station
// from the VM's curIdxOverride is treated as "already-departed/at"; we
// surface curIdx+1 as the hero target ("次駅"), and hide that row from the
// mini list so the same station never appears twice.
//
// Phase 1 covers: tablet+compact split, hero block (rebuild on
// ActiveTrain/ShowPasses/CurIdxVersion), mini list with section breaks,
// marker badges, memo dots, SwipeItem wiring for cycle/clear/openMemo
// (open-memo is a no-op stub — Phase 2 adds the sheet). MarkerPopover,
// MemoSheet, NoteFold, Tweaks panel and Density are Phase 2/3.
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

	// Hero block bindings — populated by RebuildItems when an ActiveTrain
	// exists, then refreshed whenever CurIdxVersion bumps.
	public string NextStationName { get; private set; } = string.Empty;
	public string NextStationArriveText { get; private set; } = string.Empty;
	public string NextStationDepartText { get; private set; } = string.Empty;
	public string NextStationTrack { get; private set; } = "–";
	public string NextPlusOneText { get; private set; } = string.Empty;
	public bool HasNextPlusOne { get; private set; }

	// SwipeItem commands invoked from inside the DataTemplate via
	// {Source={x:Reference Self}}. CommandParameter is the row id.
	public ICommand CycleMarkerCommand { get; }
	public ICommand ClearMarkerCommand { get; }
	public ICommand OpenMemoCommand { get; }

	public OriginalTimetableV4Page()
	{
		_vm = InstanceManager.OriginalTimetableViewModel;

		CycleMarkerCommand = new Command<string>(OnCycleMarker);
		ClearMarkerCommand = new Command<string>(OnClearMarker);
		// Phase 1: memo sheet is not wired up. Keep the SwipeItem visible
		// so the swipe gesture still works; the action is a no-op stub.
		OpenMemoCommand = new Command<string>(_ => { });

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
			// Phase 1: when the current-station pointer advances, the hero
			// block has to retarget (NextStationName etc. shift to the new
			// next row) AND a different mini-list row becomes the hidden
			// one. Easiest to just rebuild — mini list is short relative to
			// the hero so the scroll-jump is acceptable in Phase 1.
			case nameof(OriginalTimetableViewModel.CurIdxVersion):
				RebuildItems();
				break;
			case nameof(OriginalTimetableViewModel.MarkersVersion):
				UpdateMarkersInPlace();
				break;
			case nameof(OriginalTimetableViewModel.MemosVersion):
				UpdateMemosInPlace();
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
		// keep imperative tree manipulation minimal (avoids the
		// ApplyStyleSheets NRE path on iPad). Same pattern as V1.
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

		// Hero defaults (overwritten below when we have a real row).
		NextStationName = string.Empty;
		NextStationArriveText = string.Empty;
		NextStationDepartText = string.Empty;
		NextStationTrack = "–";
		NextPlusOneText = string.Empty;
		HasNextPlusOne = false;

		Items.Clear();

		if (train is not null && train.Rows is not null && train.Rows.Length > 0)
		{
			BuildHeroAndItems(train);
		}

		// Notify all bound properties at once. Most of these don't have
		// INPC backing fields (the props are read-only), so explicit raises
		// are required.
		OnPropertyChanged(nameof(HasActiveTrain));
		OnPropertyChanged(nameof(HasNoActiveTrain));
		OnPropertyChanged(nameof(HeaderTypeText));
		OnPropertyChanged(nameof(HasHeaderType));
		OnPropertyChanged(nameof(HeaderTrainNumberText));
		OnPropertyChanged(nameof(HeaderDestinationText));
		OnPropertyChanged(nameof(HeaderCarsAndSpeedText));
		OnPropertyChanged(nameof(HeaderCarsAndSpeedTextCompact));
		OnPropertyChanged(nameof(NextStationName));
		OnPropertyChanged(nameof(NextStationArriveText));
		OnPropertyChanged(nameof(NextStationDepartText));
		OnPropertyChanged(nameof(NextStationTrack));
		OnPropertyChanged(nameof(NextPlusOneText));
		OnPropertyChanged(nameof(HasNextPlusOne));
	}

	void BuildHeroAndItems(TrainData train)
	{
		bool showPasses = _vm.ShowPasses;
		var rows = train.Rows!;

		// "Current" row index per VM (the row the train is at / has just
		// departed). Hero displays curIdx+1 ("次駅"). Walk forward to
		// clamp at the last non-info row when we're at terminus.
		int curOrigIdx = _vm.GetCurIdxOverride(train.Id) ?? 0;
		int nextIdx = FindNextStopIndex(rows, curOrigIdx);
		int nextPlusOneIdx = nextIdx >= 0 ? FindNextStopIndex(rows, nextIdx) : -1;

		if (nextIdx >= 0)
		{
			var nextRow = rows[nextIdx];
			NextStationName = nextRow.StationName ?? string.Empty;
			NextStationArriveText = FormatHhMm(nextRow.ArriveTime);
			NextStationDepartText = FormatHhMm(nextRow.DepartureTime);
			NextStationTrack = string.IsNullOrEmpty(nextRow.TrackName) ? "–" : nextRow.TrackName!;

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

		// Mini list: include every visible row, but hide the hero's row so
		// the same station doesn't appear twice. Use a per-row
		// IsHiddenInList flag (Border.IsVisible is bound to
		// !IsHiddenInList) rather than a converter.
		TimetableRow? prev = null;
		for (int i = 0; i < rows.Length; i++)
		{
			var r = rows[i];
			if (r.IsInfoRow)
				continue;
			if (!showPasses && r.IsPass)
				continue;

			// Section break between successive (visible, non-info) rows
			// when their limit segment changes — mirrors V1.
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
				IsSectionBreakRow = false,
				SectionBreakLabel = string.Empty,
			};
			ApplyDerivedStyling(item);
			Items.Add(item);

			prev = r;
		}
	}

	// Skip info rows and (when ShowPasses=false) pass rows, mirroring the
	// prototype's `find(s => s.kind !== 'pass')`. Returns -1 if no next
	// stop exists past `fromIdx` (we're at terminus / single-row train).
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
}

// Mini-list row VM. ObservableObject so MarkersVersion / MemosVersion
// changes can mutate visible props in place without an Items.Clear+Add
// (CollectionView scroll position is preserved). Identity props (Id,
// OrigIndex, station/time text, IsHiddenInList) stay plain — those only
// change during full RebuildItems().
public partial class V4RowItem : ObservableObject
{
	public string Id { get; set; } = string.Empty;
	// Index in ActiveTrain.Rows (the unfiltered list). -1 for section breaks.
	public int OrigIndex { get; set; } = -1;
	public string StationName { get; set; } = string.Empty;
	public string ArriveText { get; set; } = string.Empty;
	public string DepartText { get; set; } = string.Empty;
	public string TrackName { get; set; } = string.Empty;
	public bool IsPass { get; set; }
	// "Already departed" — dimmed in the mini list (matches v4.jsx opacity:0.4).
	public bool IsPassed { get; set; }
	// True when this row is the hero's "next station" — collapse to 0
	// height in the mini list so the station isn't shown twice.
	public bool IsHiddenInList { get; set; }

	[ObservableProperty]
	public partial MarkerKind Marker { get; set; } = MarkerKind.None;

	[ObservableProperty]
	public partial bool HasMemo { get; set; }

	public bool IsSectionBreakRow { get; set; }
	public string SectionBreakLabel { get; set; } = string.Empty;

	// Derived (filled by ApplyDerivedStyling). Bools drive DataTriggers in XAML.
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

	public bool IsNormalRow => !IsSectionBreakRow;
	// Mini-list Border.IsVisible binds here so the hero's station collapses
	// to 0 height in the list (avoids needing a value converter).
	public bool IsVisibleNormalRow => IsNormalRow && !IsHiddenInList;
	public bool HasTrackName => !string.IsNullOrEmpty(TrackName);

	// AutomationId helpers — generated from Id so each row has stable,
	// distinct accessibility identifiers for E2E tests.
	const string AutomationIdPrefix = "OriginalTimetable.V4.Row.";
	public string RowAutomationId => $"{AutomationIdPrefix}{Id}";
	public string MarkerAutomationId => $"{AutomationIdPrefix}{Id}.Marker";
	public string MemoAutomationId => $"{AutomationIdPrefix}{Id}.Memo";
	public string ClearAutomationId => $"{AutomationIdPrefix}{Id}.Clear";
	public string MarkerBadgeAutomationId => $"{AutomationIdPrefix}{Id}.MarkerBadge";

	public static V4RowItem SectionBreak(string id, string label) => new()
	{
		Id = id,
		IsSectionBreakRow = true,
		SectionBreakLabel = label,
	};
}
