using System.ComponentModel;

using TRViS.DTAC.Logic.Abstractions;
using TRViS.DTAC.Logic.Formatters;
using TRViS.IO.Models;
using TRViS.LocationService.Abstractions;

namespace TRViS.DTAC.Logic.Presenter;

/// <summary>
/// Presenter for the vertical style page that manages all business logic.
/// Wake lock, debug map, and orientation are View responsibilities.
/// </summary>
public sealed class VerticalStylePagePresenter : ILocationMarkerStateSource, IDisposable
{
	private readonly IDtacLocationServiceController _locationService;
	private readonly IMarkerToggleController _markerToggle;
	private readonly IClock _clock;
	private readonly IAppViewModelProvider _appViewModelProvider;

	private VerticalPageState _currentState = new();
	private TrainData? _lastTrainData = null;

	private (int rowIndex, DateTime time)? _lastTapInfo = null;
	private const double DOUBLE_TAP_DETECT_MS = 500;

	private bool _disposed = false;

	public VerticalPageState CurrentState => _currentState;
	public TrainData? CurrentTrainData => _lastTrainData;

	// ILocationMarkerStateSource
	IReadOnlyDictionary<int, VerticalTimetableRowState> ILocationMarkerStateSource.RowStates => _currentState.RowStates;

	public event EventHandler<VerticalPageStateChangedEventArgs>? StateChanged;

	public VerticalStylePagePresenter(
		IDtacLocationServiceController locationService,
		IMarkerToggleController markerToggle,
		IClock clock,
		IAppViewModelProvider appViewModelProvider)
	{
		_locationService = locationService ?? throw new ArgumentNullException(nameof(locationService));
		_markerToggle = markerToggle ?? throw new ArgumentNullException(nameof(markerToggle));
		_clock = clock ?? throw new ArgumentNullException(nameof(clock));
		_appViewModelProvider = appViewModelProvider ?? throw new ArgumentNullException(nameof(appViewModelProvider));

		_locationService.CanUseServiceChanged += OnLocationServiceCanUseChanged;
		_locationService.LocationStateChanged += OnLocationStateChanged_Internal;
		_locationService.GpsLocationUpdated += OnGpsLocationUpdated_Internal;
		_appViewModelProvider.PropertyChanged += OnAppViewModelPropertyChanged;

		// Sync initial train: PropertyChanged may have fired before this presenter subscribed.
		SetSelectedTrainData(_appViewModelProvider.SelectedTrainData);
	}

	private void OnAppViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		// SelectedTrainData が変わったとき、または SelectedWork が変わったとき (= AffectDateText
		// が更新された可能性があるとき) に施行日表示を再評価する。
		if (e.PropertyName == nameof(IAppViewModelProvider.SelectedTrainData)
			|| e.PropertyName == nameof(IAppViewModelProvider.SelectedWork))
			SetSelectedTrainData(_appViewModelProvider.SelectedTrainData);
	}

	private void OnLocationStateChanged_Internal(object? sender, LocationStateChangedEventArgs e)
	{
		if (!_currentState.TimetableViewState.IsLocationServiceEnabled)
			return;

		if (e.NewStationIndex < 0)
		{
			_currentState.TimetableViewState.IsLocationServiceEnabled = false;
			_currentState.PageHeaderState.IsLocationServiceEnabled = false;
			_currentState.LocationServiceState.IsEnabled = false;
			RaiseStateChanged(VerticalPageStateSection.LocationService | VerticalPageStateSection.PageHeader | VerticalPageStateSection.TimetableView);
			return;
		}

		if (!_currentState.RowStates.ContainsKey(e.NewStationIndex))
		{
			_currentState.TimetableViewState.IsLocationServiceEnabled = false;
			_currentState.PageHeaderState.IsLocationServiceEnabled = false;
			_currentState.LocationServiceState.IsEnabled = false;
			RaiseStateChanged(VerticalPageStateSection.LocationService | VerticalPageStateSection.PageHeader | VerticalPageStateSection.TimetableView);
			return;
		}

		VerticalPageStateUpdater.ResetAllRowLocationStates(_currentState);
		_currentState.RowStates[e.NewStationIndex].LocationState = e.IsRunningToNextStation
			? TimetableLocationState.RunningToNextStation
			: TimetableLocationState.AroundThisStation;

		RaiseStateChanged(VerticalPageStateSection.RowStates | VerticalPageStateSection.TimetableView);
	}

	private void OnGpsLocationUpdated_Internal(object? sender, GpsLocationUpdate e)
	{
		VerticalPageStateUpdater.UpdateGpsLocation(
			_currentState.LocationServiceState,
			e.Latitude,
			e.Longitude,
			e.Accuracy);
		RaiseStateChanged(VerticalPageStateSection.LocationService);
	}

	private void SetSelectedTrainData(TrainData? trainData)
	{
		string affectDate = AffectDateFormatter.FormatAffectDateOrText(
			_appViewModelProvider.SelectedWork?.AffectDateText,
			trainData?.AffectDate,
			trainData?.DayCount ?? 0);

		if (ReferenceEquals(_lastTrainData, trainData)
			&& (trainData is null || _currentState.PageHeaderState.AffectDateLabelText == affectDate))
		{
			return;
		}

		// 同じ列車 (TrainId 一致) なら soft 更新パスへ。WS リアルタイム編集 (TRViS_Realtime_Editor)
		// が同一 TrainId に対して都度新インスタンスを発行するため、ここで参照が変わったからといって
		// 運行状態を巻き戻すと、ユーザーが「運行開始」を押した直後に列の DriveTime を 1 つ直しただけで
		// 「運行前」に戻されてしまう。同 Id 更新は表示 field と RowStates の更新だけに留め、
		// IsRunning / IsRunStarted / marker toggle / IsLocationServiceEnabled は維持する。
		// 行数が変わった場合 (= 駅追加/削除) も同じ列車であれば soft path で対応する。
		// RowStates dict は新 row 数に揃えてリサイズし、IsInfoRow を再同期する。
		string? newId = trainData?.Id;
		string? oldId = _lastTrainData?.Id;
		bool canSoftUpdate = trainData is not null
			&& TimetableRebuildPolicy.IsSameTrainEdit(oldId, newId);

		_lastTrainData = trainData;

		if (canSoftUpdate)
		{
			VerticalPageStateFactory.ApplyTrainDataFields(_currentState, trainData, affectDate);
			VerticalPageStateFactory.ResizeAndSyncRowStates(_currentState, trainData!.Rows ?? []);

			// 駅情報は更新する。NetworkSyncServiceBase.StaLocationInfo の setter が
			// 旧 current 駅の Location_m を新配列で再検索して index を再計算するので、
			// 走行中の駅追跡は維持される。
			_locationService.SetTimetableRows(trainData.Rows);

			RaiseStateChanged(VerticalPageStateSection.All);
			return;
		}

		// 全面リセットパス: 列車切替 / 行数変化 / null クリア / 初回ロード のいずれか。
		bool isLocationServiceEnabled = _currentState.LocationServiceState.IsEnabled;
		bool canUseLocationService = _currentState.PageHeaderState.CanUseLocationService;

		var newState = VerticalPageStateFactory.CreateStateFromTrainData(
			trainData,
			affectDate,
			isLocationServiceEnabled);

		newState.PageHeaderState.CanUseLocationService = canUseLocationService;
		newState.TimetableViewState.CanUseLocationService = canUseLocationService;

		if (trainData?.Rows != null)
		{
			VerticalPageStateFactory.InitializeRowStates(newState, trainData.Rows);
		}

		_currentState = newState;

		_locationService.SetTimetableRows(trainData?.Rows);
		_markerToggle.ResetToggle();

		_currentState.PageHeaderState.IsRunning = false;
		_currentState.TimetableViewState.IsRunStarted = false;

		RaiseStateChanged(VerticalPageStateSection.All);
	}

	/// <summary>
	/// Called when the start/end run button is clicked.
	/// Toggles the running state.
	/// </summary>
	public void OnStartButtonClicked()
	{
		SetIsRunning(!_currentState.PageHeaderState.IsRunning);
	}

	private void SetIsRunning(bool isRunning)
	{
		_currentState.PageHeaderState.IsRunning = isRunning;
		_currentState.TimetableViewState.IsRunStarted = isRunning;

		if (!isRunning)
		{
			_locationService.IsEnabled = false;
			_currentState.PageHeaderState.IsLocationServiceEnabled = false;
			_currentState.LocationServiceState.IsEnabled = false;
			_currentState.TimetableViewState.IsLocationServiceEnabled = false;

			VerticalPageStateUpdater.ResetAllRowLocationStates(_currentState);
		}
		else
		{
			bool hasActiveMarker = _currentState.RowStates.Values.Any(r => r.LocationState != TimetableLocationState.Undefined);
			if (!hasActiveMarker)
			{
				var firstStationKey = _currentState.RowStates
					.Where(kvp => !kvp.Value.IsInfoRow)
					.Select(kvp => (int?)kvp.Key)
					.FirstOrDefault();
				if (firstStationKey.HasValue)
					_currentState.RowStates[firstStationKey.Value].LocationState = TimetableLocationState.AroundThisStation;
			}
		}

		RaiseStateChanged(
			VerticalPageStateSection.PageHeader
			| VerticalPageStateSection.TimetableView
			| VerticalPageStateSection.LocationService
			| VerticalPageStateSection.RowStates);
	}

	/// <summary>
	/// Called when a timetable row is tapped.
	/// </summary>
	public void OnRowTapped(int rowIndex)
	{
		if (!_currentState.TimetableViewState.IsRunStarted)
			return;

		if (!_currentState.RowStates.TryGetValue(rowIndex, out var rowState) || rowState.IsInfoRow)
			return;

		bool isLocationServiceEnabled = _currentState.TimetableViewState.IsLocationServiceEnabled;

		if (isLocationServiceEnabled)
		{
			DateTime now = _clock.UtcNow;

			if (_lastTapInfo is null
				|| _lastTapInfo.Value.rowIndex != rowIndex
				|| (now - _lastTapInfo.Value.time).TotalMilliseconds > DOUBLE_TAP_DETECT_MS)
			{
				_lastTapInfo = (rowIndex, now);
				return;
			}

			_lastTapInfo = null;
			_locationService.ForceSetLocationInfo(rowIndex, false);
		}
		else
		{
			_lastTapInfo = null;

			int lastStationRow = _currentState.RowStates
				.Where(kvp => !kvp.Value.IsInfoRow)
				.Select(kvp => kvp.Key)
				.DefaultIfEmpty(-1)
				.Max();
			bool isLastRow = rowIndex == lastStationRow;

			int currentMarkerRow = -1;
			TimetableLocationState currentMarkerState = TimetableLocationState.Undefined;
			foreach (var kvp in _currentState.RowStates)
			{
				if (kvp.Value.LocationState != TimetableLocationState.Undefined)
				{
					currentMarkerRow = kvp.Key;
					currentMarkerState = kvp.Value.LocationState;
					break;
				}
			}

			VerticalPageStateUpdater.ResetAllRowLocationStates(_currentState);

			if (currentMarkerState == TimetableLocationState.Undefined)
			{
				rowState.LocationState = TimetableLocationState.AroundThisStation;
			}
			else if (currentMarkerRow == rowIndex && currentMarkerState == TimetableLocationState.AroundThisStation)
			{
				rowState.LocationState = isLastRow ? TimetableLocationState.AroundThisStation : TimetableLocationState.RunningToNextStation;
			}
			else if (currentMarkerRow == rowIndex && currentMarkerState == TimetableLocationState.RunningToNextStation)
			{
				rowState.LocationState = TimetableLocationState.AroundThisStation;
			}
			else
			{
				rowState.LocationState = TimetableLocationState.AroundThisStation;
			}
		}

		RaiseStateChanged(VerticalPageStateSection.RowStates | VerticalPageStateSection.TimetableView);
	}

	/// <summary>
	/// Called when the location service button is clicked.
	/// Toggles the location service enabled state.
	/// </summary>
	public void OnLocationServiceToggled()
	{
		SetLocationServiceEnabled(!_currentState.LocationServiceState.IsEnabled);
	}

	private void SetLocationServiceEnabled(bool enabled)
	{
		_locationService.IsEnabled = enabled;
		_currentState.LocationServiceState.IsEnabled = enabled;
		_currentState.PageHeaderState.IsLocationServiceEnabled = enabled;
		_currentState.TimetableViewState.IsLocationServiceEnabled = enabled;

		RaiseStateChanged(
			VerticalPageStateSection.LocationService
			| VerticalPageStateSection.PageHeader
			| VerticalPageStateSection.TimetableView);
	}

	/// <summary>
	/// Called when network sync auto start is requested
	/// </summary>
	public void OnNetworkSyncAutoStartRequested()
	{
		if (_locationService.NetworkSyncServiceCanStart)
		{
			if (!_currentState.PageHeaderState.IsRunning)
			{
				SetIsRunning(true);
			}
			if (!_currentState.TimetableViewState.IsLocationServiceEnabled)
			{
				SetLocationServiceEnabled(true);
			}
		}
	}

	private void OnLocationServiceCanUseChanged(object? sender, bool canUse)
	{
		_currentState.PageHeaderState.CanUseLocationService = canUse;
		_currentState.TimetableViewState.CanUseLocationService = canUse;

		if (_locationService.NetworkSyncServiceCanStart)
		{
			if (!_currentState.PageHeaderState.IsRunning)
			{
				SetIsRunning(true);
			}
			if (!_currentState.TimetableViewState.IsLocationServiceEnabled)
			{
				SetLocationServiceEnabled(true);
			}
		}

		RaiseStateChanged(VerticalPageStateSection.PageHeader | VerticalPageStateSection.TimetableView);
	}

	private void RaiseStateChanged(VerticalPageStateSection changed)
	{
		StateChanged?.Invoke(this, new VerticalPageStateChangedEventArgs(changed));
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		_disposed = true;

		_locationService.CanUseServiceChanged -= OnLocationServiceCanUseChanged;
		_locationService.LocationStateChanged -= OnLocationStateChanged_Internal;
		_locationService.GpsLocationUpdated -= OnGpsLocationUpdated_Internal;
		_appViewModelProvider.PropertyChanged -= OnAppViewModelPropertyChanged;
	}
}
