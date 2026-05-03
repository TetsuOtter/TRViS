using System.ComponentModel;

using TRViS.DTAC.Logic.Abstractions;
using TRViS.IO.Models;
using TRViS.Services;

namespace TRViS.DTAC.Logic.Presenter;

/// <summary>
/// Presenter for the vertical style page that manages all business logic.
/// </summary>
public sealed class VerticalStylePagePresenter : IDisposable
{
	private readonly IDtacLocationServiceController _locationService;
	private readonly IWakeLockController _wakeLock;
	private readonly IEasterEggSettings _easterEgg;
	private readonly IViewHostModeProvider _viewHostMode;
	private readonly IMarkerToggleController _markerToggle;
	private readonly IDtacCrashLogger _crashLogger;
	private readonly IClock _clock;

	private VerticalPageState _currentState = new();
	private TrainData? _lastTrainData = null;
	private string? _lastAffectDate = null;
	private double _lastPageHeight = 0;
	private double _lastTimetableHeight = 0;
	// NOTE: This constant mirrors VerticalStylePage.CONTENT_OTHER_THAN_TIMETABLE_HEIGHT
	private const double CONTENT_OTHER_THAN_TIMETABLE_HEIGHT = 359;

	private (int rowIndex, DateTime time)? _lastTapInfo = null;
	private const double DOUBLE_TAP_DETECT_MS = 500;

	private bool _disposed = false;

	public VerticalPageState CurrentState => _currentState;

	public event EventHandler<VerticalPageStateChangedEventArgs>? StateChanged;

	public VerticalStylePagePresenter(
		IDtacLocationServiceController locationService,
		IWakeLockController wakeLock,
		IEasterEggSettings easterEgg,
		IViewHostModeProvider viewHostMode,
		IMarkerToggleController markerToggle,
		IDtacCrashLogger crashLogger,
		IClock clock)
	{
		_locationService = locationService ?? throw new ArgumentNullException(nameof(locationService));
		_wakeLock = wakeLock ?? throw new ArgumentNullException(nameof(wakeLock));
		_easterEgg = easterEgg ?? throw new ArgumentNullException(nameof(easterEgg));
		_viewHostMode = viewHostMode ?? throw new ArgumentNullException(nameof(viewHostMode));
		_markerToggle = markerToggle ?? throw new ArgumentNullException(nameof(markerToggle));
		_crashLogger = crashLogger ?? throw new ArgumentNullException(nameof(crashLogger));
		_clock = clock ?? throw new ArgumentNullException(nameof(clock));

		// Subscribe to events
		_easterEgg.PropertyChanged += OnEasterEggPropertyChanged;
		_viewHostMode.PropertyChanged += OnViewHostModePropertyChanged;
		_locationService.CanUseServiceChanged += OnLocationServiceCanUseChanged_Internal;
		_locationService.LocationStateChanged += OnLocationStateChanged_Internal;
		_locationService.GpsLocationUpdated += OnGpsLocationUpdated_Internal;
	}

	private void OnEasterEggPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(IEasterEggSettings.KeepScreenOnWhenRunning))
		{
			// Handle wake lock setting change during runtime
			if (_currentState.TimetableViewState.IsRunStarted && _easterEgg.KeepScreenOnWhenRunning)
			{
				_wakeLock.EnableWakeLock();
			}
			else if (_currentState.TimetableViewState.IsRunStarted && !_easterEgg.KeepScreenOnWhenRunning)
			{
				_wakeLock.DisableWakeLock();
			}
		}
		else if (e.PropertyName == nameof(IEasterEggSettings.ShowMapWhenLandscape))
		{
			VerticalPageStateFactory.UpdateDebugMapState(
				_currentState.DebugMapState,
				_easterEgg.ShowMapWhenLandscape,
				_currentState.DebugMapState.IsLandscapeMode);
			RaiseStateChanged(VerticalPageStateSection.DebugMap);
		}
	}

	private void OnViewHostModePropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(IViewHostModeProvider.IsViewHostVisible)
			|| e.PropertyName == nameof(IViewHostModeProvider.IsVerticalViewMode))
		{
			// Re-run train data application when view host becomes visible
			OnSelectedTrainDataChanged(_lastTrainData, _lastAffectDate);
		}
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
			// Invalid index - disable location service
			_currentState.TimetableViewState.IsLocationServiceEnabled = false;
			_currentState.PageHeaderState.IsLocationServiceEnabled = false;
			_currentState.LocationServiceState.IsEnabled = false;
			RaiseStateChanged(VerticalPageStateSection.LocationService | VerticalPageStateSection.PageHeader | VerticalPageStateSection.TimetableView);
			return;
		}

		if (!_currentState.RowStates.ContainsKey(e.NewStationIndex))
		{
			// Index out of range - disable location service
			_currentState.TimetableViewState.IsLocationServiceEnabled = false;
			_currentState.PageHeaderState.IsLocationServiceEnabled = false;
			_currentState.LocationServiceState.IsEnabled = false;
			RaiseStateChanged(VerticalPageStateSection.LocationService | VerticalPageStateSection.PageHeader | VerticalPageStateSection.TimetableView);
			return;
		}

		// Clear all existing markers
		VerticalPageStateFactory.ResetAllRowLocationStates(_currentState);

		// Set the new marker
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
	public void OnSelectedTrainDataChanged(TrainData? trainData, string? affectDate)
	{
		_lastAffectDate = affectDate;

		// Lazy load check - if view host is not visible or not in vertical mode, save but don't apply
		if (!_viewHostMode.IsViewHostVisible || !_viewHostMode.IsVerticalViewMode)
		{
			_lastTrainData = trainData;
			return;
		}

		// Idempotency: if same train data and already applied, skip
		if (ReferenceEquals(_lastTrainData, trainData) && trainData != null
			&& _currentState.PageHeaderState.AffectDateLabelText == (affectDate ?? string.Empty))
		{
			return;
		}

		_lastTrainData = trainData;

		bool isLocationServiceEnabled = _currentState.LocationServiceState.IsEnabled;
		bool canUseLocationService = _currentState.PageHeaderState.CanUseLocationService;
		bool isLandscape = _currentState.DebugMapState.IsLandscapeMode;

		// Rebuild state from train data
		var newState = VerticalPageStateFactory.CreateStateFromTrainData(
			trainData,
			affectDate,
			isLocationServiceEnabled,
			_lastPageHeight,
			CONTENT_OTHER_THAN_TIMETABLE_HEIGHT);

		// Preserve debug map state
		VerticalPageStateFactory.UpdateDebugMapState(newState.DebugMapState, _easterEgg.ShowMapWhenLandscape, isLandscape);

		// Preserve location service capability
		newState.PageHeaderState.CanUseLocationService = canUseLocationService;
		newState.TimetableViewState.CanUseLocationService = canUseLocationService;

		// Initialize row states from rows
		if (trainData?.Rows != null)
		{
			VerticalPageStateFactory.InitializeRowStates(newState, trainData.Rows.Length);
		}

		_currentState = newState;

		// Notify location service of new rows
		_locationService.SetTimetableRows(trainData?.Rows);

		// Reset marker toggle
		_markerToggle.ResetToggle();

		// IsRunning is false on new train data
		_currentState.PageHeaderState.IsRunning = false;
		_currentState.TimetableViewState.IsRunStarted = false;

		RaiseStateChanged(VerticalPageStateSection.All);
	}

	/// <summary>
	/// Called when run started state changes
	/// </summary>
	public void OnRunStartedChanged(bool isRunning)
	{
		_currentState.PageHeaderState.IsRunning = isRunning;
		_currentState.TimetableViewState.IsRunStarted = isRunning;

		if (!isRunning)
		{
			// Stop location service on run end
			_locationService.IsEnabled = false;
			_currentState.PageHeaderState.IsLocationServiceEnabled = false;
			_currentState.LocationServiceState.IsEnabled = false;
			_currentState.TimetableViewState.IsLocationServiceEnabled = false;

			// Disable wake lock
			if (_easterEgg.KeepScreenOnWhenRunning)
			{
				_wakeLock.DisableWakeLock();
			}

			// Reset location marker
			VerticalPageStateFactory.ResetAllRowLocationStates(_currentState);
		}
		else
		{
			// Enable wake lock if setting is on
			if (_easterEgg.KeepScreenOnWhenRunning)
			{
				_wakeLock.EnableWakeLock();
			}

			// If no active marker, set first row as around-this-station
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
				// First tap or different row or outside threshold - record and wait
				_lastTapInfo = (rowIndex, now);
				return;
			}

			// Double tap detected within threshold
			_lastTapInfo = null;
			_locationService.ForceSetLocationInfo(rowIndex, false);
		}
		else
		{
			_lastTapInfo = null;

			if (!_currentState.RowStates.TryGetValue(rowIndex, out var rowState))
				return;

			bool isLastRow = rowIndex == totalRowCount - 1;

			// Find which row currently has marker and what state it's in
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

			// Clear all rows
			VerticalPageStateFactory.ResetAllRowLocationStates(_currentState);

			if (currentMarkerState == 0)
			{
				// Undefined -> AroundThisStation on this row
				rowState.LocationState = 1;
			}
			else if (currentMarkerRow == rowIndex && currentMarkerState == 1)
			{
				// AroundThisStation -> RunningToNextStation (unless last row)
				if (!isLastRow)
				{
					rowState.LocationState = 2;
				}
				// else: reset (stays Undefined = 0)
			}
			else if (currentMarkerRow == rowIndex && currentMarkerState == 2)
			{
				// RunningToNextStation -> Undefined (reset)
				// rowState.LocationState stays 0
			}
			else
			{
				// Different row was selected -> set AroundThisStation on new row
				rowState.LocationState = 1;
			}
		}

		RaiseStateChanged(VerticalPageStateSection.RowStates | VerticalPageStateSection.TimetableView);
	}

	/// <summary>
	/// Called when location service enabled state changes
	/// </summary>
	public void OnLocationServiceEnabledChanged(bool enabled)
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
	/// Called when train info open/close is toggled
	/// </summary>
	public void OnTrainInfoOpenCloseToggled(bool isOpen)
	{
		VerticalPageStateFactory.UpdateTrainInfoAreaOpenCloseState(_currentState.TrainInfoAreaState, isOpen);
		RaiseStateChanged(VerticalPageStateSection.TrainInfoArea);
	}

	/// <summary>
	/// Called when train info open/close animation finishes
	/// </summary>
	public void OnTrainInfoOpenCloseAnimationFinished(bool wasOpen, bool canceled)
	{
		if (!canceled)
		{
			VerticalPageStateFactory.CompleteTrainInfoAreaAnimation(_currentState.TrainInfoAreaState, wasOpen);
			RaiseStateChanged(VerticalPageStateSection.TrainInfoArea);
		}
	}

	/// <summary>
	/// Called when timetable busy state changes
	/// </summary>
	public void OnTimetableBusyChanged(bool isBusy, double timetableHeightHint)
	{
		VerticalPageStateFactory.UpdateTimetableActivityIndicatorState(_currentState.TimetableActivityIndicatorState, isBusy);

		if (timetableHeightHint > 0)
		{
			_lastTimetableHeight = timetableHeightHint;
		}

		VerticalPageStateFactory.UpdateScrollViewHeight(_currentState.ScrollViewState, _lastTimetableHeight, _lastPageHeight);

		RaiseStateChanged(VerticalPageStateSection.ActivityIndicator | VerticalPageStateSection.ScrollView);
	}

	/// <summary>
	/// Called when timetable height changes
	/// </summary>
	public void OnTimetableHeightChanged(double timetableHeight, double pageHeight)
	{
		_lastTimetableHeight = timetableHeight;
		_lastPageHeight = pageHeight;

		VerticalPageStateFactory.UpdateScrollViewHeight(_currentState.ScrollViewState, timetableHeight, pageHeight);

		RaiseStateChanged(VerticalPageStateSection.ScrollView);
	}

	/// <summary>
	/// Called when device orientation changes
	/// </summary>
	public void OnDeviceOrientationChanged(bool isLandscape)
	{
		VerticalPageStateFactory.UpdateDebugMapState(
			_currentState.DebugMapState,
			_easterEgg.ShowMapWhenLandscape,
			isLandscape);

		RaiseStateChanged(VerticalPageStateSection.DebugMap);
	}

	/// <summary>
	/// Called when page height changes
	/// </summary>
	public void OnPageHeightChanged(double pageHeight)
	{
		_lastPageHeight = pageHeight;

		VerticalPageStateFactory.UpdateScrollViewHeight(_currentState.ScrollViewState, _lastTimetableHeight, pageHeight);

		RaiseStateChanged(VerticalPageStateSection.ScrollView);
	}

	/// <summary>
	/// Called when network sync auto start is requested
	/// </summary>
	public void OnNetworkSyncAutoStartRequested()
	{
		if (_locationService.NetworkSyncServiceCanStart)
		{
			// Auto-start run and location service
			if (!_currentState.PageHeaderState.IsRunning)
			{
				OnRunStartedChanged(true);
			}
			if (!_currentState.TimetableViewState.IsLocationServiceEnabled)
			{
				OnLocationServiceEnabledChanged(true);
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

		// If network sync service can start, auto-start
		if (_locationService.NetworkSyncServiceCanStart)
		{
			if (!_currentState.PageHeaderState.IsRunning)
			{
				OnRunStartedChanged(true);
			}
			if (!_currentState.TimetableViewState.IsLocationServiceEnabled)
			{
				OnLocationServiceEnabledChanged(true);
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

		_easterEgg.PropertyChanged -= OnEasterEggPropertyChanged;
		_viewHostMode.PropertyChanged -= OnViewHostModePropertyChanged;
		_locationService.CanUseServiceChanged -= OnLocationServiceCanUseChanged_Internal;
		_locationService.LocationStateChanged -= OnLocationStateChanged_Internal;
		_locationService.GpsLocationUpdated -= OnGpsLocationUpdated_Internal;
	}
}
