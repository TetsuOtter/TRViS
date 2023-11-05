namespace TRViS.DTAC.HakoParts;

public class SimpleView : Grid
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

	const double STA_NAME_TIME_COLUMN_WIDTH = 120;
	const double TRAIN_NUMBER_ROW_HEIGHT = 40;
	const double TIME_ROW_HEIGHT = 20;
	SimpleRow? SelectedRow = null;

	public SimpleView()
	{
		logger.Debug("Creating...");
		
		ColumnDefinitions.Add(new(new(STA_NAME_TIME_COLUMN_WIDTH, GridUnitType.Absolute)));
		ColumnDefinitions.Add(new(new(1, GridUnitType.Star)));
		ColumnDefinitions.Add(new(new(STA_NAME_TIME_COLUMN_WIDTH, GridUnitType.Absolute)));

		RowDefinitions.Add(new(new(TRAIN_NUMBER_ROW_HEIGHT, GridUnitType.Absolute)));
		RowDefinitions.Add(new(new(TIME_ROW_HEIGHT, GridUnitType.Absolute)));
		RowDefinitions.Add(new(new(TRAIN_NUMBER_ROW_HEIGHT, GridUnitType.Absolute)));
		RowDefinitions.Add(new(new(TIME_ROW_HEIGHT, GridUnitType.Absolute)));

		new SimpleRow(this, 0)
		{
			FromStationName = "試験1",
			FromTime = new(12, 34, null, null),
			ToStationName = "試験2",
			ToTime = new(12, 34, 56, null),
			TrainNumber = "試験3",
		};
		new SimpleRow(this, 1)
		{
			FromStationName = "さ新都心",
			FromTime = new(12, 34, 56, null),
			ToStationName = "長い駅名",
			ToTime = new(12, 34, 56, null),
			TrainNumber = "現回９９２３横浜",
		};

		logger.Debug("Created");
	}
}
