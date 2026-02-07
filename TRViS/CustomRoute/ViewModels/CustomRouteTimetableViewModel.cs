using CommunityToolkit.Mvvm.ComponentModel;

using System.Collections.ObjectModel;

using TRViS.CustomRoute.Services;
using TRViS.IO.Models;
using TRViS.Services;

using NLog;

namespace TRViS.CustomRoute.ViewModels;

/// <summary>
/// CustomRoute時刻表ページのメインViewModel
/// </summary>
public partial class CustomRouteTimetableViewModel : ObservableObject
{
	private static readonly Logger logger = LoggerService.GetGeneralLogger();
	private readonly CustomRouteService _service = new();
	private readonly LocationService _locationService = InstanceManager.LocationService;

	public CustomRouteTimetableViewModel()
	{
		_locationService.LocationStateChanged += OnLocationStateChanged;
	}

	#region Observable Properties

	/// <summary>
	/// 列車の一覧
	/// </summary>
	[ObservableProperty]
	public partial ObservableCollection<CustomRouteTrainListItemViewModel> TrainList { get; set; } = [];

	/// <summary>
	/// 現在選択されている列車のインデックス
	/// </summary>
	[ObservableProperty]
	public partial int SelectedTrainIndex { get; set; } = -1;

	/// <summary>
	/// 現在選択されている列車の情報
	/// </summary>
	[ObservableProperty]
	public partial CustomRouteTrainInfoViewModel? SelectedTrainInfo { get; set; } = null;

	/// <summary>
	/// 時刻表の行データ
	/// </summary>
	[ObservableProperty]
	public partial ObservableCollection<CustomRouteTimetableRowViewModel> TimetableRows { get; set; } = [];

	/// <summary>
	/// 現在位置マーカーの駅インデックス
	/// </summary>
	[ObservableProperty]
	public partial int LocationMarkerPosition { get; set; } = -1;

	/// <summary>
	/// 現在位置マーカーの状態
	/// </summary>
	[ObservableProperty]
	public partial LocationMarkerStates LocationMarkerState { get; set; } = LocationMarkerStates.Undefined;

	/// <summary>
	/// 位置情報サービスが有効かどうか
	/// </summary>
	[ObservableProperty]
	public partial bool IsLocationServiceEnabled { get; set; } = false;

	/// <summary>
	/// 運行が開始されたかどうか
	/// </summary>
	[ObservableProperty]
	public partial bool IsRunStarted { get; set; } = false;

	/// <summary>
	/// ライトモード/ダークモードが有効かどうか
	/// </summary>
	[ObservableProperty]
	public partial AppTheme CurrentAppTheme { get; set; } = AppTheme.Unspecified;

	#endregion

	#region Event Handlers

	partial void OnIsLocationServiceEnabledChanged(bool value)
	{
		_locationService.IsEnabled = value;
		logger.Info("IsLocationServiceEnabled changed to {0}", value);
	}

	partial void OnSelectedTrainIndexChanged(int value)
	{
		if (value < 0 || !_service.SelectTrainByIndex(value))
		{
			logger.Warn("Failed to select train at index {0}", value);
			return;
		}

		var selectedTrain = _service.GetSelectedTrain();
		if (selectedTrain != null)
		{
			UpdateTrainInfo(selectedTrain);
			UpdateTimetableView(selectedTrain);
		}
	}

	#endregion

	/// <summary>
	/// 列車データを設定
	/// </summary>
	public void SetTrains(IReadOnlyList<TrainData> trains)
	{
		if (trains == null || trains.Count == 0)
		{
			logger.Warn("SetTrains: No trains provided");
			TrainList.Clear();
			TimetableRows.Clear();
			SelectedTrainIndex = -1;
			return;
		}

		_service.SetTrains(trains);

		// 列車リストを更新
		TrainList = new ObservableCollection<CustomRouteTrainListItemViewModel>(
			trains.Select((train, index) => new CustomRouteTrainListItemViewModel
			{
				Index = index,
				TrainName = train.TrainNumber,
				TrainNumber = train.TrainNumber,
				LineId = train.Id,
				TrainId = train.Id,
			})
		);

		// 最初の列車を選択
		SelectedTrainIndex = 0;
		logger.Info("SetTrains: {0} trains loaded", trains.Count);
	}

	/// <summary>
	/// 列車を選択
	/// </summary>
	public void SelectTrain(int trainIndex)
	{
		SelectedTrainIndex = trainIndex;
	}

	/// <summary>
	/// 運行開始
	/// </summary>
	public void StartRun()
	{
		IsRunStarted = true;
		if (LocationMarkerPosition < 0)
		{
			LocationMarkerPosition = 0;
			LocationMarkerState = LocationMarkerStates.AroundThisStation;
		}

		logger.Info("Run started");
	}

	/// <summary>
	/// 運行終了
	/// </summary>
	public void StopRun()
	{
		IsRunStarted = false;
		LocationMarkerPosition = -1;
		LocationMarkerState = LocationMarkerStates.Undefined;
		logger.Info("Run stopped");
	}

	/// <summary>
	/// 駅をタップして位置を強制設定
	/// </summary>
	public void SetLocationAtStation(int stationIndex)
	{
		if (!IsRunStarted || !_service.IsValidStationIndex(stationIndex))
		{
			logger.Warn("SetLocationAtStation: Invalid station index {0}", stationIndex);
			return;
		}

		_locationService.ForceSetLocationInfo(stationIndex, false);
		logger.Info("Location set to station {0}", stationIndex);
	}

	/// <summary>
	/// テーマを変更
	/// </summary>
	public void ChangeTheme(AppTheme theme)
	{
		CurrentAppTheme = theme;
		Application.Current!.UserAppTheme = theme;
		logger.Info("Theme changed to {0}", theme);
	}

	#region Private Methods

	private void UpdateTrainInfo(TrainData train)
	{
		var (trainName, trainNumber, lineId) = _service.GetTrainBasicInfo(train);
		SelectedTrainInfo = new CustomRouteTrainInfoViewModel
		{
			TrainName = trainName,
			TrainNumber = trainNumber,
			LineId = lineId,
		};

		logger.Debug("Train info updated: {0} ({1})", trainName, trainNumber);
	}

	private void UpdateTimetableView(TrainData train)
	{
		var rows = _service.GetSelectedTrainRows();
		TimetableRows = new ObservableCollection<CustomRouteTimetableRowViewModel>(
			rows.Select((row, index) => new CustomRouteTimetableRowViewModel
			{
				RowIndex = index,
				StationName = row.StationName ?? string.Empty,
				ArrivalTime = row.ArriveTime?.ToString() ?? string.Empty,
				DepartureTime = row.DepartureTime?.ToString() ?? string.Empty,
				IsPass = row.IsPass,
				IsInfoRow = row.IsInfoRow,
				HasBracket = row.HasBracket,
				IsLastStop = row.IsLastStop,
				Remarks = row.Remarks,
				TrackName = row.TrackName,
				IsLocationMarkerOnThisRow = false,
			})
		);

		// 位置マーカーをリセット
		LocationMarkerPosition = -1;
		LocationMarkerState = LocationMarkerStates.Undefined;
		IsRunStarted = false;

		logger.Debug("Timetable view updated: {0} rows", rows.Count);
	}

	private void OnLocationStateChanged(object? sender, LocationStateChangedEventArgs e)
	{
		if (!IsLocationServiceEnabled || !IsRunStarted)
		{
			return;
		}

		if (e.NewStationIndex < 0 || e.NewStationIndex >= TimetableRows.Count)
		{
			IsLocationServiceEnabled = false;
			logger.Warn("Location service disabled: Invalid station index {0}", e.NewStationIndex);
			return;
		}

		LocationMarkerState = e.IsRunningToNextStation
			? LocationMarkerStates.RunningToNextStation
			: LocationMarkerStates.AroundThisStation;
		LocationMarkerPosition = e.NewStationIndex;

		// 行の状態を更新
		for (int i = 0; i < TimetableRows.Count; i++)
		{
			TimetableRows[i].IsLocationMarkerOnThisRow = (i == e.NewStationIndex);
		}

		logger.Debug("Location marker updated: Position={0}, State={1}", e.NewStationIndex, e.IsRunningToNextStation ? "Running" : "Stopped");
	}

	#endregion
}

/// <summary>
/// 現在位置マーカーの状態
/// </summary>
public enum LocationMarkerStates
{
	/// <summary>未定義（表示なし）</summary>
	Undefined = 0,

	/// <summary>駅停車中</summary>
	AroundThisStation = 1,

	/// <summary>次駅へ移動中</summary>
	RunningToNextStation = 2,
}

/// <summary>
/// 列車リストアイテムのViewModel
/// </summary>
public class CustomRouteTrainListItemViewModel
{
	public int Index { get; set; }
	public string? TrainName { get; set; }
	public string? TrainNumber { get; set; }
	public string? LineId { get; set; }
	public string? TrainId { get; set; }
}

/// <summary>
/// 列車情報のViewModel
/// </summary>
public class CustomRouteTrainInfoViewModel
{
	public string? TrainName { get; set; }
	public string? TrainNumber { get; set; }
	public string? LineId { get; set; }
}

/// <summary>
/// 時刻表行のViewModel
/// </summary>
public partial class CustomRouteTimetableRowViewModel : ObservableObject
{
	[ObservableProperty]
	public partial int RowIndex { get; set; }

	[ObservableProperty]
	public partial string StationName { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string? ArrivalTime { get; set; }

	[ObservableProperty]
	public partial string? DepartureTime { get; set; }

	[ObservableProperty]
	public partial bool IsPass { get; set; }

	[ObservableProperty]
	public partial bool IsInfoRow { get; set; }

	[ObservableProperty]
	public partial bool HasBracket { get; set; }

	[ObservableProperty]
	public partial bool IsLastStop { get; set; }

	[ObservableProperty]
	public partial string? Remarks { get; set; }

	[ObservableProperty]
	public partial string? TrackName { get; set; }

	[ObservableProperty]
	public partial bool IsLocationMarkerOnThisRow { get; set; }
}
