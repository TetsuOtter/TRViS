using TRViS.Services;

namespace TRViS.DTAC;

public class DTACColumnDefinitionsProvider
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	private const double TRAIN_INFO_BEFORE_DEPARTURE_HEADER_COLUMN_WIDTH_NARROW = 48;
	private const double RUN_TIME_COLUMN_WIDTH_NARROW = 4;
	private const double RUN_TIME_COLUMN_WIDTH = 60;
	private const double STA_NAME_COLUMN_WIDTH_NARROW = 96;
	private const double STA_NAME_COLUMN_WIDTH = 140;
	private const double ARR_DEP_COLUMN_WIDTH_MINI6 = 134;
	private const double ARR_DEP_COLUMN_WIDTH_NARROW = 110;
	private const double ARR_DEP_COLUMN_WIDTH = 140;
	private const double TRACK_NUMBER_COLUMN_WIDTH = 60;
	private const double TRACK_NUMBER_COLUMN_WIDTH_NARROW = 48;
	private const double SPEED_LIMIT_COLUMN_WIDTH = 60;
	// private const double REMARKS_COLUMN_WIDTH = 104;
	private const double MARKER_COLUMN_WIDTH = 64;

	private ColumnDefinition TrainInfoBeforeDepartureHeaderColumnDefinition { get; } = new(RUN_TIME_COLUMN_WIDTH);
	private ColumnDefinition RunTimeColumnDefinition { get; } = new(RUN_TIME_COLUMN_WIDTH);
	private ColumnDefinition StationNameColumnDefinition { get; } = new(STA_NAME_COLUMN_WIDTH);
	private ColumnDefinition ArrivalDepartureColumnDefinition { get; } = new(ARR_DEP_COLUMN_WIDTH);
	private ColumnDefinition TrackNumberColumnDefinition { get; } = new(TRACK_NUMBER_COLUMN_WIDTH);
	private ColumnDefinition SpeedLimitColumnDefinition { get; } = new(SPEED_LIMIT_COLUMN_WIDTH);
	private ColumnDefinition RemarksColumnDefinition { get; } = new(new(1, GridUnitType.Star));
	private ColumnDefinition MarkerColumnDefinition { get; } = new(MARKER_COLUMN_WIDTH);

	public ColumnDefinitionCollection TrainInfoBeforeDepartureColumnDefinitions { get; }
	public ColumnDefinitionCollection TimetableRowColumnDefinitions { get; }

	public bool IsRunTimeColumnVisible => GetIsRunTimeColumnVisible(viewWidthMode);
	public bool IsStaNameColumnNarrow => GetIsStaNameColumnNarrow(viewWidthMode);
	public bool IsTrackNameColumnNarrow => GetIsTrackNameColumnNarrow(viewWidthMode);
	public bool IsSpeedLimitColumnVisible => GetIsSpeedLimitColumnVisible(viewWidthMode);
	public bool IsRemarksColumnVisible => GetIsRemarksColumnVisible(viewWidthMode);
	public bool IsMarkerColumnVisible => GetIsMarkerColumnVisible(viewWidthMode);

	public DTACColumnDefinitionsProvider()
	{
		TrainInfoBeforeDepartureColumnDefinitions =
		[
			TrainInfoBeforeDepartureHeaderColumnDefinition,
			new(new(1, GridUnitType.Star)),
		];
		TimetableRowColumnDefinitions =
		[
			RunTimeColumnDefinition,
			StationNameColumnDefinition,
			ArrivalDepartureColumnDefinition, // arrival
			ArrivalDepartureColumnDefinition, // departure
			TrackNumberColumnDefinition,
			SpeedLimitColumnDefinition,
			RemarksColumnDefinition,
			MarkerColumnDefinition
		];
	}

	private ViewWidthMode viewWidthMode = ViewWidthMode.IPAD_MINI_2_3_4_5_V;
	public event EventHandler? ViewWidthModeChanged;
	private enum ViewWidthMode
	{
		NARROW = 0,

		IPHONE_SE_V = 320,
		IPHONE_6_7_8_V = 375,
		IPHONE_6_7_8_PLUS_V = 414,

		IPHONE_SE_H = 568,
		IPHONE_6_7_8_H = 667,
		IPHONE_6_7_8_PLUS_H = 736,

		// (左右の余白部分を含めたサイズ)
		IPAD_MINI_6_V = 744 + 12,
		IPAD_MINI_2_3_4_5_V = 768,
	}
	private static bool GetIsRunTimeColumnVisible(in ViewWidthMode currentMode) => SameOrWide(ViewWidthMode.IPAD_MINI_6_V, currentMode);
	private static bool GetIsStaNameColumnNarrow(in ViewWidthMode currentMode) => SameOrNarrow(ViewWidthMode.IPHONE_6_7_8_PLUS_V, currentMode);
	private static bool GetIsTrackNameColumnNarrow(in ViewWidthMode currentMode) => SameOrNarrow(ViewWidthMode.IPHONE_6_7_8_PLUS_V, currentMode);
	private static bool GetIsSpeedLimitColumnVisible(in ViewWidthMode currentMode) => SameOrWide(ViewWidthMode.IPHONE_6_7_8_PLUS_H, currentMode);
	private static bool GetIsRemarksColumnVisible(in ViewWidthMode currentMode) => SameOrWide(ViewWidthMode.IPHONE_6_7_8_H, currentMode);
	private static bool GetIsMarkerColumnVisible(in ViewWidthMode currentMode) => SameOrWide(ViewWidthMode.IPHONE_6_7_8_H, currentMode);
	public void OnViewWidthChanged(double newWidth)
	{
		ViewWidthMode newMode = newWidth switch
		{
			>= 768 => ViewWidthMode.IPAD_MINI_2_3_4_5_V,
			>= 756 => ViewWidthMode.IPAD_MINI_6_V,
			>= 736 => ViewWidthMode.IPHONE_6_7_8_PLUS_H,
			>= 667 => ViewWidthMode.IPHONE_6_7_8_H,
			>= 568 => ViewWidthMode.IPHONE_SE_H,
			>= 414 => ViewWidthMode.IPHONE_6_7_8_PLUS_V,
			>= 375 => ViewWidthMode.IPHONE_6_7_8_V,
			>= 320 => ViewWidthMode.IPHONE_SE_V,
			_ => ViewWidthMode.NARROW,
		};
		if (newMode == viewWidthMode)
		{
			logger.Debug("ViewWidthMode did not change: {0} ({1}px)", newMode, newWidth);
			return;
		}

		logger.Debug("ViewWidthMode changed from {0} to {1} ({2}px)", viewWidthMode, newMode, newWidth);
		UpdateTrainInfoBeforeDepartureHeaderColumnDefinition(TrainInfoBeforeDepartureHeaderColumnDefinition, newMode);
		UpdateRunTimeColumnDefinition(RunTimeColumnDefinition, newMode);
		UpdateStaNameColumnDefinition(StationNameColumnDefinition, newMode);
		UpdateArrDepTimeColumnDefinition(ArrivalDepartureColumnDefinition, newMode);
		UpdateTrackNumberColumnDefinition(TrackNumberColumnDefinition, newMode);
		UpdateSpeedLimitColumnDefinition(SpeedLimitColumnDefinition, newMode);
		UpdateRemarksColumnDefinition(RemarksColumnDefinition, newMode);
		UpdateMarkerColumnDefinition(MarkerColumnDefinition, newMode);
		viewWidthMode = newMode;
		ViewWidthModeChanged?.Invoke(this, EventArgs.Empty);
	}

	private static void UpdateTrainInfoBeforeDepartureHeaderColumnDefinition(
		ColumnDefinition columnDefinition,
		in ViewWidthMode currentMode
	)
	{
		if (GetIsRunTimeColumnVisible(currentMode))
		{
			columnDefinition.Width = RUN_TIME_COLUMN_WIDTH;
		}
		else
		{
			columnDefinition.Width = TRAIN_INFO_BEFORE_DEPARTURE_HEADER_COLUMN_WIDTH_NARROW;
		}
	}
	private static void UpdateRunTimeColumnDefinition(
		ColumnDefinition columnDefinition,
		in ViewWidthMode currentMode
	)
	{
		if (GetIsRunTimeColumnVisible(currentMode))
		{
			columnDefinition.Width = RUN_TIME_COLUMN_WIDTH;
		}
		else
		{
			columnDefinition.Width = RUN_TIME_COLUMN_WIDTH_NARROW;
		}
	}
	private static void UpdateStaNameColumnDefinition(
		ColumnDefinition columnDefinition,
		in ViewWidthMode currentMode
	)
	{
		double width;
		if (GetIsStaNameColumnNarrow(currentMode))
		{
			width = STA_NAME_COLUMN_WIDTH_NARROW;
		}
		else
		{
			width = STA_NAME_COLUMN_WIDTH;
		}
		columnDefinition.Width = new(width, GetIsRemarksColumnVisible(currentMode) ? GridUnitType.Absolute : GridUnitType.Star);
	}
	private static void UpdateArrDepTimeColumnDefinition(
		ColumnDefinition columnDefinition,
		in ViewWidthMode currentMode
	)
	{
		switch (currentMode)
		{
			case ViewWidthMode.NARROW:
			case ViewWidthMode.IPHONE_SE_V:
			case ViewWidthMode.IPHONE_6_7_8_V:
			case ViewWidthMode.IPHONE_6_7_8_PLUS_V:
				columnDefinition.Width = ARR_DEP_COLUMN_WIDTH_NARROW;
				return;

			case ViewWidthMode.IPHONE_SE_H:
			case ViewWidthMode.IPHONE_6_7_8_H:
			case ViewWidthMode.IPHONE_6_7_8_PLUS_H:
				columnDefinition.Width = ARR_DEP_COLUMN_WIDTH;
				return;

			case ViewWidthMode.IPAD_MINI_6_V:
				columnDefinition.Width = ARR_DEP_COLUMN_WIDTH_MINI6;
				return;
			case ViewWidthMode.IPAD_MINI_2_3_4_5_V:
				columnDefinition.Width = ARR_DEP_COLUMN_WIDTH;
				return;
		}
	}
	private static void UpdateTrackNumberColumnDefinition(
		ColumnDefinition columnDefinition,
		in ViewWidthMode currentMode
	)
	{
		double width;
		if (GetIsTrackNameColumnNarrow(currentMode))
		{
			width = TRACK_NUMBER_COLUMN_WIDTH_NARROW;
		}
		else
		{
			width = TRACK_NUMBER_COLUMN_WIDTH;
		}
		columnDefinition.Width = new(width, GetIsSpeedLimitColumnVisible(currentMode) ? GridUnitType.Absolute : GridUnitType.Star);
	}
	private static void UpdateSpeedLimitColumnDefinition(
		ColumnDefinition columnDefinition,
		in ViewWidthMode currentMode
	)
	{
		if (GetIsSpeedLimitColumnVisible(currentMode))
		{
			columnDefinition.Width = TRACK_NUMBER_COLUMN_WIDTH;
		}
		else
		{
			columnDefinition.Width = 0;
		}
	}
	private static void UpdateRemarksColumnDefinition(
		ColumnDefinition columnDefinition,
		in ViewWidthMode currentMode
	)
	{
		if (GetIsRemarksColumnVisible(currentMode))
		{
			columnDefinition.Width = new(1, GridUnitType.Star);
		}
		else
		{
			columnDefinition.Width = 0;
		}
	}
	private static void UpdateMarkerColumnDefinition(
		ColumnDefinition columnDefinition,
		in ViewWidthMode currentMode
	)
	{
		if (GetIsMarkerColumnVisible(currentMode))
		{
			columnDefinition.Width = MARKER_COLUMN_WIDTH;
		}
		else
		{
			columnDefinition.Width = 0;
		}
	}

	private bool SameOrWide(in ViewWidthMode mode) => SameOrWide(mode, viewWidthMode);
	private static bool SameOrWide(in ViewWidthMode mode, in ViewWidthMode currentMode) => mode <= currentMode;
	private bool SameOrNarrow(in ViewWidthMode mode) => SameOrNarrow(mode, viewWidthMode);
	private static bool SameOrNarrow(in ViewWidthMode mode, in ViewWidthMode currentMode) => currentMode <= mode;
}
