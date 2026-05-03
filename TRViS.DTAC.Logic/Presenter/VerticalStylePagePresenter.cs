using System.ComponentModel;

using TRViS.DTAC.Logic.Abstractions;
using TRViS.IO.Models;
using TRViS.Services;

namespace TRViS.DTAC.Logic.Presenter;

/// <summary>
/// Presenter for the vertical style page that manages all business logic.
/// Wake lock, debug map, and orientation are View responsibilities.
/// </summary>
public sealed class VerticalStylePagePresenter : IDisposable
{
	private readonly IDtacLocationServiceController _locationService;
	private readonly IMarkerToggleController _markerToggle;
	private readonly IDtacCrashLogger _crashLogger;
	private readonly IClock _clock;

	private VerticalPageState _currentState = new();
	private TrainData? _lastTrainData = null;

	private (int rowIndex, DateTime time)? _lastTapInfo = null;
	private const double DOUBLE_TAP_DETECT_MS = 500;

	private bool _disposed = false;

	public VerticalPageState CurrentState => _currentState;

	public event EventHandler<VerticalPageStateChangedEventArgs>? StateChanged;

	public VerticalStylePagePresenter(
		IDtacLocationServiceController locationService,
		IMarkerToggleController markerToggle,
		IDtacCrashLogger crashLogger,
		IClock clock)
	{
		_locationService = locationService ?? throw new ArgumentNullException(nameof(locationService));
		_markerToggle = markerToggle ?? throw new ArgumentNullException(nameof(markerToggle));
		_crashLogger = crashLogger ?? throw new ArgumentNullException(nameof(crashLogger));
		_clock = clock ?? throw new ArgumentNullException(nameof(clock));

		_locationService.CanUseServiceChanged += OnLocationServiceCanUseChanged_Internal;
		_locationService.LocationStateChanged += OnLocationStateChanged_Internal;
		_locationService.GpsLocationUpdated += OnGpsLocationUpdated_Internal;
	}

	private void OnLocationServiceCanUseChanged_Internal(object? sender, bool canUse)
	{
		OnLocationServiceCanUseChanged(canUse);
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

		VerticalPageStateFactory.ResetAllRowLocationStates(_currentState);
		_currentState.RowStates[e.NewStationIndex].LocationState = e.IsRunningToNextStation ? 2 : 1;

		RaiseStateChanged(VerticalPageStateSection.RowStates | VerticalPageStateSection.TimetableView);
	}

	private void OnGpsLocationUpdated_Internal(object? sender, GpsLocationUpdate e)
	{
		VerticalPageStateFactory.UpdateGpsLocation(
			_currentState.LocationServiceState,
			e.Latitude,
			e.Longitude,
			e.Accuracy);
		RaiseStateChanged(VerticalPageStateSection.LocationService);
	}

	/// <summary>
	/// Called when train data selection changes
	/// </summary>
	public void OnSelectedTrainDataChanged(TrainData? trainData)
	{
		string affectDate = ViewHostStateFactory.FormatAffectDateOnly(
			trainData?.AffectDate,
			trainData?.DayCount ?? 0);

		if (ReferenceEquals(_lastTrainData, trainData) && trainData != null
			&& _currentState.PageHeaderState.AffectDateLabelText == affectDate)
		{
			return;
		}

		_lastTrainData = trainData;

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
			VerticalPageStateFactory.InitializeRowStates(newState, trainData.Rows.Length);
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

			VerticalPageStateFactory.ResetAllRowLocationStates(_currentState);
		}
		else
		{
			bool hasActiveMarker = _currentState.RowStates.Values.Any(r => r.LocationState != 0);
			if (!hasActiveMarker && _currentState.RowStates.Count > 0)
			{
				_currentState.RowStates[0].LocationState = 1; // AroundThisStation
			}
		}

		RaiseStateChanged(
			VerticalPageStateSection.PageHeader
			| VerticalPageStateSection.TimetableView
			| VerticalPageStateSection.LocationService
			| VerticalPageStateSection.RowStates);
	}

	/// <summary>
	/// Called when a timetable row is tapped
	/// </summary>
	public void OnRowTapped(int rowIndex, bool isInfoRow, int totalRowCount)
	{
		if (!_currentState.TimetableViewState.IsRunStarted || isInfoRow)
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

			if (!_currentState.RowStates.TryGetValue(rowIndex, out var rowState))
				return;

			bool isLastRow = rowIndex == totalRowCount - 1;

			int currentMarkerRow = -1;
			int currentMarkerState = 0;
			foreach (var kvp in _currentState.RowStates)
			{
				if (kvp.Value.LocationState != 0)
				{
					currentMarkerRow = kvp.Key;
					currentMarkerState = kvp.Value.LocationState;
					break;
				}
			}

			VerticalPageStateFactory.ResetAllRowLocationStates(_currentState);

			if (currentMarkerState == 0)
			{
				rowState.LocationState = 1;
			}
			else if (currentMarkerRow == rowIndex && currentMarkerState == 1)
			{
				rowState.LocationState = isLastRow ? 1 : 2;
			}
			else if (currentMarkerRow == rowIndex && currentMarkerState == 2)
			{
				rowState.LocationState = 1;
			}
			else
			{
				rowState.LocationState = 1;
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
	/// Called when timetable busy state changes
	/// </summary>
	public void OnTimetableBusyChanged(bool isBusy)
	{
		VerticalPageStateFactory.UpdateTimetableActivityIndicatorState(_currentState.TimetableActivityIndicatorState, isBusy);
		RaiseStateChanged(VerticalPageStateSection.ActivityIndicator);
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

	/// <summary>
	/// Called when location service can-use state changes
	/// </summary>
	public void OnLocationServiceCanUseChanged(bool canUse)
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

	/// <summary>
	/// Logs an exception via the crash logger.
	/// </summary>
	public void CrashLog(Exception ex, string? context = null)
	{
		_crashLogger.Log(ex, context);
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

		_locationService.CanUseServiceChanged -= OnLocationServiceCanUseChanged_Internal;
		_locationService.LocationStateChanged -= OnLocationStateChanged_Internal;
		_locationService.GpsLocationUpdated -= OnGpsLocationUpdated_Internal;
	}
}
