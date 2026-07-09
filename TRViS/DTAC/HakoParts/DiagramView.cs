using System.ComponentModel;

using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

using TRViS.Controls;
using TRViS.DTAC.Logic.Layout;
using TRViS.IO.Models;
using TRViS.Services;
using TRViS.Utils;

namespace TRViS.DTAC.HakoParts;

/// <summary>
/// Diagram-style ("ハコ図") rendering of the Hako tab for wide (tablet) screens.
/// All layout math (grid line positions, per-train line spans, row placement) comes from
/// <see cref="HakoDiagramLayoutCalculator"/> — this view only turns that data into visuals.
/// </summary>
public class DiagramView : Grid
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	public const double RowHeight = 90;
	public const double ButtonWidth = 172;
	public const double ButtonHeight = 32;

	// Gap between the button's bottom edge and its row's line.
	const double ButtonToLineGap = 18;

	// Short vertical tick at each route line end so 始発/終着 read at a glance: it points up at the
	// departure end, down at the arrival end.
	const double EndpointTickLength = 8;

	// --- Boundary (発着) time units drawn at each train's departure/arrival station ---------------
	// Font sizes: HH:MM at 16, the trailing SS at 10. The unit's width/height/nudge below are sized
	// to these, so they must move together if the font sizes change.
	const double TimeFontSize_HHMM = 16;
	const double TimeFontSize_SS = 10;
	// Reserved width of the opening-bracket slot. Kept even when no bracket is drawn so the HH:MM
	// (and therefore the colon) sit at the same x whether or not the time is bracketed.
	const double TimeBracketSlotWidth = 13;
	// Fixed overall size of one time unit. The closing bracket slides inward when there is no SS,
	// but the unit's own footprint never changes (spec: 時刻ユニット全体のサイズは変わらない), so an
	// arrival time (drawn above the line) and a departure time (below) always share one colon x.
	const double TimeUnitWidth = 98;
	const double TimeUnitHeight = 26;
	// Vertical clearance between the route line and the nearest edge of a time unit. The two sides
	// differ because the font's ascent padding sits above the glyph: a top-aligned (below-line)
	// unit would otherwise read as farther from the line than a bottom-aligned (above-line) one, so
	// the below value is pulled up (negative) to visually match.
	const double TimeToLineGapAbove = 1;
	const double TimeToLineGapBelow = -2;
	// Nudge each unit one character-width toward its station (inboard). The unit reserves an
	// (often empty) bracket slot on its station-facing side, which otherwise leaves a full
	// character of dead space between the visible time and the line; this pulls it back in so the
	// time reads as sitting one blank line out, not one line plus a bracket's width.
	const double TimeInwardNudge = 12;

	// Extra headroom (beyond each row's normal half-height gap above its own line) added once at
	// the very top, so the topmost row's line sits below the top of the (scrollable) diagram
	// content by the sticky header's own height, instead of just RowHeight/2 — every row after it
	// stays RowHeight apart.
	const double TopPadding = DiagramHeaderView.HeaderHeight - (RowHeight / 2);

	/// <summary>
	/// Horizontal distance (device-independent pixels) between two adjacent grid lines. Fixed per
	/// line — but the *number* of lines (<see cref="GridLineCount"/>) grows with the available
	/// width, so the diagram's overall width adapts to the container (see <see cref="GridWidth"/>).
	/// </summary>
	public const double LineSpacing = HakoDiagramLayoutCalculator.DefaultLineSpacing;

	static readonly AppThemeGenericsBindingExtension<Brush> SeparatorLineBrush = DTACElementStyles.SeparatorLineColor.ToBrushTheme();

	readonly AbsoluteLayout _canvas = new();

	IReadOnlyList<TrainData> _trains = [];
	IReadOnlyList<HakoDiagramLayoutCalculator.StationColumn> _columns = [];
	IReadOnlyList<HakoDiagramLayoutCalculator.TrainSegment> _segments = [];

	readonly Dictionary<string, ToggleButton> _buttonByTrainId = [];
	ToggleButton? _selectedButton;

	int _gridLineCount = HakoDiagramLayoutCalculator.MinGridLineCount;

	/// <summary>
	/// Number of vertical dashed grid lines to draw (including the two edge lines). Grows with the
	/// available width so the diagram can fill wider screens (and be shown on narrower ones); pushed
	/// in from <see cref="Hako"/> via <see cref="HakoDiagramLayoutCalculator.CalculateGridLineCount"/>.
	/// Changing it re-positions every station column (their <see cref="StationColumns"/>
	/// <c>LineIndex</c> depends on this) and re-lays-out the diagram.
	/// </summary>
	public int GridLineCount
	{
		get => _gridLineCount;
		set
		{
			if (_gridLineCount == value)
				return;

			_gridLineCount = value;
			RebuildColumnsAndSegments();
			RelayoutAndNotifyOnMainThread();
		}
	}

	/// <summary>
	/// Total width (device-independent pixels) of the diagram's grid area — spans from the first to
	/// the last of the <see cref="GridLineCount"/> lines.
	/// </summary>
	public double GridWidth => Math.Max(0, (_gridLineCount - 1) * LineSpacing);

	/// <summary>
	/// Number of turn-back stations in the currently loaded work. Callers use this together
	/// with the available width to decide whether diagram mode should be shown at all
	/// (see <see cref="HakoDiagramLayoutCalculator.ShouldUseDiagramLayout"/>). Depends only on the
	/// data, not on <see cref="GridLineCount"/>.
	/// </summary>
	public int TurnBackStationCount => _columns.Count;

	/// <summary>
	/// The turn-back station columns for the currently loaded work, positioned in the same
	/// fixed-pixel coordinate space as this view's own grid lines. Exposed so a sticky header
	/// (<see cref="DiagramHeaderView"/>) placed outside this view's scrollable container can
	/// render the same station names aligned with the same lines.
	/// </summary>
	public IReadOnlyList<HakoDiagramLayoutCalculator.StationColumn> StationColumns => _columns;

	/// <summary>
	/// Raised whenever the underlying train list is rebuilt (work switch, data reload).
	/// </summary>
	public event EventHandler? DataChanged;

	double _viewportHeight;

	/// <summary>
	/// Height of the enclosing <see cref="ScrollView"/>'s visible viewport. The background grid
	/// lines are drawn to at least this height (see <see cref="Relayout"/>) so that a work with
	/// few trains still shows the dashed lines running all the way down to the bottom of the
	/// screen, instead of stopping right after the last train row.
	/// </summary>
	public double ViewportHeight
	{
		get => _viewportHeight;
		set
		{
			if (_viewportHeight == value)
				return;

			_viewportHeight = value;
			Relayout();
		}
	}

	public DiagramView()
	{
		logger.Debug("Creating...");

		_canvas.HorizontalOptions = LayoutOptions.Center;
		_canvas.VerticalOptions = LayoutOptions.Start;
		Children.Add(_canvas);

		InstanceManager.AppViewModel.PropertyChanged += OnAppViewModelPropertyChanged;

		RefreshTrains(InstanceManager.AppViewModel.OrderedTrainDataList);

		logger.Debug("Created");
	}

	void OnAppViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (!MainThread.IsMainThread)
		{
			MainThread.BeginInvokeOnMainThread(() => OnAppViewModelPropertyChanged(sender, e));
			return;
		}

		try
		{
			if (e.PropertyName == nameof(InstanceManager.AppViewModel.SelectedWork) ||
				e.PropertyName == nameof(InstanceManager.AppViewModel.OrderedTrainDataList))
			{
				RefreshTrains(InstanceManager.AppViewModel.OrderedTrainDataList);
			}
			else if (e.PropertyName == nameof(InstanceManager.AppViewModel.SelectedTrainData))
			{
				SyncSelection(InstanceManager.AppViewModel.SelectedTrainData);
			}
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "DiagramView.OnAppViewModelPropertyChanged");
			Util.ExitWithAlertAsync(ex);
		}
	}

	void RefreshTrains(IReadOnlyList<TrainData>? trains)
	{
		logger.Debug("RefreshTrains: count={0}", trains?.Count ?? 0);

		_trains = trains ?? [];
		RebuildColumnsAndSegments();
		RelayoutAndNotifyOnMainThread();
	}

	void RebuildColumnsAndSegments()
	{
		_columns = HakoDiagramLayoutCalculator.BuildStationColumns(_trains, _gridLineCount);
		_segments = HakoDiagramLayoutCalculator.BuildTrainSegments(_trains, _columns);
	}

	void RelayoutAndNotifyOnMainThread()
	{
		// OrderedTrainDataList/SelectedWork changes can be raised from a background (loader) thread;
		// Relayout() mutates _canvas.Children and must run on the main thread.
		MainThread.BeginInvokeOnMainThread(() =>
		{
			try
			{
				Relayout();
				DataChanged?.Invoke(this, EventArgs.Empty);
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "DiagramView.RefreshTrains");
				Util.ExitWithAlertAsync(ex);
			}
		});
	}

	void Relayout()
	{
		_canvas.Children.Clear();
		_buttonByTrainId.Clear();
		_selectedButton = null;

		if (_trains.Count == 0)
		{
			_canvas.WidthRequest = 0;
			_canvas.HeightRequest = 0;
			return;
		}

		double height = Math.Max(TopPadding + (_trains.Count * RowHeight), _viewportHeight);
		_canvas.WidthRequest = GridWidth;
		_canvas.HeightRequest = height;

		AddBackgroundGridLines(height);
		AddTrainSegments();

		SyncSelection(InstanceManager.AppViewModel.SelectedTrainData);
	}

	static double XOf(double lineIndex)
		=> lineIndex * LineSpacing;

	void PlaceAbsolute(View view, double x, double y, double width, double height)
	{
		AbsoluteLayout.SetLayoutBounds(view, new Rect(x, y, width, height));
		AbsoluteLayout.SetLayoutFlags(view, AbsoluteLayoutFlags.None);
		_canvas.Children.Add(view);
	}

	void AddBackgroundGridLines(double height)
	{
		// Every line gets its own frame centered on its x position (rather than all lines sharing
		// one (0, 0, GridWidth, height) frame) so the two edge lines (i=0 at x=0, i=GridLineCount-1
		// at x=GridWidth) don't have their stroke sitting exactly on the frame's boundary — a shape's
		// stroke straddling its own frame edge gets half clipped away, which was silently dropping
		// one of the lines. The same trick is applied to the top/bottom (frameVerticalPad) so the
		// line's very top and bottom pixels aren't clipped either, letting it reach all the way down.
		const double frameHalfWidth = 4;
		const double frameVerticalPad = 4;

		for (int i = 0; i < _gridLineCount; i++)
		{
			double x = XOf(i);
			Line line = new()
			{
				X1 = frameHalfWidth,
				X2 = frameHalfWidth,
				Y1 = frameVerticalPad,
				Y2 = frameVerticalPad + height,
				StrokeThickness = 1,
				StrokeDashArray = [4, 4],
				// This shape's frame is much larger than its painted stroke, and MAUI hit-tests
				// by bounding box rather than by painted pixel — without this, the frame silently
				// swallows taps meant for whatever sits underneath it.
				InputTransparent = true,
			};
			SeparatorLineBrush.Apply(line, Line.StrokeProperty);

			PlaceAbsolute(line, x - frameHalfWidth, -frameVerticalPad, frameHalfWidth * 2, height + (frameVerticalPad * 2));
		}
	}

	void AddTrainSegments()
	{
		foreach (HakoDiagramLayoutCalculator.TrainSegment segment in _segments)
		{
			TrainData? train = _trains.FirstOrDefault(t => t.Id == segment.TrainId);
			if (train is null)
			{
				logger.Debug("train not found for segment.TrainId: {0}", segment.TrainId);
				continue;
			}

			double startX = XOf(segment.StartLineIndex);
			double endX = XOf(segment.EndLineIndex);
			double lineY = TopPadding + (segment.RowIndex * RowHeight) + (RowHeight / 2);

			AddRouteLine(startX, endX, lineY);
			// startX/endX are the departure/arrival ends regardless of travel direction, so the up
			// tick always marks 始発 and the down tick 終着.
			AddEndpointTick(startX, lineY, up: true);
			AddEndpointTick(endX, lineY, up: false);
			AddTrainNumberButton(train, segment, startX, lineY);
			AddBoundaryTimes(train, segment, lineY);

			// TODO: segment.ConnectorFromPreviousTrain / DepartureMark / ArrivalMark are
			// prepared for future use but their concrete values and rendering are not yet defined.
		}
	}

	void AddRouteLine(double startX, double endX, double lineY)
	{
		Line routeLine = new()
		{
			X1 = startX,
			X2 = endX,
			Y1 = lineY,
			Y2 = lineY,
			StrokeThickness = 4,
			// Frame grows with lineY and is anchored at (0,0) (see PlaceAbsolute below), so a
			// lower row's route line frame fully encloses the button frames of every row above
			// it. Without this, the (invisible, bounding-box-hit-tested) frame swallows taps
			// meant for those earlier trains' ToggleButtons.
			InputTransparent = true,
		};
		DTACElementStyles.ForegroundBlackWhiteBrush.Apply(routeLine, Line.StrokeProperty);

		// Frame just needs to bound the line's own points (see PlaceAbsolute) —
		// unlike the full-canvas grid lines, a route line only spans a sub-range.
		double frameWidth = Math.Max(startX, endX) + routeLine.StrokeThickness;
		double frameHeight = lineY + routeLine.StrokeThickness;
		PlaceAbsolute(routeLine, 0, 0, frameWidth, frameHeight);
	}

	void AddEndpointTick(double x, double lineY, bool up)
	{
		double y2 = up ? lineY - EndpointTickLength : lineY + EndpointTickLength;
		Line tick = new()
		{
			X1 = x,
			X2 = x,
			Y1 = lineY,
			Y2 = y2,
			StrokeThickness = 4,
			// Same reasoning as AddRouteLine: (0,0)-anchored frame, kept out of hit-testing.
			InputTransparent = true,
		};
		DTACElementStyles.ForegroundBlackWhiteBrush.Apply(tick, Line.StrokeProperty);

		double frameWidth = x + tick.StrokeThickness;
		double frameHeight = Math.Max(lineY, y2) + tick.StrokeThickness;
		PlaceAbsolute(tick, 0, 0, frameWidth, frameHeight);
	}

	void AddTrainNumberButton(TrainData train, HakoDiagramLayoutCalculator.TrainSegment segment, double startX, double lineY)
	{
		Label numberLabel = DTACElementStyles.LabelStyle<Label>();
		numberLabel.Text = train.TrainNumber;
		numberLabel.FontAttributes = FontAttributes.Bold;
		numberLabel.Margin = new(0);

		Border border = new()
		{
			Padding = new(8, 2),
			StrokeThickness = 1,
			StrokeShape = new RoundRectangle { CornerRadius = 6 },
			Shadow = DTACElementStyles.DefaultShadow,
			Content = numberLabel,
		};
		DTACElementStyles.DefaultBGColor.Apply(border, Border.BackgroundColorProperty);
		ApplyButtonBorderStyle(border, isSelected: false);

		ToggleButton toggleButton = new()
		{
			Content = border,
			IsRadio = true,
		};
		toggleButton.IsCheckedChanged += (_, e) =>
		{
			try
			{
				ApplyButtonBorderStyle(border, e.NewValue);

				if (!e.NewValue)
					return;

				SelectButton(toggleButton);
				InstanceManager.AppViewModel.SelectedTrainData = train;
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "DiagramView.ToggleButton.IsCheckedChanged");
				Util.ExitWithAlertAsync(ex);
			}
		};

		// 始発駅(StartLineIndex側)の線と、その隣の線(進行方向側)のちょうど中央に、ボタンの
		// 始発駅側の端(右向きなら左端、左向きなら右端)が来るように配置する
		// (ボタンの中心ではなく、端を中央に合わせる)
		double buttonX = segment.IsLeftToRight
			? startX + (LineSpacing / 2)
			: startX - (LineSpacing / 2) - ButtonWidth;
		double buttonY = lineY - ButtonHeight - ButtonToLineGap;

		PlaceAbsolute(toggleButton, buttonX, buttonY, ButtonWidth, ButtonHeight);

		_buttonByTrainId[train.Id] = toggleButton;

#if UI_TEST
		if (train.TrainNumber is not null)
			toggleButton.AutomationId = $"DTAC.HakoDiagram.{train.TrainNumber}";
#endif
	}

	// Draws the arrival/departure times at a train's start (始発) and terminal (終着) stations.
	// The route line is treated as running infinitely left/right; each time unit sits one grid line
	// *outboard* of its endpoint (behind the direction of travel at the departure station, ahead of
	// it at the arrival station), with its station-facing edge on that line. At both stations the
	// arrival time is drawn above the line and the departure time below it.
	void AddBoundaryTimes(TrainData train, HakoDiagramLayoutCalculator.TrainSegment segment, double lineY)
	{
		TimetableRow? firstRow = train.Rows?.FirstOrDefault(static r => !r.IsInfoRow);
		TimetableRow? lastRow = train.Rows?.LastOrDefault(static r => !r.IsInfoRow);

		// Departure station: outboard is behind the direction of travel — left for a rightward train.
		double depAnchorX = XOf(segment.StartLineIndex + (segment.IsLeftToRight ? -1 : 1));
		AddTimePair(firstRow, depAnchorX, unitOnLeft: segment.IsLeftToRight, lineY);

		// Arrival station: outboard is ahead of the direction of travel — right for a rightward train.
		double arrAnchorX = XOf(segment.EndLineIndex + (segment.IsLeftToRight ? 1 : -1));
		AddTimePair(lastRow, arrAnchorX, unitOnLeft: !segment.IsLeftToRight, lineY);
	}

	void AddTimePair(TimetableRow? row, double anchorX, bool unitOnLeft, double lineY)
	{
		if (row is null)
			return;

		// The anchor is the unit's station-facing edge; when the unit sits to the left of the station
		// that is its right edge, so shift the box left by its own width. The inward nudge then pulls
		// the unit a character-width back toward the station (left side moves right, right side left).
		double boxLeft = unitOnLeft
			? anchorX - TimeUnitWidth + TimeInwardNudge
			: anchorX - TimeInwardNudge;

		// The arrival unit sits above the line and hugs it from below (text bottom-aligned in its box);
		// the departure unit sits below the line and hugs it from above (text top-aligned). Without the
		// per-side alignment the below-line text would fall to the bottom of its box, a whole unit
		// height away from the line.
		if (HasRenderableTime(row.ArriveTime))
			PlaceAbsolute(CreateTimeUnit(row.ArriveTime!, row.HasBracket, below: false), boxLeft, lineY - TimeToLineGapAbove - TimeUnitHeight, TimeUnitWidth, TimeUnitHeight);

		if (HasRenderableTime(row.DepartureTime))
			PlaceAbsolute(CreateTimeUnit(row.DepartureTime!, row.HasBracket, below: true), boxLeft, lineY + TimeToLineGapBelow, TimeUnitWidth, TimeUnitHeight);
	}

	static bool HasRenderableTime(TimeData? time)
		=> time is not null && time.Hour is not null && time.Minute is not null;

	// One fixed-size time unit: [ ( ] HH:MM [SS] [ ) ]. The opening-bracket slot is reserved even
	// when empty (so the colon never moves), the SS is rendered smaller, and the closing bracket
	// packs immediately after whatever precedes it (SS if present, else MM) while the unit's overall
	// footprint stays constant.
	static View CreateTimeUnit(TimeData time, bool hasBracket, bool below)
	{
		HorizontalStackLayout stack = new()
		{
			Spacing = 0,
			HorizontalOptions = LayoutOptions.Start,
			// Pin the content to the line-facing edge of the box: top for a below-line (departure)
			// unit, bottom for an above-line (arrival) unit.
			VerticalOptions = below ? LayoutOptions.Start : LayoutOptions.End,
			InputTransparent = true,
		};

		Label open = TimeGlyphLabel(hasBracket ? "(" : string.Empty, TimeFontSize_HHMM);
		open.WidthRequest = TimeBracketSlotWidth;
		open.HorizontalTextAlignment = TextAlignment.End;
		stack.Add(open);

		stack.Add(TimeGlyphLabel($"{time.Hour:00}:{time.Minute:00}", TimeFontSize_HHMM));

		stack.Add(TimeGlyphLabel(time.Second?.ToString("00") ?? string.Empty, TimeFontSize_SS));

		if (hasBracket)
			stack.Add(TimeGlyphLabel(")", TimeFontSize_HHMM));

		return new ContentView
		{
			WidthRequest = TimeUnitWidth,
			HeightRequest = TimeUnitHeight,
			Content = stack,
			// The unit's reserved/trailing space is invisible but would otherwise be hit-tested by
			// bounding box and swallow taps meant for the train buttons underneath (same lesson the
			// grid/route lines carry above).
			InputTransparent = true,
		};
	}

	static Label TimeGlyphLabel(string text, double fontSize)
	{
		Label v = DTACElementStyles.LabelStyle<Label>();
		v.Text = text;
		v.FontSize = fontSize;
		// Match the timetable page's time cell: Helvetica, bold — brackets included.
		v.FontFamily = DTACElementStyles.TimetableNumFontFamily;
		v.FontAttributes = FontAttributes.Bold;
		v.Margin = new(0);
		v.Padding = new(0);
		v.LineBreakMode = LineBreakMode.NoWrap;
		// Bottom-align so the smaller SS sits on the same baseline-ish line as the HH:MM.
		v.VerticalOptions = LayoutOptions.End;
		v.VerticalTextAlignment = TextAlignment.End;
		v.InputTransparent = true;
		return v;
	}

	static void ApplyButtonBorderStyle(Border border, bool isSelected)
	{
		if (isSelected)
		{
			DTACElementStyles.DefaultGreen.Apply(border, Border.StrokeProperty);
			border.StrokeThickness = 3;
		}
		else
		{
			border.Stroke = new SolidColorBrush(new Color(0.6f, 0.6f, 0.6f));
			border.StrokeThickness = 1;
		}
	}

	void SelectButton(ToggleButton button)
	{
		if (_selectedButton == button)
			return;

		if (_selectedButton is not null)
			_selectedButton.IsChecked = false;

		_selectedButton = button;
	}

	void SyncSelection(TrainData? selectedTrainData)
	{
		if (selectedTrainData is null || !_buttonByTrainId.TryGetValue(selectedTrainData.Id, out ToggleButton? button))
		{
			if (_selectedButton is not null)
				_selectedButton.IsChecked = false;
			_selectedButton = null;
			return;
		}

		if (_selectedButton == button)
			return;

		SelectButton(button);
		button.IsChecked = true;
	}
}
