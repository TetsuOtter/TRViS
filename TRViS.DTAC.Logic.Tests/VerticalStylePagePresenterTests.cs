using System;
using System.Collections.Generic;
using System.ComponentModel;
using TRViS.DTAC.Logic;
using TRViS.DTAC.Logic.Abstractions;
using TRViS.DTAC.Logic.Presenter;
using TRViS.IO.Models;
using Xunit;
using TRViS.LocationService.Abstractions;

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
		public event EventHandler<Exception>? ExceptionThrown;

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

	private class FakeMarkerToggle : IMarkerToggleController
	{
		public int ResetCount { get; private set; } = 0;

		private bool _isToggled = false;
		public bool IsToggled
		{
			get => _isToggled;
			set
			{
				if (_isToggled != value)
				{
					_isToggled = value;
					PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsToggled)));
				}
			}
		}

		public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

		public void ResetToggle()
		{
			IsToggled = false;
			ResetCount++;
		}

		public void Toggle()
		{
			IsToggled = !IsToggled;
		}
	}

	private class FakeAppViewModelProvider : IAppViewModelProvider
	{
		public WorkGroup? SelectedWorkGroup { get; set; }
		public Work? SelectedWork { get; set; }

		private TrainData? _selectedTrainData;
		public TrainData? SelectedTrainData
		{
			get => _selectedTrainData;
			set
			{
				_selectedTrainData = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedTrainData)));
			}
		}

		public string? HeaderTimeFormat { get; set; }

		public event PropertyChangedEventHandler? PropertyChanged;
	}

	#endregion

	#region Helpers

	private static (
		VerticalStylePagePresenter presenter,
		FakeLocationService locationService,
		FakeMarkerToggle markerToggle,
		FakeClock clock,
		FakeAppViewModelProvider appVm
	) CreatePresenter()
	{
		var locationService = new FakeLocationService();
		var markerToggle = new FakeMarkerToggle();
		var clock = new FakeClock();
		var appVm = new FakeAppViewModelProvider();

		var presenter = new VerticalStylePagePresenter(
			locationService,
			markerToggle,
			clock,
			appVm);

		return (presenter, locationService, markerToggle, clock, appVm);
	}

	private static TrainData CreateTrainData(string destination = "Tokyo", int rowCount = 3, DateOnly? affectDate = null, string id = "train-001", int firstRowDriveTimeMM = 10)
	{
		var rows = new TimetableRow[rowCount];
		for (int i = 0; i < rowCount; i++)
		{
			rows[i] = new TimetableRow(
				Id: $"row-{i}",
				Location: new LocationInfo(i * 1000.0),
				DriveTimeMM: i == 0 ? firstRowDriveTimeMM : 10,
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
			Id: id,
			Direction: 0,
			WorkName: "Test Work",
			TrainNumber: "101",
			Destination: destination,
			AffectDate: affectDate,
			Rows: rows
		);
	}

	private static TrainData CreateTrainDataWithInfoRow(int rowCount = 3, int infoRowIndex = 0)
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
				Remarks: null,
				IsInfoRow: i == infoRowIndex
			);
		}

		return new TrainData(
			Id: "train-001",
			Direction: 0,
			WorkName: "Test Work",
			TrainNumber: "101",
			Destination: "Tokyo",
			Rows: rows
		);
	}

	#endregion

	#region SelectedTrainData change handling Tests

	[Fact]
	public void SelectedTrainDataChanged_AppliesAllStateFromTrainData()
	{
		var (presenter, locationService, markerToggle, _, appVm) = CreatePresenter();

		var trainData = CreateTrainData("Osaka", affectDate: new DateOnly(2024, 1, 15));
		appVm.SelectedTrainData = trainData;

		var state = presenter.CurrentState;
		Assert.Equal("Osaka", state.Destination.OriginalValue);
		Assert.True(state.Destination.IsVisible);
		Assert.Equal("2024年1月15日", state.PageHeaderState.AffectDateLabelText);
		Assert.Equal(trainData.Rows!.Length, state.RowStates.Count);
		Assert.Equal(trainData.Rows, locationService.LastSetRows);
		Assert.Equal(1, markerToggle.ResetCount);
		Assert.False(state.PageHeaderState.IsRunning);
	}

	/// <summary>
	/// 不具合再現: 運行開始した状態で、同じ列車に対して 1 行だけ field 編集された
	/// TrainData (= 別インスタンスだが Id と行数は同じ) が来た時、運行中状態 (IsRunning /
	/// IsRunStarted) が維持されること。旧実装は SetSelectedTrainData が無条件に
	/// false 代入していたため、WS 経由のリアルタイム編集ごとに「運行前」に戻されていた。
	/// </summary>
	[Fact]
	public void SelectedTrainDataChanged_SameIdAndRowCount_PreservesRunningState()
	{
		var (presenter, _, markerToggle, _, appVm) = CreatePresenter();
		appVm.SelectedTrainData = CreateTrainData(rowCount: 3, firstRowDriveTimeMM: 5);

		// 運行開始
		presenter.OnStartButtonClicked();
		Assert.True(presenter.CurrentState.PageHeaderState.IsRunning);
		Assert.True(presenter.CurrentState.TimetableViewState.IsRunStarted);
		int markerResetsBeforeEdit = markerToggle.ResetCount;

		// 同じ列車 (Id="train-001") に対するリアルタイム編集を模擬:
		// 別インスタンス、行数は同じ、先頭行の DriveTimeMM だけ変える。
		appVm.SelectedTrainData = CreateTrainData(rowCount: 3, firstRowDriveTimeMM: 7);

		Assert.Multiple(() =>
		{
			Assert.True(presenter.CurrentState.PageHeaderState.IsRunning,
				"運行中フラグは同一列車の field 編集で巻き戻されてはならない");
			Assert.True(presenter.CurrentState.TimetableViewState.IsRunStarted,
				"運行開始フラグも同様に維持される");
			Assert.Equal(markerResetsBeforeEdit, markerToggle.ResetCount);
		});
	}

	/// <summary>
	/// 列車切替 (Id 変化) は従来通り「運行前」にリセットされる。
	/// </summary>
	[Fact]
	public void SelectedTrainDataChanged_DifferentTrainId_ResetsRunningState()
	{
		var (presenter, _, _, _, appVm) = CreatePresenter();
		appVm.SelectedTrainData = CreateTrainData(rowCount: 3, id: "train-A");
		presenter.OnStartButtonClicked();
		Assert.True(presenter.CurrentState.PageHeaderState.IsRunning);

		appVm.SelectedTrainData = CreateTrainData(rowCount: 3, id: "train-B");

		Assert.Multiple(() =>
		{
			Assert.False(presenter.CurrentState.PageHeaderState.IsRunning);
			Assert.False(presenter.CurrentState.TimetableViewState.IsRunStarted);
		});
	}

	/// <summary>
	/// 同じ Id でも行数が変わった (= 駅が追加/削除された) ら、構造変化なので「運行前」にリセットされる。
	/// </summary>
	[Fact]
	public void SelectedTrainDataChanged_SameIdDifferentRowCount_ResetsRunningState()
	{
		var (presenter, _, _, _, appVm) = CreatePresenter();
		appVm.SelectedTrainData = CreateTrainData(rowCount: 3);
		presenter.OnStartButtonClicked();
		Assert.True(presenter.CurrentState.PageHeaderState.IsRunning);

		appVm.SelectedTrainData = CreateTrainData(rowCount: 4);

		Assert.False(presenter.CurrentState.PageHeaderState.IsRunning);
	}

	/// <summary>
	/// 同 Id + 同行数の soft 更新でも、表示用の field (Destination 等) は更新される。
	/// 運行中状態維持と表示更新は両立させる。
	/// </summary>
	[Fact]
	public void SelectedTrainDataChanged_SameIdAndRowCount_StillPropagatesFieldEdits()
	{
		var (presenter, locationService, _, _, appVm) = CreatePresenter();
		appVm.SelectedTrainData = CreateTrainData(destination: "Tokyo", rowCount: 3);

		var edited = CreateTrainData(destination: "Osaka", rowCount: 3);
		appVm.SelectedTrainData = edited;

		Assert.Multiple(() =>
		{
			Assert.Equal("Osaka", presenter.CurrentState.Destination.OriginalValue);
			// SetTimetableRows は呼ばれている (StaLocationInfo の再計算に必要)
			Assert.Equal(edited.Rows, locationService.LastSetRows);
		});
	}

	#endregion

	#region OnStartButtonClicked Tests

	[Fact]
	public void OnStartButtonClicked_ToFalse_DisablesLocationService()
	{
		var (presenter, locationService, _, _, appVm) = CreatePresenter();

		// First start running
		presenter.OnStartButtonClicked();

		// Now stop running
		presenter.OnStartButtonClicked();

		Assert.False(locationService.IsEnabled);
		Assert.False(presenter.CurrentState.PageHeaderState.IsLocationServiceEnabled);
		Assert.False(presenter.CurrentState.TimetableViewState.IsLocationServiceEnabled);
		Assert.False(presenter.CurrentState.LocationServiceState.IsEnabled);
	}

	[Fact]
	public void OnStartButtonClicked_ToFalse_ResetsRowMarkerStates()
	{
		var (presenter, _, _, _, appVm) = CreatePresenter();
		var trainData = CreateTrainData(rowCount: 3);
		appVm.SelectedTrainData = trainData;

		// Set a marker on row 1
		presenter.CurrentState.RowStates[1].LocationState = TimetableLocationState.AroundThisStation;

		// Start then stop running
		presenter.OnStartButtonClicked();
		presenter.OnStartButtonClicked();

		// All row states should be reset to undefined
		foreach (var rowState in presenter.CurrentState.RowStates.Values)
		{
			Assert.Equal(TimetableLocationState.Undefined, rowState.LocationState);
		}
	}

	[Fact]
	public void OnStartButtonClicked_ToTrue_SetsFirstRowMarker_WhenNoActiveMarker()
	{
		var (presenter, _, _, _, appVm) = CreatePresenter();
		var trainData = CreateTrainData(rowCount: 3);
		appVm.SelectedTrainData = trainData;

		// Ensure no active marker
		foreach (var rowState in presenter.CurrentState.RowStates.Values)
		{
			Assert.Equal(TimetableLocationState.Undefined, rowState.LocationState);
		}

		// Start running
		presenter.OnStartButtonClicked();

		// First row should have AroundThisStation marker
		Assert.Equal(TimetableLocationState.AroundThisStation, presenter.CurrentState.RowStates[0].LocationState);
	}

	#endregion

	#region OnRowTapped Tests

	[Fact]
	public void OnRowTapped_DoubleTapWithinThreshold_CallsForceSetLocationInfo()
	{
		var (presenter, locationService, _, clock, appVm) = CreatePresenter();
		var trainData = CreateTrainData(rowCount: 3);
		appVm.SelectedTrainData = trainData;
		presenter.OnStartButtonClicked();
		presenter.OnLocationServiceToggled();

		var baseTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		clock.UtcNow = baseTime;

		presenter.OnRowTapped(1);
		Assert.Equal(0, locationService.ForceSetLocationInfoCallCount);

		clock.UtcNow = baseTime.AddMilliseconds(200);
		presenter.OnRowTapped(1);

		Assert.Equal(1, locationService.ForceSetLocationInfoCallCount);
		Assert.Equal(1, locationService.LastForceSetRow);
	}

	[Fact]
	public void OnRowTapped_SingleTapBeforeThreshold_DoesNotCallForceSet()
	{
		var (presenter, locationService, _, clock, appVm) = CreatePresenter();
		var trainData = CreateTrainData(rowCount: 3);
		appVm.SelectedTrainData = trainData;
		presenter.OnStartButtonClicked();
		presenter.OnLocationServiceToggled();

		clock.UtcNow = DateTime.UtcNow;
		presenter.OnRowTapped(1);

		Assert.Equal(0, locationService.ForceSetLocationInfoCallCount);
	}

	[Fact]
	public void OnRowTapped_TwoTapsOutsideThreshold_DoesNotCallForceSet()
	{
		var (presenter, locationService, _, clock, appVm) = CreatePresenter();
		var trainData = CreateTrainData(rowCount: 3);
		appVm.SelectedTrainData = trainData;
		presenter.OnStartButtonClicked();
		presenter.OnLocationServiceToggled();

		var baseTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		clock.UtcNow = baseTime;

		presenter.OnRowTapped(1);

		clock.UtcNow = baseTime.AddMilliseconds(600);
		presenter.OnRowTapped(1);

		Assert.Equal(0, locationService.ForceSetLocationInfoCallCount);
	}

	[Fact]
	public void OnRowTapped_LocationServiceDisabled_CyclesMarkerState()
	{
		var (presenter, _, _, _, appVm) = CreatePresenter();
		var trainData = CreateTrainData(rowCount: 3);
		appVm.SelectedTrainData = trainData;
		presenter.OnStartButtonClicked();

		presenter.OnRowTapped(1);
		Assert.Equal(TimetableLocationState.AroundThisStation, presenter.CurrentState.RowStates[1].LocationState);

		presenter.OnRowTapped(1);
		Assert.Equal(TimetableLocationState.RunningToNextStation, presenter.CurrentState.RowStates[1].LocationState);

		presenter.OnRowTapped(1);
		Assert.Equal(TimetableLocationState.AroundThisStation, presenter.CurrentState.RowStates[1].LocationState);

		presenter.OnRowTapped(1);
		Assert.Equal(TimetableLocationState.RunningToNextStation, presenter.CurrentState.RowStates[1].LocationState);
	}

	[Fact]
	public void OnRowTapped_LastRow_LocationServiceDisabled_StaysAtAroundThisStation()
	{
		var (presenter, _, _, _, appVm) = CreatePresenter();
		var trainData = CreateTrainData(rowCount: 3);
		appVm.SelectedTrainData = trainData;
		presenter.OnStartButtonClicked();

		presenter.OnRowTapped(2);
		Assert.Equal(TimetableLocationState.AroundThisStation, presenter.CurrentState.RowStates[2].LocationState);

		presenter.OnRowTapped(2);
		Assert.Equal(TimetableLocationState.AroundThisStation, presenter.CurrentState.RowStates[2].LocationState);

		presenter.OnRowTapped(2);
		Assert.Equal(TimetableLocationState.AroundThisStation, presenter.CurrentState.RowStates[2].LocationState);
	}

	[Fact]
	public void OnRowTapped_InfoRow_IsIgnored()
	{
		var (presenter, _, _, _, appVm) = CreatePresenter();
		// Row 0 is info row; rows 1-2 are station rows
		var trainData = CreateTrainDataWithInfoRow(rowCount: 3, infoRowIndex: 0);
		appVm.SelectedTrainData = trainData;
		presenter.OnStartButtonClicked();

		// First non-info row (1) gets the initial marker
		Assert.Equal(TimetableLocationState.AroundThisStation, presenter.CurrentState.RowStates[1].LocationState);

		// Tap info row 0 - should be ignored
		presenter.OnRowTapped(0);

		Assert.Equal(TimetableLocationState.AroundThisStation, presenter.CurrentState.RowStates[1].LocationState);
		Assert.Equal(TimetableLocationState.Undefined, presenter.CurrentState.RowStates[0].LocationState);
	}

	[Fact]
	public void OnRowTapped_NotRunStarted_IsIgnored()
	{
		var (presenter, _, _, _, appVm) = CreatePresenter();
		var trainData = CreateTrainData(rowCount: 3);
		appVm.SelectedTrainData = trainData;

		presenter.OnRowTapped(0);

		foreach (var rowState in presenter.CurrentState.RowStates.Values)
		{
			Assert.Equal(TimetableLocationState.Undefined, rowState.LocationState);
		}
	}

	#endregion

	#region OnLocationServiceToggled Tests

	[Fact]
	public void OnLocationServiceToggled_ToTrue_UpdatesAllLocationServiceStates()
	{
		var (presenter, locationService, _, _, appVm) = CreatePresenter();

		presenter.OnLocationServiceToggled();

		Assert.True(locationService.IsEnabled);
		Assert.True(presenter.CurrentState.LocationServiceState.IsEnabled);
		Assert.True(presenter.CurrentState.PageHeaderState.IsLocationServiceEnabled);
		Assert.True(presenter.CurrentState.TimetableViewState.IsLocationServiceEnabled);
	}

	[Fact]
	public void OnLocationServiceToggled_ToFalse_UpdatesAllLocationServiceStates()
	{
		var (presenter, locationService, _, _, appVm) = CreatePresenter();

		presenter.OnLocationServiceToggled();
		presenter.OnLocationServiceToggled();

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
		var (presenter, locationService, _, _, appVm) = CreatePresenter();

		var stateChangedCount = 0;
		presenter.StateChanged += (_, _) => stateChangedCount++;

		var trainData = CreateTrainData(rowCount: 3);
		appVm.SelectedTrainData = trainData;
		Assert.Equal(1, stateChangedCount);

		// Dispose
		presenter.Dispose();

		// After dispose, raising events should not cause state changes or exceptions
		locationService.RaiseCanUseServiceChanged(true);
		locationService.RaiseLocationStateChanged(0, false);
		locationService.RaiseGpsLocationUpdated(35.0, 139.0, 10.0);

		// Only the one state change before dispose should have been counted
		Assert.Equal(1, stateChangedCount);
	}

	[Fact]
	public void Dispose_CalledTwice_DoesNotThrow()
	{
		var (presenter, _, _, _, appVm) = CreatePresenter();

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
		var (presenter, locationService, _, clock, appVm) = CreatePresenter();

		var stateChanges = new List<VerticalPageStateSection>();
		presenter.StateChanged += (_, e) => stateChanges.Add(e.Changed);

		// Step 1: Select train data
		var trainData = CreateTrainData("Nagoya", 5);
		appVm.SelectedTrainData = trainData;

		var state = presenter.CurrentState;
		Assert.Equal("Nagoya", state.Destination.OriginalValue);
		Assert.Equal(5, state.RowStates.Count);
		Assert.False(state.PageHeaderState.IsRunning);

		// Step 2: Start run
		presenter.OnStartButtonClicked();

		Assert.True(state.PageHeaderState.IsRunning);
		Assert.True(state.TimetableViewState.IsRunStarted);
		// First row should have AroundThisStation
		Assert.Equal(TimetableLocationState.AroundThisStation, state.RowStates[0].LocationState);

		// Step 3: Enable location service
		presenter.OnLocationServiceToggled();

		Assert.True(locationService.IsEnabled);
		Assert.True(state.TimetableViewState.IsLocationServiceEnabled);

		// Step 4: Double tap row to force set location
		var baseTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		clock.UtcNow = baseTime;
		presenter.OnRowTapped(2);  // First tap

		Assert.Equal(0, locationService.ForceSetLocationInfoCallCount);

		clock.UtcNow = baseTime.AddMilliseconds(300);
		presenter.OnRowTapped(2);  // Second tap within threshold

		Assert.Equal(1, locationService.ForceSetLocationInfoCallCount);
		Assert.Equal(2, locationService.LastForceSetRow);

		// Step 5: Location state changes from service
		locationService.RaiseLocationStateChanged(2, false); // Around station 2

		Assert.Equal(TimetableLocationState.AroundThisStation, state.RowStates[2].LocationState);

		// Step 6: Stop run
		presenter.OnStartButtonClicked();

		Assert.False(state.PageHeaderState.IsRunning);
		Assert.False(locationService.IsEnabled);
		// All row markers should be reset
		foreach (var rowState in state.RowStates.Values)
		{
			Assert.Equal(TimetableLocationState.Undefined, rowState.LocationState);
		}

		// Verify StateChanged was raised multiple times throughout
		Assert.True(stateChanges.Count > 0);
	}

	[Fact]
	public void EndToEnd_TrainSelected_TapWithoutLocationService_CyclesMarkers()
	{
		var (presenter, _, _, _, appVm) = CreatePresenter();

		var trainData = CreateTrainData(rowCount: 4);
		appVm.SelectedTrainData = trainData;
		presenter.OnStartButtonClicked();
		// Do NOT enable location service

		// Row 0 gets marker from run start
		Assert.Equal(TimetableLocationState.AroundThisStation, presenter.CurrentState.RowStates[0].LocationState);

		// Tap row 2 - should move marker there
		presenter.OnRowTapped(2);
		Assert.Equal(TimetableLocationState.AroundThisStation, presenter.CurrentState.RowStates[2].LocationState);
		Assert.Equal(TimetableLocationState.Undefined, presenter.CurrentState.RowStates[0].LocationState);

		// Tap row 2 again - AroundThisStation -> RunningToNextStation
		presenter.OnRowTapped(2);
		Assert.Equal(TimetableLocationState.RunningToNextStation, presenter.CurrentState.RowStates[2].LocationState);

		// Tap row 2 again - RunningToNextStation -> AroundThisStation (marker never disappears mid-run)
		presenter.OnRowTapped(2);
		Assert.Equal(TimetableLocationState.AroundThisStation, presenter.CurrentState.RowStates[2].LocationState);
	}

	#endregion

	#region Network Sync Tests

	[Fact]
	public void OnNetworkSyncAutoStartRequested_NetworkSyncCanStart_StartsRunAndLocationService()
	{
		var (presenter, locationService, _, _, appVm) = CreatePresenter();
		locationService.NetworkSyncServiceCanStart = true;

		presenter.OnNetworkSyncAutoStartRequested();

		Assert.True(presenter.CurrentState.PageHeaderState.IsRunning);
		Assert.True(presenter.CurrentState.TimetableViewState.IsLocationServiceEnabled);
	}

	[Fact]
	public void OnNetworkSyncAutoStartRequested_NetworkSyncCanNotStart_DoesNothing()
	{
		var (presenter, locationService, _, _, appVm) = CreatePresenter();
		locationService.NetworkSyncServiceCanStart = false;

		presenter.OnNetworkSyncAutoStartRequested();

		Assert.False(presenter.CurrentState.PageHeaderState.IsRunning);
		Assert.False(presenter.CurrentState.TimetableViewState.IsLocationServiceEnabled);
	}

	#endregion

}
