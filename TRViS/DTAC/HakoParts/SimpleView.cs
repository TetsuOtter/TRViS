using System.ComponentModel;

using TRViS.IO;
using TRViS.IO.Models.DB;

namespace TRViS.DTAC.HakoParts;

public class SimpleView : Grid
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

	public const double STA_NAME_TIME_COLUMN_WIDTH = 120;
	const double TRAIN_NUMBER_ROW_HEIGHT = 80;
	const double TIME_ROW_HEIGHT = 20;
	SimpleRow? SelectedRow = null;

	public SimpleView()
	{
		logger.Debug("Creating...");

		ColumnDefinitions.Add(new(new(STA_NAME_TIME_COLUMN_WIDTH, GridUnitType.Absolute)));
		ColumnDefinitions.Add(new(new(1, GridUnitType.Star)));
		ColumnDefinitions.Add(new(new(STA_NAME_TIME_COLUMN_WIDTH, GridUnitType.Absolute)));

		InstanceManager.AppViewModel.PropertyChanged += OnAppViewModelPropertyChanged;

		OnSelectedWorkChanged(InstanceManager.AppViewModel.SelectedWork);

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
			InstanceManager.AppViewModel.SelectedTrainData = row.TrainData;
		}
		else if (SelectedRow == row)
		{
			logger.Debug("SelectedRow == row ({0}) -> reset selection", SelectedRow.TrainNumber);
			SelectedRow.IsSelected = false;
			SelectedRow = null;
		}
	}

	void OnAppViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(InstanceManager.AppViewModel.SelectedWork))
		{
			OnSelectedWorkChanged(InstanceManager.AppViewModel.SelectedWork);
		}
	}
	void OnSelectedWorkChanged(Work? newWork)
	{
		logger.Debug("newWork: {0}", newWork?.Name ?? "null");
		Clear();
		if (newWork is null)
		{
			logger.Debug("newWork is null");
			return;
		}

		ILoader? loader = InstanceManager.AppViewModel.Loader;
		if (loader is null)
		{
			logger.Debug("loader is null");
			return;
		}

		IReadOnlyList<TrainData> trainDataList = loader.GetTrainDataList(newWork.Id);
		SetRowDefinitions(trainDataList.Count);
		for (int i = 0; i < trainDataList.Count; i++)
		{
			TrainData dbTrainData = trainDataList[i];
			IO.Models.TrainData? trainData = loader.GetTrainData(dbTrainData.Id);
			if (trainData is null)
			{
				logger.Debug("trainData is null");
				continue;
			}

			SimpleRow row = new(this, i, trainData, dbTrainData);
			row.IsSelectedChanged += OnIsSelectedChanged;
		}
	}

	void SetRowDefinitions(int workCount)
	{
		int currentWorkCount = RowDefinitions.Count / 2;
		if (currentWorkCount == workCount)
		{
			logger.Debug("currentWorkCount == workCount ({0})", workCount);
			return;
		}

		for (int i = currentWorkCount; i < workCount; i++)
		{
			RowDefinitions.Add(new(new(TRAIN_NUMBER_ROW_HEIGHT, GridUnitType.Absolute)));
			RowDefinitions.Add(new(new(TIME_ROW_HEIGHT, GridUnitType.Absolute)));
		}
		for (int i = currentWorkCount - 1; workCount <= i; i--)
		{
			RowDefinitions.RemoveAt(i * 2);
			RowDefinitions.RemoveAt(i * 2);
		}
	}
}
