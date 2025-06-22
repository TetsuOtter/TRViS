using TRViS.Services;

namespace TRViS.DTAC;

public class DTACRowDefinitionsProvider
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	private const double DATE_AND_START_BUTTON_ROW_HEIGHT = 60;
	private const double TRAIN_INFO_HEADER_ROW_HEIGHT = 54;
	private const double TRAIN_INFO_ROW_HEIGHT = 54;
	private const double TRAIN_INFO_BEFORE_DEPARTURE_ROW_HEIGHT = DTACElementStyles.BeforeDeparture_AfterArrive_Height * 2;
	private const double CAR_COUNT_AND_BEFORE_REMARKS_ROW_HEIGHT = 60;
	private const double TIMETABLE_HEADER_ROW_HEIGHT = 60;

	private const double HAKO_PAGE_HEADER_ROW_HEIGHT = 80;

	public const double CONTENT_OTHER_THAN_TIMETABLE_HEIGHT
		= DATE_AND_START_BUTTON_ROW_HEIGHT
		+ TRAIN_INFO_HEADER_ROW_HEIGHT
		+ TRAIN_INFO_ROW_HEIGHT
		+ TRAIN_INFO_BEFORE_DEPARTURE_ROW_HEIGHT
		+ CAR_COUNT_AND_BEFORE_REMARKS_ROW_HEIGHT
		+ TIMETABLE_HEADER_ROW_HEIGHT;

	private RowDefinition DateAndStartButtonRowDefinition { get; } = new(DATE_AND_START_BUTTON_ROW_HEIGHT);
	private RowDefinition TrainInfoHeaderRowDefinition { get; } = new(TRAIN_INFO_HEADER_ROW_HEIGHT);
	private RowDefinition TrainInfoRowDefinition { get; } = new(TRAIN_INFO_ROW_HEIGHT);
	private double TrainInfo_beforeDeparture_RowHeight = TRAIN_INFO_BEFORE_DEPARTURE_ROW_HEIGHT;
	private RowDefinition TrainInfo_BeforeDeparture_RowDefinition { get; } = new(0);
	private RowDefinition CarCountAndBeforeRemarksRowDefinition { get; } = new(CAR_COUNT_AND_BEFORE_REMARKS_ROW_HEIGHT);
	private RowDefinition TimetableHeaderRowDefinition { get; } = new(TIMETABLE_HEADER_ROW_HEIGHT);
	private RowDefinition TimetableContentRowDefinition { get; } = new(new(1, GridUnitType.Star));

	private RowDefinition HakoPageHeaderRowDefinition { get; } = new(HAKO_PAGE_HEADER_ROW_HEIGHT);

	public double ContentOtherThanTimetableHeight =>
		DateAndStartButtonRowDefinition.Height.Value
		+ TrainInfoHeaderRowDefinition.Height.Value
		+ TrainInfoRowDefinition.Height.Value
		+ TrainInfo_beforeDeparture_RowHeight
		+ CarCountAndBeforeRemarksRowDefinition.Height.Value
		+ TimetableHeaderRowDefinition.Height.Value;

	public RowDefinitionCollection HakoPageRowDefinitions { get; }
	public RowDefinitionCollection VerticalStylePageRowDefinitions { get; }

	public DTACRowDefinitionsProvider()
	{
		HakoPageRowDefinitions =
		[
			DateAndStartButtonRowDefinition,
			HakoPageHeaderRowDefinition,
			new(new(1, GridUnitType.Star))
		];

		VerticalStylePageRowDefinitions =
		[
			DateAndStartButtonRowDefinition,
			TrainInfoHeaderRowDefinition,
			TrainInfoRowDefinition,
			TrainInfo_BeforeDeparture_RowDefinition,
			CarCountAndBeforeRemarksRowDefinition,
			TimetableHeaderRowDefinition,
			TimetableContentRowDefinition
		];
	}

	private enum ViewHeightMode
	{
		Normal,
		Low,
	}
	private ViewHeightMode CurrentViewHeightMode = ViewHeightMode.Normal;
	public void OnViewHeightChanged(double viewHeight)
	{
		if (viewHeight <= 0)
		{
			return;
		}

		ViewHeightMode nextMode = viewHeight switch
		{
			< 480 => ViewHeightMode.Low,
			_ => ViewHeightMode.Normal,
		};
		if (CurrentViewHeightMode == nextMode)
		{
			logger.Debug("ViewHeightMode is not changed: {0} ({1}px)", CurrentViewHeightMode, viewHeight);
			return;
		}

		logger.Debug("ViewHeightMode is changed from {0} to {1} ({2}px)", CurrentViewHeightMode, nextMode, viewHeight);
		double DateAndStartButtonRowHeight = DATE_AND_START_BUTTON_ROW_HEIGHT;
		double TrainInfoHeaderRowHeight = TRAIN_INFO_HEADER_ROW_HEIGHT;
		double TrainInfoRowHeight = TRAIN_INFO_ROW_HEIGHT;
		// double TrainInfo_BeforeDeparture_RowHeight = TRAIN_INFO_BEFORE_DEPARTURE_ROW_HEIGHT;
		double CarCountAndBeforeRemarksRowHeight = CAR_COUNT_AND_BEFORE_REMARKS_ROW_HEIGHT;
		double TimetableHeaderRowHeight = TIMETABLE_HEADER_ROW_HEIGHT;
		double HakoPageHeaderRowHeight = HAKO_PAGE_HEADER_ROW_HEIGHT;
		switch (nextMode)
		{
			case ViewHeightMode.Low:
				DateAndStartButtonRowHeight = DATE_AND_START_BUTTON_ROW_HEIGHT - 6;
				TrainInfoHeaderRowHeight = TRAIN_INFO_HEADER_ROW_HEIGHT - 12;
				TrainInfoRowHeight = TRAIN_INFO_ROW_HEIGHT - 6;
				// TrainInfo_BeforeDepartureは高さ調整が面倒なため、高さを変更しない
				// TrainInfo_BeforeDeparture_RowHeight = TRAIN_INFO_BEFORE_DEPARTURE_ROW_HEIGHT;
				CarCountAndBeforeRemarksRowHeight = CAR_COUNT_AND_BEFORE_REMARKS_ROW_HEIGHT - 6;
				TimetableHeaderRowHeight = TIMETABLE_HEADER_ROW_HEIGHT - 12;
				HakoPageHeaderRowHeight = HAKO_PAGE_HEADER_ROW_HEIGHT - 12;
				break;
			case ViewHeightMode.Normal:
				// 全てデフォルトの高さ
				TrainInfo_beforeDeparture_RowHeight = TRAIN_INFO_BEFORE_DEPARTURE_ROW_HEIGHT;
				break;
			default:
				throw new NotImplementedException("Unknown ViewHeightMode");
		}

		DateAndStartButtonRowDefinition.Height = new(DateAndStartButtonRowHeight);
		TrainInfoHeaderRowDefinition.Height = new(TrainInfoHeaderRowHeight);
		TrainInfoRowDefinition.Height = new(TrainInfoRowHeight);
		// if (TrainInfo_BeforeDeparture_RowDefinition.Height.Value == this.TrainInfo_beforeDeparture_RowHeight)
		// {
		// 	TrainInfo_BeforeDeparture_RowDefinition.Height = new(TrainInfo_BeforeDeparture_RowHeight);
		// 	TrainInfo_BeforeDepartureArea.HeightRequest = TrainInfo_BeforeDeparture_RowHeight;
		// }
		CarCountAndBeforeRemarksRowDefinition.Height = new(CarCountAndBeforeRemarksRowHeight);
		TimetableHeaderRowDefinition.Height = new(TimetableHeaderRowHeight);
		HakoPageHeaderRowDefinition.Height = new(HakoPageHeaderRowHeight);
		CurrentViewHeightMode = nextMode;
		logger.Trace("RowDefinitions are updated: {0} ({1}px)", CurrentViewHeightMode, viewHeight);
	}

	private const string DATE_AND_START_BUTTON_ANIMATION_NAME = nameof(DATE_AND_START_BUTTON_ANIMATION_NAME);
	public void BeforeRemarks_TrainInfo_OpenCloseChanged(
		IAnimatable target,
		TrainInfo_BeforeDeparture TrainInfo_BeforeDepartureArea,
		ValueChangedEventArgs<bool> e
	)
	{
		bool isToOpen = e.NewValue;
		(double start, double end) = isToOpen
			? (TrainInfo_BeforeDeparture_RowDefinition.Height.Value, TrainInfo_beforeDeparture_RowHeight)
			: (TrainInfo_BeforeDeparture_RowDefinition.Height.Value, 0d);
		logger.Info("BeforeRemarks_TrainInfo_OpenCloseChanged: {0} -> {1} / pos {2} -> {3}",
			e.OldValue,
			e.NewValue,
			start,
			end
		);

		if (target.AnimationIsRunning(DATE_AND_START_BUTTON_ANIMATION_NAME))
		{
			logger.Debug("AbortAnimation({0})", DATE_AND_START_BUTTON_ANIMATION_NAME);
			target.AbortAnimation(DATE_AND_START_BUTTON_ANIMATION_NAME);
		}
		new Animation(
			v =>
			{
				if (!TrainInfo_BeforeDepartureArea.IsVisible)
				{
					logger.Debug("TrainInfo_BeforeDepartureArea.IsVisible set to true");
					TrainInfo_BeforeDepartureArea.IsVisible = true;
				}
				TrainInfo_BeforeDeparture_RowDefinition.Height = v;
				TrainInfo_BeforeDepartureArea.HeightRequest = v;
				logger.Trace("v: {0}", v);
			},
			start,
			end,
			Easing.SinInOut
		)
			.Commit(
				target,
				DATE_AND_START_BUTTON_ANIMATION_NAME,
				finished: (_, canceled) =>
				{
					if (!isToOpen && !canceled)
					{
						logger.Debug("Animation Successfully finished to close");
						TrainInfo_BeforeDepartureArea.IsVisible = false;
					}
					else
					{
						logger.Debug("Animation Successfully finished to open or canceled");
						if (!canceled)
						{
							TrainInfo_BeforeDeparture_RowDefinition.Height = new(TrainInfo_beforeDeparture_RowHeight);
							TrainInfo_BeforeDepartureArea.HeightRequest = TrainInfo_beforeDeparture_RowHeight;
						}
					}
				}
			);
		logger.Debug("Animation started");
	}
}
