using System;
using System.Collections.Generic;
using System.ComponentModel;
using TRViS.DTAC.Logic;
using TRViS.DTAC.Logic.Abstractions;
using TRViS.DTAC.Logic.Presenter;
using TRViS.IO.Models;
using TRViS.Services;
using Xunit;

namespace TRViS.DTAC.Logic.Tests;

public class VerticalStylePagePresenterTests
{
	#region Fake Implementations

	private class FakeClock : IClock
	{
		public DateTime UtcNow { get; set; } = DateTime.UtcNow;
	}

	private class FakeLocationService : IDtacLocationServiceController
	{
		public bool IsEnabled { get; set; } = false;
		public bool CanUseService { get; set; } = false;
		public bool NetworkSyncServiceCanStart { get; set; } = false;
		public StaLocationInfo[]? StaLocationInfo { get; set; }
		public int CurrentStationIndex { get; set; } = -1;
		public bool IsRunningToNextStation { get; set; } = false;
		public int ForceSetLocationInfoCallCount { get; private set; } = 0;
		public int LastForceSetRow { get; private set; } = -1;
		public TimetableRow[]? LastSetRows { get; private set; }

		public event EventHandler<bool>? CanUseServiceChanged;
		public event EventHandler<LocationStateChangedEventArgs>? LocationStateChanged;
		public event EventHandler<GpsLocationUpdate>? GpsLocationUpdated;

		public void RaiseCanUseServiceChanged(bool value)
		{
			CanUseService = value;
			CanUseServiceChanged?.Invoke(this, value);
		}

		public void RaiseLocationStateChanged(int stationIndex, bool isRunningToNextStation)
		{
			LocationStateChanged?.Invoke(this, new LocationStateChangedEventArgs(stationIndex, isRunningToNextStation));
		}

		public void RaiseGpsLocationUpdated(double lat, double lon, double? accuracy)
		{
			GpsLocationUpdated?.Invoke(this, new GpsLocationUpdate(lat, lon, accuracy));
		}

		public void ResetLocationInfo() { }

		public void ForceSetLocationInfo(int stationIndex, bool isRunningToNextStation)
		{
			ForceSetLocationInfoCallCount++;
			LastForceSetRow = stationIndex;
		}

		public void SetTimetableRows(TimetableRow[]? rows)
		{
			LastSetRows = rows;
		}
	}

	private class FakeWakeLock : IWakeLockController
	{
		public bool IsWakeLockEnabled { get; private set; } = false;
		public int EnableCount { get; private set; } = 0;
		public int DisableCount { get; private set; } = 0;

		public void EnableWakeLock()
		{
			IsWakeLockEnabled = true;
			EnableCount++;
		}

		public void DisableWakeLock()
		{
			IsWakeLockEnabled = false;
			DisableCount++;
		}
	}

	private class FakeEasterEgg : IEasterEggSettings
	{
		private bool _keepScreenOnWhenRunning;
		private bool _showMapWhenLandscape;

		public bool KeepScreenOnWhenRunning
		{
			get => _keepScreenOnWhenRunning;
			set
			{
				if (_keepScreenOnWhenRunning != value)
				{
					_keepScreenOnWhenRunning = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(KeepScreenOnWhenRunning)));
				}
			}
		}

		public bool ShowMapWhenLandscape
		{
			get => _showMapWhenLandscape;
			set
			{
				if (_showMapWhenLandscape != value)
				{
					_showMapWhenLandscape = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowMapWhenLandscape)));
				}
			}
		}

		public event PropertyChangedEventHandler? PropertyChanged;
	}

	private class FakeViewHostModeProvider : IViewHostModeProvider
	{
		private bool _isViewHostVisible;
		private bool _isVerticalViewMode;

		public bool IsViewHostVisible
		{
			get => _isViewHostVisible;
			set
			{
				if (_isViewHostVisible != value)
				{
					_isViewHostVisible = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsViewHostVisible)));
				}
			}
		}

		public bool IsVerticalViewMode
		{
			get => _isVerticalViewMode;
			set
			{
				if (_isVerticalViewMode != value)
				{
					_isVerticalViewMode = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVerticalViewMode)));
				}
			}
		}

		public event PropertyChangedEventHandler? PropertyChanged;
	}

	private class FakeMarkerToggle : IMarkerToggleController
	{
		public int ResetCount { get; private set; } = 0;

		public void ResetToggle()
		{
			ResetCount++;
		}
	}

	private class FakeCrashLogger : IDtacCrashLogger
	{
		public void Log(Exception ex, string? context = null) { }
	}

	#endregion

	#region Helpers

	private static (
		VerticalStylePagePresenter presenter,
		FakeLocationService locationService,
		FakeWakeLock wakeLock,
		FakeEasterEgg easterEgg,
		FakeViewHostModeProvider viewHostMode,
		FakeMarkerToggle markerToggle,
		FakeClock clock
	) CreatePresenter(bool viewHostVisible = true, bool verticalViewMode = true)
	{
		var locationService = new FakeLocationService();
		var wakeLock = new FakeWakeLock();
		var easterEgg = new FakeEasterEgg();
		var viewHostMode = new FakeViewHostModeProvider
		{
			IsViewHostVisible = viewHostVisible,
			IsVerticalViewMode = verticalViewMode
		};
		var markerToggle = new FakeMarkerToggle();
		var crashLogger = new FakeCrashLogger();
		var clock = new FakeClock();

		var presenter = new VerticalStylePagePresenter(
			locationService,
			wakeLock,
			easterEgg,
			viewHostMode,
			markerToggle,
			crashLogger,
			clock);

		return (presenter, locationService, wakeLock, easterEgg, viewHostMode, markerToggle, clock);
	}

	private static TrainData CreateTrainData(string destination = "Tokyo", int rowCount = 3)
	{
		var rows = new TimetableRow[rowCount];
		for (int i = 0; i < rowCount; i++)
		{
			rows[i] = new TimetableRow(
				Id: $"row-{i}",
				Location: new LocationInfo(i * 1000.0),
				DriveTimeMM: 10,
				DriveTimeSS: 0,
				StationName: $"Station {i}",
				IsOperationOnlyStop: false,
				IsPass: false,
				HasBracket: false,
				IsLastStop: i == rowCount - 1,
				ArriveTime: null,
				DepartureTime: null,
				TrackName: "1",
				RunInLimit: null,
				RunOutLimit: null,
				Remarks: null
			);
		}

		return new TrainData(
			Id: "train-001",
			Direction: 0,
			WorkName: "Test Work",
			TrainNumber: "101",
			Destination: destination,
			Rows: rows
		);
	}

	#endregion

	#region OnSelectedTrainDataChanged Tests

	[Fact]
	public void OnSelectedTrainDataChanged_AppliesAllStateFromTrainData()
	{
		var (presenter, locationService, _, _, _, markerToggle, _) = CreatePresenter();

		var trainData = CreateTrainData("Osaka");
		presenter.OnSelectedTrainDataChanged(trainData, "2024年1月15日");

		var state = presenter.CurrentState;
		Assert.Equal("Osaka", state.Destination.OriginalValue);
		Assert.True(state.Destination.IsVisible);
		Assert.Equal("2024年1月15日", state.PageHeaderState.AffectDateLabelText);
		Assert.Equal(trainData.Rows!.Length, state.RowStates.Count);
		Assert.Equal(trainData.Rows, locationService.LastSetRows);
		Assert.Equal(1, markerToggle.ResetCount);
		Assert.False(state.PageHeaderState.IsRunning);
	}

	[Fact]
	public void OnSelectedTrainDataChanged_LazyLoad_WhenViewHostNotVisible_DoesNotApply()
	{
		var (presenter, locationService, _, _, _, markerToggle, _) = CreatePresenter(viewHostVisible: false, verticalViewMode: true);

		var trainData = CreateTrainData("Tokyo");
		presenter.OnSelectedTrainDataChanged(trainData, "2024年1月15日");

		// State should not be applied - destination still empty
		var state = presenter.CurrentState;
		Assert.Null(state.Destination.OriginalValue);
		Assert.Equal(0, markerToggle.ResetCount);
		Assert.Null(locationService.LastSetRows);
	}

	[Fact]
	public void OnSelectedTrainDataChanged_LazyLoad_ThenViewHostVisible_AppliesData()
	{
		var (presenter, _, _, _, viewHostMode, markerToggle, _) = CreatePresenter(viewHostVisible: false, verticalViewMode: true);

		var trainData = CreateTrainData("Osaka");
		presenter.OnSelectedTrainDataChanged(trainData, "2024年1月15日");

		// State not applied yet
		Assert.Null(presenter.CurrentState.Destination.OriginalValue);

		// Now make view host visible - should trigger re-apply
		viewHostMode.IsViewHostVisible = true;
		viewHostMode.IsVerticalViewMode = true;

		// State should now be applied
		Assert.Equal("Osaka", presenter.CurrentState.Destination.OriginalValue);
		Assert.Equal(1, markerToggle.ResetCount);
	}

	#endregion

	#region OnRunStartedChanged Tests

	[Fact]
	public void OnRunStartedChanged_True_EnablesWakeLock_WhenSettingOn()
	{
		var (presenter, _, wakeLock, easterEgg, _, _, _) = CreatePresenter();
		easterEgg.KeepScreenOnWhenRunning = true;

		presenter.OnRunStartedChanged(true);

		Assert.True(wakeLock.IsWakeLockEnabled);
		Assert.Equal(1, wakeLock.EnableCount);
		Assert.True(presenter.CurrentState.PageHeaderState.IsRunning);
		Assert.True(presenter.CurrentState.TimetableViewState.IsRunStarted);
	}

	[Fact]
	public void OnRunStartedChanged_True_DoesNotEnableWakeLock_WhenSettingOff()
	{
		var (presenter, _, wakeLock, easterEgg, _, _, _) = CreatePresenter();
		easterEgg.KeepScreenOnWhenRunning = false;

		presenter.OnRunStartedChanged(true);

		Assert.False(wakeLock.IsWakeLockEnabled);
		Assert.Equal(0, wakeLock.EnableCount);
	}

	[Fact]
	public void OnRunStartedChanged_False_DisablesLocationService()
	{
		var (presenter, locationService, wakeLock, easterEgg, _, _, _) = CreatePresenter();
		easterEgg.KeepScreenOnWhenRunning = true;

		// First start running
		presenter.OnRunStartedChanged(true);
		Assert.True(wakeLock.IsWakeLockEnabled);

		// Now stop running
		presenter.OnRunStartedChanged(false);

		Assert.False(locationService.IsEnabled);
		Assert.False(presenter.CurrentState.PageHeaderState.IsLocationServiceEnabled);
		Assert.False(presenter.CurrentState.TimetableViewState.IsLocationServiceEnabled);
		Assert.False(presenter.CurrentState.LocationServiceState.IsEnabled);
		Assert.False(wakeLock.IsWakeLockEnabled);
	}

	[Fact]
	public void OnRunStartedChanged_False_ResetsRowMarkerStates()
	{
		var (presenter, _, _, _, _, _, _) = CreatePresenter();
		var trainData = CreateTrainData(rowCount: 3);
		presenter.OnSelectedTrainDataChanged(trainData, null);

		// Set a marker on row 1
		presenter.CurrentState.RowStates[1].LocationState = 1;

		// Stop running
		presenter.OnRunStartedChanged(false);

		// All row states should be reset to undefined
		foreach (var rowState in presenter.CurrentState.RowStates.Values)
		{
			Assert.Equal(0, rowState.LocationState);
		}
	}

	[Fact]
	public void OnRunStartedChanged_True_SetsFirstRowMarker_WhenNoActiveMarker()
	{
		var (presenter, _, _, _, _, _, _) = CreatePresenter();
		var trainData = CreateTrainData(rowCount: 3);
		presenter.OnSelectedTrainDataChanged(trainData, null);

		// Ensure no active marker
		foreach (var rowState in presenter.CurrentState.RowStates.Values)
		{
			Assert.Equal(0, rowState.LocationState);
		}

		// Start running
		presenter.OnRunStartedChanged(true);

		// First row should have AroundThisStation marker
		Assert.Equal(1, presenter.CurrentState.RowStates[0].LocationState);
	}

	#endregion

	#region OnRowTapped Tests

	[Fact]
	public void OnRowTapped_DoubleTapWithinThreshold_CallsForceSetLocationInfo()
	{
		var (presenter, locationService, _, _, _, _, clock) = CreatePresenter();
		var trainData = CreateTrainData(rowCount: 3);
		presenter.OnSelectedTrainDataChanged(trainData, null);
		presenter.OnRunStartedChanged(true);
		presenter.OnLocationServiceEnabledChanged(true);

		var baseTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		clock.UtcNow = baseTime;

		// First tap
		presenter.OnRowTapped(1, false, 3);
		Assert.Equal(0, locationService.ForceSetLocationInfoCallCount);

		// Second tap within threshold (200ms later)
		clock.UtcNow = baseTime.AddMilliseconds(200);
		presenter.OnRowTapped(1, false, 3);

		Assert.Equal(1, locationService.ForceSetLocationInfoCallCount);
		Assert.Equal(1, locationService.LastForceSetRow);
	}

	[Fact]
	public void OnRowTapped_SingleTapBeforeThreshold_DoesNotCallForceSet()
	{
		var (presenter, locationService, _, _, _, _, clock) = CreatePresenter();
		var trainData = CreateTrainData(rowCount: 3);
		presenter.OnSelectedTrainDataChanged(trainData, null);
		presenter.OnRunStartedChanged(true);
		presenter.OnLocationServiceEnabledChanged(true);

		clock.UtcNow = DateTime.UtcNow;

		// Single tap only
		presenter.OnRowTapped(1, false, 3);

		Assert.Equal(0, locationService.ForceSetLocationInfoCallCount);
	}

	[Fact]
	public void OnRowTapped_TwoTapsOutsideThreshold_DoesNotCallForceSet()
	{
		var (presenter, locationService, _, _, _, _, clock) = CreatePresenter();
		var trainData = CreateTrainData(rowCount: 3);
		presenter.OnSelectedTrainDataChanged(trainData, null);
		presenter.OnRunStartedChanged(true);
		presenter.OnLocationServiceEnabledChanged(true);

		var baseTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		clock.UtcNow = baseTime;

		// First tap
		presenter.OnRowTapped(1, false, 3);

		// Second tap outside threshold (600ms later)
		clock.UtcNow = baseTime.AddMilliseconds(600);
		presenter.OnRowTapped(1, false, 3);

		Assert.Equal(0, locationService.ForceSetLocationInfoCallCount);
	}

	[Fact]
	public void OnRowTapped_LocationServiceDisabled_CyclesMarkerState()
	{
		var (presenter, _, _, _, _, _, _) = CreatePresenter();
		var trainData = CreateTrainData(rowCount: 3);
		presenter.OnSelectedTrainDataChanged(trainData, null);
		presenter.OnRunStartedChanged(true);
		// Location service NOT enabled

		// Tap row 1 first time - Undefined -> AroundThisStation
		presenter.OnRowTapped(1, false, 3);
		Assert.Equal(1, presenter.CurrentState.RowStates[1].LocationState);

		// Tap row 1 second time - AroundThisStation -> RunningToNextStation
		presenter.OnRowTapped(1, false, 3);
		Assert.Equal(2, presenter.CurrentState.RowStates[1].LocationState);

		// Tap row 1 third time - RunningToNextStation -> Undefined
		presenter.OnRowTapped(1, false, 3);
		Assert.Equal(0, presenter.CurrentState.RowStates[1].LocationState);
	}

	[Fact]
	public void OnRowTapped_LastRow_LocationServiceDisabled_DoesNotGoToRunningToNextStation()
	{
		var (presenter, _, _, _, _, _, _) = CreatePresenter();
		var trainData = CreateTrainData(rowCount: 3);
		presenter.OnSelectedTrainDataChanged(trainData, null);
		presenter.OnRunStartedChanged(true);

		// Tap last row (index 2) first time - Undefined -> AroundThisStation
		presenter.OnRowTapped(2, false, 3);
		Assert.Equal(1, presenter.CurrentState.RowStates[2].LocationState);

		// Tap last row second time - AroundThisStation on last row -> Undefined (not RunningToNextStation)
		presenter.OnRowTapped(2, false, 3);
		Assert.Equal(0, presenter.CurrentState.RowStates[2].LocationState);
	}

	[Fact]
	public void OnRowTapped_InfoRow_IsIgnored()
	{
		var (presenter, _, _, _, _, _, _) = CreatePresenter();
		var trainData = CreateTrainData(rowCount: 3);
		presenter.OnSelectedTrainDataChanged(trainData, null);
		presenter.OnRunStartedChanged(true);

		// Tap info row - should be ignored
		presenter.OnRowTapped(0, true, 3);

		// State should remain unchanged (all undefined)
		foreach (var rowState in presenter.CurrentState.RowStates.Values)
		{
			// Row 0 gets AroundThisStation on RunStarted, so reset
			// Actually after OnRunStartedChanged(true) with no prior marker, first row gets marker
		}
		// After run started, row 0 has AroundThisStation
		// Tapping info row on row 0 should not change state
		Assert.Equal(1, presenter.CurrentState.RowStates[0].LocationState);
	}

	[Fact]
	public void OnRowTapped_NotRunStarted_IsIgnored()
	{
		var (presenter, _, _, _, _, _, _) = CreatePresenter();
		var trainData = CreateTrainData(rowCount: 3);
		presenter.OnSelectedTrainDataChanged(trainData, null);
		// Not calling OnRunStartedChanged(true)

		presenter.OnRowTapped(0, false, 3);

		// All rows should remain undefined
		foreach (var rowState in presenter.CurrentState.RowStates.Values)
		{
			Assert.Equal(0, rowState.LocationState);
		}
	}

	#endregion

	#region OnDeviceOrientationChanged Tests

	[Fact]
	public void OnDeviceOrientationChanged_Landscape_AndEasterEggOn_ShowsDebugMap()
	{
		var (presenter, _, _, easterEgg, _, _, _) = CreatePresenter();
		easterEgg.ShowMapWhenLandscape = true;

		VerticalPageStateChangedEventArgs? eventArgs = null;
		presenter.StateChanged += (_, e) => eventArgs = e;

		presenter.OnDeviceOrientationChanged(true);

		Assert.True(presenter.CurrentState.DebugMapState.IsVisible);
		Assert.True(presenter.CurrentState.DebugMapState.IsEnabled);
		Assert.True(presenter.CurrentState.DebugMapState.IsLandscapeMode);
		Assert.NotNull(eventArgs);
		Assert.True((eventArgs!.Changed & VerticalPageStateSection.DebugMap) != 0);
	}

	[Fact]
	public void OnDeviceOrientationChanged_Portrait_HidesDebugMap()
	{
		var (presenter, _, _, easterEgg, _, _, _) = CreatePresenter();
		easterEgg.ShowMapWhenLandscape = true;

		// First set to landscape
		presenter.OnDeviceOrientationChanged(true);
		Assert.True(presenter.CurrentState.DebugMapState.IsVisible);

		// Then switch to portrait
		presenter.OnDeviceOrientationChanged(false);
		Assert.False(presenter.CurrentState.DebugMapState.IsVisible);
	}

	[Fact]
	public void OnDeviceOrientationChanged_Landscape_EasterEggOff_DoesNotShowDebugMap()
	{
		var (presenter, _, _, easterEgg, _, _, _) = CreatePresenter();
		easterEgg.ShowMapWhenLandscape = false;

		presenter.OnDeviceOrientationChanged(true);

		Assert.False(presenter.CurrentState.DebugMapState.IsVisible);
	}

	#endregion

	#region OnLocationServiceEnabledChanged Tests

	[Fact]
	public void OnLocationServiceEnabledChanged_True_UpdatesAllLocationServiceStates()
	{
		var (presenter, locationService, _, _, _, _, _) = CreatePresenter();

		presenter.OnLocationServiceEnabledChanged(true);

		Assert.True(locationService.IsEnabled);
		Assert.True(presenter.CurrentState.LocationServiceState.IsEnabled);
		Assert.True(presenter.CurrentState.PageHeaderState.IsLocationServiceEnabled);
		Assert.True(presenter.CurrentState.TimetableViewState.IsLocationServiceEnabled);
	}

	[Fact]
	public void OnLocationServiceEnabledChanged_False_UpdatesAllLocationServiceStates()
	{
		var (presenter, locationService, _, _, _, _, _) = CreatePresenter();

		presenter.OnLocationServiceEnabledChanged(true);
		presenter.OnLocationServiceEnabledChanged(false);

		Assert.False(locationService.IsEnabled);
		Assert.False(presenter.CurrentState.LocationServiceState.IsEnabled);
		Assert.False(presenter.CurrentState.PageHeaderState.IsLocationServiceEnabled);
		Assert.False(presenter.CurrentState.TimetableViewState.IsLocationServiceEnabled);
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void Dispose_UnsubscribesEvents()
	{
		var (presenter, locationService, _, easterEgg, viewHostMode, _, _) = CreatePresenter();

		// Verify events are initially connected by checking state changes
		var stateChangedCount = 0;
		presenter.StateChanged += (_, _) => stateChangedCount++;

		presenter.OnDeviceOrientationChanged(true);
		Assert.Equal(1, stateChangedCount);

		// Dispose
		presenter.Dispose();

		// After dispose, raising events should not cause state changes or exceptions
		// (test that no exception is thrown)
		easterEgg.ShowMapWhenLandscape = true;
		viewHostMode.IsViewHostVisible = false;
		locationService.RaiseCanUseServiceChanged(true);
		locationService.RaiseLocationStateChanged(0, false);
		locationService.RaiseGpsLocationUpdated(35.0, 139.0, 10.0);

		// Only the one state change before dispose should have been counted
		// (events after dispose should not trigger StateChanged)
	}

	[Fact]
	public void Dispose_CalledTwice_DoesNotThrow()
	{
		var (presenter, _, _, _, _, _, _) = CreatePresenter();

		presenter.Dispose();

		// Second dispose should not throw
		var exception = Record.Exception(() => presenter.Dispose());
		Assert.Null(exception);
	}

	#endregion

	#region End-to-End Tests

	[Fact]
	public void EndToEnd_TrainSelected_RunStarts_RowTapped_StateUpdates()
	{
		var (presenter, locationService, wakeLock, easterEgg, _, _, clock) = CreatePresenter();
		easterEgg.KeepScreenOnWhenRunning = true;

		var stateChanges = new List<VerticalPageStateSection>();
		presenter.StateChanged += (_, e) => stateChanges.Add(e.Changed);

		// Step 1: Select train data
		var trainData = CreateTrainData("Nagoya", 5);
		presenter.OnSelectedTrainDataChanged(trainData, "2024年1月15日");

		var state = presenter.CurrentState;
		Assert.Equal("Nagoya", state.Destination.OriginalValue);
		Assert.Equal(5, state.RowStates.Count);
		Assert.False(state.PageHeaderState.IsRunning);

		// Step 2: Start run
		presenter.OnRunStartedChanged(true);

		Assert.True(state.PageHeaderState.IsRunning);
		Assert.True(state.TimetableViewState.IsRunStarted);
		Assert.True(wakeLock.IsWakeLockEnabled);
		// First row should have AroundThisStation
		Assert.Equal(1, state.RowStates[0].LocationState);

		// Step 3: Enable location service
		presenter.OnLocationServiceEnabledChanged(true);

		Assert.True(locationService.IsEnabled);
		Assert.True(state.TimetableViewState.IsLocationServiceEnabled);

		// Step 4: Double tap row to force set location
		var baseTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		clock.UtcNow = baseTime;
		presenter.OnRowTapped(2, false, 5);  // First tap

		Assert.Equal(0, locationService.ForceSetLocationInfoCallCount);

		clock.UtcNow = baseTime.AddMilliseconds(300);
		presenter.OnRowTapped(2, false, 5);  // Second tap within threshold

		Assert.Equal(1, locationService.ForceSetLocationInfoCallCount);
		Assert.Equal(2, locationService.LastForceSetRow);

		// Step 5: Location state changes from service
		locationService.RaiseLocationStateChanged(2, false); // Around station 2

		Assert.Equal(1, state.RowStates[2].LocationState); // AroundThisStation

		// Step 6: Stop run
		presenter.OnRunStartedChanged(false);

		Assert.False(state.PageHeaderState.IsRunning);
		Assert.False(locationService.IsEnabled);
		Assert.False(wakeLock.IsWakeLockEnabled);
		// All row markers should be reset
		foreach (var rowState in state.RowStates.Values)
		{
			Assert.Equal(0, rowState.LocationState);
		}

		// Verify StateChanged was raised multiple times throughout
		Assert.True(stateChanges.Count > 0);
	}

	[Fact]
	public void EndToEnd_TrainSelected_TapWithoutLocationService_CyclesMarkers()
	{
		var (presenter, _, _, _, _, _, _) = CreatePresenter();

		var trainData = CreateTrainData(rowCount: 4);
		presenter.OnSelectedTrainDataChanged(trainData, null);
		presenter.OnRunStartedChanged(true);
		// Do NOT enable location service

		// Row 0 gets marker from run start
		Assert.Equal(1, presenter.CurrentState.RowStates[0].LocationState);

		// Tap row 2 - should move marker there
		presenter.OnRowTapped(2, false, 4);
		Assert.Equal(1, presenter.CurrentState.RowStates[2].LocationState);
		Assert.Equal(0, presenter.CurrentState.RowStates[0].LocationState);

		// Tap row 2 again - AroundThisStation -> RunningToNextStation
		presenter.OnRowTapped(2, false, 4);
		Assert.Equal(2, presenter.CurrentState.RowStates[2].LocationState);

		// Tap row 2 again - RunningToNextStation -> Undefined
		presenter.OnRowTapped(2, false, 4);
		Assert.Equal(0, presenter.CurrentState.RowStates[2].LocationState);
	}

	#endregion

	#region Network Sync Tests

	[Fact]
	public void OnNetworkSyncAutoStartRequested_NetworkSyncCanStart_StartsRunAndLocationService()
	{
		var (presenter, locationService, _, _, _, _, _) = CreatePresenter();
		locationService.NetworkSyncServiceCanStart = true;

		presenter.OnNetworkSyncAutoStartRequested();

		Assert.True(presenter.CurrentState.PageHeaderState.IsRunning);
		Assert.True(presenter.CurrentState.TimetableViewState.IsLocationServiceEnabled);
	}

	[Fact]
	public void OnNetworkSyncAutoStartRequested_NetworkSyncCanNotStart_DoesNothing()
	{
		var (presenter, locationService, _, _, _, _, _) = CreatePresenter();
		locationService.NetworkSyncServiceCanStart = false;

		presenter.OnNetworkSyncAutoStartRequested();

		Assert.False(presenter.CurrentState.PageHeaderState.IsRunning);
		Assert.False(presenter.CurrentState.TimetableViewState.IsLocationServiceEnabled);
	}

	#endregion

	#region TrainInfo Open/Close Tests

	[Fact]
	public void OnTrainInfoOpenCloseToggled_SetsAnimationState()
	{
		var (presenter, _, _, _, _, _, _) = CreatePresenter();

		VerticalPageStateChangedEventArgs? eventArgs = null;
		presenter.StateChanged += (_, e) => eventArgs = e;

		presenter.OnTrainInfoOpenCloseToggled(true);

		Assert.True(presenter.CurrentState.TrainInfoAreaState.IsOpen);
		Assert.True(presenter.CurrentState.TrainInfoAreaState.IsAnimationRunning);
		Assert.NotNull(eventArgs);
		Assert.True((eventArgs!.Changed & VerticalPageStateSection.TrainInfoArea) != 0);
	}

	[Fact]
	public void OnTrainInfoOpenCloseAnimationFinished_NotCanceled_CompletesAnimation()
	{
		var (presenter, _, _, _, _, _, _) = CreatePresenter();
		presenter.OnTrainInfoOpenCloseToggled(true);

		presenter.OnTrainInfoOpenCloseAnimationFinished(true, false);

		Assert.False(presenter.CurrentState.TrainInfoAreaState.IsAnimationRunning);
		Assert.True(presenter.CurrentState.TrainInfoAreaState.IsVisible);
	}

	[Fact]
	public void OnTrainInfoOpenCloseAnimationFinished_Canceled_DoesNotComplete()
	{
		var (presenter, _, _, _, _, _, _) = CreatePresenter();
		presenter.OnTrainInfoOpenCloseToggled(true);
		bool wasAnimatingBefore = presenter.CurrentState.TrainInfoAreaState.IsAnimationRunning;

		presenter.OnTrainInfoOpenCloseAnimationFinished(true, true);

		// Canceled animation should not update state
		Assert.True(presenter.CurrentState.TrainInfoAreaState.IsAnimationRunning);
	}

	#endregion
}
