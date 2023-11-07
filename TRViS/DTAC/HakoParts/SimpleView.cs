namespace TRViS.DTAC.HakoParts;

public class SimpleView : Grid
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

	const double STA_NAME_TIME_COLUMN_WIDTH = 120;
	const double TRAIN_NUMBER_ROW_HEIGHT = 80;
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
		RowDefinitions.Add(new(new(TRAIN_NUMBER_ROW_HEIGHT, GridUnitType.Absolute)));
		RowDefinitions.Add(new(new(TIME_ROW_HEIGHT, GridUnitType.Absolute)));

		SimpleRow row1 = new(this, 0)
		{
			FromStationName = "試験1",
			FromTime = new(12, 34, null, null),
			ToStationName = "試験2",
			ToTime = new(12, 34, 56, null),
			TrainNumber = "試験3",
		};
		SimpleRow row2 = new(this, 1)
		{
			FromStationName = "さ新都心",
			FromTime = new(12, 34, 56, null),
			ToStationName = "長い駅名",
			ToTime = new(12, 34, 56, null),
			TrainNumber = "現回９９２３横浜",
		};
		SimpleRow row3 = new(this, 2)
		{
			FromStationName = "さ新都心",
			FromTime = new(12, 34, 56, null),
			ToStationName = "長い駅名",
			ToTime = new(12, 34, 56, null),
			TrainNumber = "入換担当\n　　現回９９２３横浜",
		};

		row1.IsSelectedChanged += OnIsSelectedChanged;
		row2.IsSelectedChanged += OnIsSelectedChanged;
		row3.IsSelectedChanged += OnIsSelectedChanged;

		logger.Debug("Created");
	}

	void OnIsSelectedChanged(object? sender, bool oldValue, bool newValue)
	{
		if (sender is not SimpleRow row)
		{
			logger.Debug("sender is not SimpleRow");
			return;
		}

		if (newValue == true)
		{
			if (SelectedRow is not null && SelectedRow != row)
			{
				logger.Debug("SelectedRow is not null and SelectedRow != row -> last selection ({0}) set to false", SelectedRow.TrainNumber);
				SelectedRow.IsSelected = false;
			}

			logger.Debug("renew selection to {0}", row.TrainNumber);
			SelectedRow = row;
		}
		else if (SelectedRow == row)
		{
			logger.Debug("SelectedRow == row ({0}) -> reset selection", SelectedRow.TrainNumber);
			SelectedRow.IsSelected = false;
			SelectedRow = null;
		}
	}
}
