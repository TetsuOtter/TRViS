using System.ComponentModel;

using TRViS.IO;
using TRViS.IO.Models;
using TRViS.Services;

namespace TRViS.DTAC.HakoParts;

public class SimpleView : Grid
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	public const double STA_NAME_TIME_COLUMN_WIDTH = 120;
	const double TRAIN_NUMBER_ROW_HEIGHT = 72;
	const double TIME_ROW_HEIGHT = 20;
	List<SimpleRow> Rows { get; } = new();
	SimpleRow? _SelectedRow = null;
	SimpleRow? SelectedRow
	{
		get => _SelectedRow;
		set
		{
			if (_SelectedRow == value)
			{
				logger.Debug("_SelectedRow == value");
				return;
			}

			if (_SelectedRow is not null)
			{
				_SelectedRow.IsSelected = false;
			}

			_SelectedRow = value;
			if (value is not null)
			{
				value.IsSelected = true;
			}
		}
	}

	public SimpleView()
	{
		logger.Debug("Creating...");

		ColumnDefinitions.Add(new(new(STA_NAME_TIME_COLUMN_WIDTH, GridUnitType.Absolute)));
		ColumnDefinitions.Add(new(new(1, GridUnitType.Star)));
		ColumnDefinitions.Add(new(new(STA_NAME_TIME_COLUMN_WIDTH, GridUnitType.Absolute)));

		InstanceManager.AppViewModel.PropertyChanged += OnAppViewModelPropertyChanged;

		try
		{
			OnSelectedWorkChanged(InstanceManager.AppViewModel.SelectedWork);
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "SimpleView.OnSelectedWorkChanged");
			Utils.ExitWithAlert(ex);
		}

		logger.Debug("Created");
	}

	void OnIsSelectedChanged(object? sender, bool oldValue, bool newValue)
	{
		if (sender is not SimpleRow row || newValue != true)
		{
			logger.Debug("sender is not SimpleRow / newValue != true");
			return;
		}

		try
		{
			SelectedRow = row;
			InstanceManager.AppViewModel.SelectedTrainData = row.TrainData;
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "SimpleView.OnIsSelectedChanged");
			Utils.ExitWithAlert(ex);
		}
	}

	void OnAppViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(InstanceManager.AppViewModel.SelectedWork))
		{
			try
			{
				OnSelectedWorkChanged(InstanceManager.AppViewModel.SelectedWork);
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "SimpleView.OnSelectedWorkChanged");
				Utils.ExitWithAlert(ex);
			}
		}
		else if (e.PropertyName == nameof(InstanceManager.AppViewModel.SelectedTrainData))
		{
			try
			{
				OnSelectedTrainChanged(InstanceManager.AppViewModel.SelectedTrainData);
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "SimpleView.OnSelectedTrainChanged");
				Utils.ExitWithAlert(ex);
			}
		}
	}
	void OnSelectedWorkChanged(IO.Models.DB.Work? newWork)
	{
		logger.Debug("newWork: {0}", newWork?.Name ?? "null");
		Clear();
		SelectedRow = null;
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

		Rows.Clear();
		IReadOnlyList<IO.Models.DB.TrainData> trainDataList = loader.GetTrainDataList(newWork.Id);
		SetRowDefinitions(trainDataList.Count);
		TrainData? selectedTrainData = InstanceManager.AppViewModel.SelectedTrainData;
		for (int i = 0; i < trainDataList.Count; i++)
		{
			string trainId = trainDataList[i].Id;
			IO.Models.TrainData? trainData = loader.GetTrainData(trainId);
			if (trainData is null)
			{
				logger.Debug("trainData is null");
				continue;
			}

			SimpleRow row = new(this, i, trainData);
			Rows.Add(row);
			row.IsSelectedChanged += OnIsSelectedChanged;
			if (trainId == selectedTrainData?.Id)
			{
				logger.Debug("trainData == selectedTrainData ({0})", trainData.TrainNumber);
				row.IsSelected = true;
				SelectedRow = row;
			}
		}
	}

	void OnSelectedTrainChanged(TrainData? newTrainData)
	{
		logger.Debug("newTrainData: {0}", newTrainData?.TrainNumber ?? "null");
		if (newTrainData is null)
		{
			SelectedRow = null;
			return;
		}

		if (SelectedRow?.TrainData.Id == newTrainData.Id)
		{
			logger.Debug("SelectedRow.TrainData.Id == newTrainData.Id ({0})", newTrainData.TrainNumber);
			return;
		}

		SelectedRow = Rows.FirstOrDefault(v => v.TrainData.Id == newTrainData.Id);
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
