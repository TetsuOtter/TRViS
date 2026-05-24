using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;

using TRViS.IO.Models;
using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS.OriginalTimetable;

// V2 Card Stack — 独自時刻表ページ骨格 (Phase 1: tablet + compact).
//
// Mirrors the V1 Phase 1 pattern (CollectionView + DataTemplate + SwipeView,
// in-place ObservableObject mutation, IsVisible-toggled tablet/compact grids)
// because that path was empirically verified to avoid the iPad
// Microsoft.Maui.Controls.Element.ApplyStyleSheets NRE that bit earlier
// imperative-tree V1 work. The card visual changes (rounded outer Border,
// concentric inner platform tile, accent-soft background + 1.5px accent
// border for current, vertically-stacked 着/発 with 40pt/22pt mono numerals)
// are all expressed inside the DataTemplate — no custom controls Add'd.
//
// Phase 2 (MarkerPopover anchored to badge, MemoSheet overlay, note-fold
// body) and Phase 3 (Density/Follow/Tweaks panel) land in subsequent tasks;
// OpenMemoCommand here is a no-op so the SwipeItem keeps a valid binding.
public partial class OriginalTimetableV2Page : ContentPage
{
	public static readonly string NameOfThisClass = nameof(OriginalTimetableV2Page);

	// Tablet >=600pt (Material breakpoint; iPad mini portrait 744pt counts).
	const double TabletBreakpoint = 600;

	readonly OriginalTimetableViewModel _vm;
	double _lastWidth = -1;
	bool _lastIsTablet;

	public ObservableCollection<V2RowItem> Items { get; } = new();

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
	public ICommand CycleMarkerCommand { get; }
	public ICommand ClearMarkerCommand { get; }
	public ICommand OpenMemoCommand { get; }

	public OriginalTimetableV2Page()
	{
		_vm = InstanceManager.OriginalTimetableViewModel;

		CycleMarkerCommand = new Command<string>(OnCycleMarker);
		ClearMarkerCommand = new Command<string>(OnClearMarker);
		// Phase 1 — メモ SwipeItem keeps a valid binding but does nothing.
		// MemoSheet overlay lands in Phase 2.
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
				RebuildItems();
				break;
			case nameof(OriginalTimetableViewModel.MarkersVersion):
				UpdateMarkersInPlace();
				break;
			case nameof(OriginalTimetableViewModel.MemosVersion):
				UpdateMemosInPlace();
				break;
			case nameof(OriginalTimetableViewModel.CurIdxVersion):
				UpdateCurrentInPlace();
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
				ApplyCurrentScaledMetrics(item);
			}
		}
	}

	// Tablet/compact "current emphasis" metrics. Mirrors v2.jsx: current
	// station gets a larger platform tile (70/52pt vs 56/44) and 40pt arrive/
	// depart numerals (vs 24pt). Station/font for the name itself bumps too.
	static void ApplyCurrentScaledMetrics(V2RowItem item)
	{
		if (item.IsCurrent)
		{
			item.TabletPlatformSize = 70;
			item.TabletPlatformFontSize = 32;
			item.TabletTimeFontSize = 32;
			item.TabletStationFontSize = 28;
			item.CompactPlatformSize = 52;
			item.CompactPlatformFontSize = 22;
			item.CompactTimeFontSize = 22;
			item.CompactStationFontSize = 22;
		}
		else
		{
			item.TabletPlatformSize = 56;
			item.TabletPlatformFontSize = 22;
			item.TabletTimeFontSize = 22;
			item.TabletStationFontSize = 22;
			item.CompactPlatformSize = 44;
			item.CompactPlatformFontSize = 16;
			item.CompactTimeFontSize = 14;
			item.CompactStationFontSize = 16;
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
				NoteText = row.Remarks ?? string.Empty,
				Marker = marker,
				HasMemo = hasMemo,
				IsSectionBreakRow = false,
				SectionBreakLabel = string.Empty,
			};
			ApplyDerivedStyling(item);
			ApplyCurrentScaledMetrics(item);
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
}

// V2 card-stack row VM consumed by the CollectionView's DataTemplate.
// ObservableObject so MarkersVersion / MemosVersion / CurIdxVersion can
// mutate visible props in place (preserves CollectionView scroll position).
//
// V2 differs from V1RowItem: no alternating-row striping (cards don't
// stripe), no LimitText (limit info is folded into section-break label),
// and *PlatformSize / *TimeFontSize / *StationFontSize are observable so
// the IsCurrent flip can rescale the platform tile + arrive/depart
// numerals + station name in place without rebuilding the row.
public partial class V2RowItem : ObservableObject
{
	public string Id { get; set; } = string.Empty;
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

	// Per-card emphasis metrics. ApplyCurrentScaledMetrics rewrites these
	// whenever IsCurrent flips, driving the visible Border / platform tile
	// / arrive-depart numerals to grow/shrink without an Items rebuild.
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

	public bool IsNormalRow => !IsSectionBreakRow;
	public bool HasTrackName => !string.IsNullOrEmpty(TrackName);

	const string AutomationIdPrefix = "OriginalTimetable.V2.Row.";
	public string RowAutomationId => $"{AutomationIdPrefix}{Id}";
	public string MarkerAutomationId => $"{AutomationIdPrefix}{Id}.Marker";
	public string MemoAutomationId => $"{AutomationIdPrefix}{Id}.Memo";
	public string ClearAutomationId => $"{AutomationIdPrefix}{Id}.Clear";
	public string MarkerBadgeAutomationId => $"{AutomationIdPrefix}{Id}.MarkerBadge";

	public static V2RowItem SectionBreak(string id, string label) => new()
	{
		Id = id,
		IsSectionBreakRow = true,
		SectionBreakLabel = label,
	};
}
