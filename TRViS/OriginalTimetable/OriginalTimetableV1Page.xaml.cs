using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

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

	public OriginalTimetableV1Page()
	{
		_vm = InstanceManager.OriginalTimetableViewModel;

		CycleMarkerCommand = new Command<string>(OnCycleMarker);
		ClearMarkerCommand = new Command<string>(OnClearMarker);
		OpenMemoCommand = new Command<string>(OnOpenMemo);
		ToggleNoteCommand = new Command<string>(OnToggleNote);

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
			case nameof(OriginalTimetableViewModel.MarkersVersion):
			case nameof(OriginalTimetableViewModel.MemosVersion):
			case nameof(OriginalTimetableViewModel.NoteOpenVersion):
			case nameof(OriginalTimetableViewModel.CurIdxVersion):
				RebuildItems();
				break;
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
		// NRE path that bit the previous V1 implementation).
		TabletGrid.IsVisible = isTablet;
		CompactPlaceholder.IsVisible = !isTablet;
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

		// Phase 1: skip info rows; keep pass rows (ShowPasses tweak comes in
		// Phase 3). Track the *visible* index for striping; CurIdx semantics
		// stay tied to the underlying ActiveTrain.Rows index (matches VM.Advance).
		var visibleRows = new List<(int origIdx, TimetableRow row)>(train.Rows.Length);
		for (int i = 0; i < train.Rows.Length; i++)
		{
			var r = train.Rows[i];
			if (r.IsInfoRow)
				continue;
			visibleRows.Add((i, r));
		}

		int curOrigIdx = _vm.GetCurIdxOverride(train.Id) ?? 0;

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

			var item = new V1RowItem
			{
				Id = row.Id,
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
				Marker = marker,
				HasMemo = hasMemo,
				IsSectionBreakRow = false,
				SectionBreakLabel = string.Empty,
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

	// Phase 1 placeholder — Phase 2 will surface a MemoSheet here.
	void OnOpenMemo(string? rowId)
	{
		_ = rowId;
	}

	void OnToggleNote(string? rowId)
	{
		var train = _vm.ActiveTrain;
		if (train is null || string.IsNullOrEmpty(rowId))
			return;
		_vm.ToggleNote(train.Id, rowId);
	}
}

// Row VM consumed by the CollectionView's DataTemplate. Plain class with
// init-style autoprops; markers/current/striping are mutated *via full
// rebuild* on every VM version bump, not via INPC — Phase 1 keeps things
// simple per the spec (Items.Clear + Items.Add).
public class V1RowItem
{
	public string Id { get; set; } = string.Empty;
	public string StationName { get; set; } = string.Empty;
	public string KanaName { get; set; } = string.Empty;
	public string RunText { get; set; } = string.Empty;
	public string ArriveText { get; set; } = string.Empty;
	public string DepartText { get; set; } = string.Empty;
	public string TrackName { get; set; } = string.Empty;
	public string LimitText { get; set; } = string.Empty;
	public bool IsPass { get; set; }
	public bool IsCurrent { get; set; }
	public bool IsAlternateRow { get; set; }
	public bool HasNote { get; set; }
	public MarkerKind Marker { get; set; } = MarkerKind.None;
	public bool HasMemo { get; set; }
	public bool IsSectionBreakRow { get; set; }
	public string SectionBreakLabel { get; set; } = string.Empty;

	// Derived (filled by ApplyDerivedStyling). Bools drive DataTriggers in XAML
	// so per-row Brush selection stays AppTheme-aware via StaticResource lookups.
	public bool HasMarker { get; set; }
	public bool IsMarkerFlag { get; set; }
	public bool IsMarkerCaution { get; set; }
	public bool IsMarkerStar { get; set; }
	public string MarkerText { get; set; } = string.Empty;

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

	public static V1RowItem SectionBreak(string id, string label) => new()
	{
		Id = id,
		IsSectionBreakRow = true,
		SectionBreakLabel = label,
	};
}
