using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;

using TR.Maui.AnchorPopover;

using TRViS.IO.Models;
using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS.OriginalTimetable;

// V6 Bold Editorial — 独自時刻表ページ骨格 (Phase 1: tablet ≥600pt + compact <600pt).
//
// 構造は V1 と同じ CollectionView + DataTemplate + SwipeView (iPad ApplyStyleSheets
// NRE 回避のため imperative tree mutation はしない)。違いは画面分割で、
//
//   - Masthead (depot / route / date)
//   - Train stripe (full-bleed accent: 列車番号 / CARS / MAX / DEST)
//   - Past chips (現在駅より前の駅を横スクロール chip 表示)
//   - Current big block (現在駅を巨大表示 + 着/発/番線; SwipeView でマーカー/メモ/クリア)
//   - Upcoming list (V1 と同じ CollectionView; two-digit counter + double-rule
//     section break + SwipeItem)
//
// CurIdx が変わると past/current/upcoming の split がシフトするため、V1 の
// UpdateCurrentInPlace と違い CurIdxVersion 変化時は丸ごと RebuildItems する。
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

	// Page-level row-id of the current station, used as CommandParameter for the
	// big-block SwipeItems (the current row isn't in Items, so V1's per-item Id
	// binding can't reach it).
	public string CurrentRowId { get; private set; } = string.Empty;

	public bool HasActiveTrain { get; private set; }
	public bool HasNoActiveTrain => !HasActiveTrain;
	public bool HasNoPastChips => PastChips.Count == 0;

	public ICommand ClearMarkerCommand { get; }
	public ICommand OpenMemoCommand { get; }
	public ICommand OpenMarkerPopoverFromSwipeCommand { get; }

	public OriginalTimetableV6Page()
	{
		_vm = InstanceManager.OriginalTimetableViewModel;

		ClearMarkerCommand = new Command<string>(OnClearMarker);
		OpenMemoCommand = new Command<string>(OnOpenMemo);
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
			case nameof(OriginalTimetableViewModel.CurIdxVersion):
				RebuildItems();
				break;
			// 以下は upcoming 行の中だけで完結するので in-place で OK。
			case nameof(OriginalTimetableViewModel.MarkersVersion):
				UpdateMarkersInPlace();
				break;
			case nameof(OriginalTimetableViewModel.MemosVersion):
				UpdateMemosInPlace();
				break;
			case nameof(OriginalTimetableViewModel.NoteOpenVersion):
				UpdateNoteOpenInPlace();
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

		if (train is null || train.Rows is null || train.Rows.Length == 0)
		{
			RaiseAllChanged();
			return;
		}

		int curOrigIdx = _vm.GetCurIdxOverride(train.Id) ?? 0;
		// Clamp to last row index so "all past" maps to the last row being current.
		curOrigIdx = Math.Clamp(curOrigIdx, 0, train.Rows.Length - 1);

		bool showPasses = _vm.ShowPasses;

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
			Items.Add(item);

			prev = row;
		}

		RaiseAllChanged();
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
		// Phase 1: memo sheet overlay is V1-only. Stub: do nothing.
		// Phase 2 will reuse the same overlay-toggle pattern as V1.
		_ = rowId;
	}

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
		if (border.BindingContext is not V6RowItem item)
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
			// Popover failures shouldn't crash the page.
		}
	}
}

// Past chip view-model. Read-only after add (PastChips is rebuilt wholesale in
// RebuildItems), so no INPC needed.
public class V6PastChipItem
{
	public string Id { get; set; } = string.Empty;
	public string StationName { get; set; } = string.Empty;

	public string ChipAutomationId => $"OriginalTimetable.V6.PastChip.{Id}";
}

// Upcoming-list row view-model. Mirrors V1RowItem's ObservableObject pattern so
// MarkersVersion / MemosVersion / NoteOpenVersion can mutate visible props in
// place without touching Items (preserves CollectionView scroll position). New
// V6-only props: CounterText (two-digit upcoming sequence number).
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

	public bool IsNormalRow => !IsSectionBreakRow;
	public bool HasTrackName => !string.IsNullOrEmpty(TrackName);

	const string AutomationIdPrefix = "OriginalTimetable.V6.Row.";
	public string RowAutomationId => $"{AutomationIdPrefix}{Id}";
	public string MarkerAutomationId => $"{AutomationIdPrefix}{Id}.Marker";
	public string MemoAutomationId => $"{AutomationIdPrefix}{Id}.Memo";
	public string ClearAutomationId => $"{AutomationIdPrefix}{Id}.Clear";
	public string MarkerBadgeAutomationId => $"{AutomationIdPrefix}{Id}.MarkerBadge";

	public static V6RowItem SectionBreak(string id, string label) => new()
	{
		Id = id,
		IsSectionBreakRow = true,
		SectionBreakLabel = label,
	};
}
