using Xunit;
using TRViS.DTAC.Logic;
using System;
using TRViS.IO.Models;

namespace TRViS.DTAC.Logic.Tests;

public class VerticalPageStateFactoryTests
{
	[Fact]
	public void CreateStateFromTrainData_WithValidData_ReturnsCompleteState()
	{
		// Arrange
		var trainData = new TrainData(
			Id: "train-001",
			WorkName: "Test Work",
			AffectDate: new DateOnly(2024, 1, 15),
			TrainNumber: "101",
			MaxSpeed: "320 km/h",
			SpeedType: "Shinkansen",
			NominalTractiveCapacity: "16 cars",
			CarCount: 16,
			Destination: "Tokyo Station",
			BeginRemarks: "Test remarks",
			AfterRemarks: null,
			Remarks: null,
			BeforeDeparture: "10 minutes",
			TrainInfo: "Shinkansen 101",
			Rows: null,
			Direction: 0,
			DayCount: 2
		);

		// Act
		var state = VerticalPageStateFactory.CreateStateFromTrainData(
			trainData,
			affectDate: "2024年1月15日",
			isLocationServiceEnabled: true,
			pageHeight: 800,
			contentOtherThanTimetableHeight: 200);

		// Assert
		Assert.NotNull(state);
		Assert.NotNull(state.PageHeaderState);
		Assert.Equal("2024年1月15日", state.PageHeaderState.AffectDateLabelText);
		Assert.True(state.PageHeaderState.IsLocationServiceEnabled);
		Assert.Equal("Tokyo Station", state.Destination.OriginalValue);
		Assert.Equal("Shinkansen 101", state.TrainInfoAreaState.TrainInfoText);
		Assert.Equal("10 minutes", state.TrainInfoAreaState.BeforeDepartureText);
		Assert.Equal(2, state.NextDayIndicatorState.DayCount);
		Assert.Equal(200, state.ScrollViewState.NonTimetableContentHeight);
	}

	[Fact]
	public void CreateStateFromTrainData_WithNullData_ReturnsEmptyState()
	{
		// Act
		var state = VerticalPageStateFactory.CreateStateFromTrainData(
			trainData: null,
			affectDate: null,
			isLocationServiceEnabled: false,
			pageHeight: 800,
			contentOtherThanTimetableHeight: 200);

		// Assert
		Assert.NotNull(state);
		Assert.Empty(state.TrainInfoAreaState.TrainInfoText);
		Assert.Empty(state.TrainInfoAreaState.BeforeDepartureText);
		Assert.Equal(0, state.NextDayIndicatorState.DayCount);
		Assert.False(state.PageHeaderState.IsLocationServiceEnabled);
	}

	[Fact]
	public void UpdateDestinationState_WithValidDestination_SetsVisibleAndText()
	{
		// Arrange
		var destinationInfo = new DestinationInfo();

		// Act
		VerticalPageStateFactory.UpdateDestinationState(destinationInfo, "Tokyo Station");

		// Assert
		Assert.NotNull(destinationInfo);
		Assert.Equal("Tokyo Station", destinationInfo.OriginalValue);
	}

	[Fact]
	public void UpdateDestinationState_WithNull_SetsNotVisible()
	{
		// Arrange
		var destinationInfo = new DestinationInfo();

		// Act
		VerticalPageStateFactory.UpdateDestinationState(destinationInfo, null);

		// Assert
		Assert.False(destinationInfo.IsVisible);
		Assert.Null(destinationInfo.Text);
	}

	[Fact]
	public void UpdateNextDayIndicatorState_WithDayCount_SetsValue()
	{
		// Arrange
		var nextDayState = new NextDayIndicatorState();

		// Act
		VerticalPageStateFactory.UpdateNextDayIndicatorState(nextDayState, 3);

		// Assert
		Assert.Equal(3, nextDayState.DayCount);
	}

	[Fact]
	public void UpdateNextDayIndicatorState_WithZeroDayCount_SetsValue()
	{
		// Arrange
		var nextDayState = new NextDayIndicatorState();

		// Act
		VerticalPageStateFactory.UpdateNextDayIndicatorState(nextDayState, 0);

		// Assert
		Assert.Equal(0, nextDayState.DayCount);
	}

	[Fact]
	public void UpdateTrainInfoAreaOpenCloseState_ToOpen_SetsOpenState()
	{
		// Arrange
		var trainInfoState = new TrainInfoAreaState();

		// Act
		VerticalPageStateFactory.UpdateTrainInfoAreaOpenCloseState(trainInfoState, isToOpen: true);

		// Assert
		Assert.True(trainInfoState.IsOpen);
		Assert.True(trainInfoState.IsVisible);
		Assert.True(trainInfoState.IsAnimationRunning);
	}

	[Fact]
	public void UpdateTrainInfoAreaOpenCloseState_ToClose_SetsClosedState()
	{
		// Arrange
		var trainInfoState = new TrainInfoAreaState();
		trainInfoState.IsOpen = true;
		trainInfoState.IsVisible = true;

		// Act
		VerticalPageStateFactory.UpdateTrainInfoAreaOpenCloseState(trainInfoState, isToOpen: false);

		// Assert
		Assert.False(trainInfoState.IsOpen);
		Assert.True(trainInfoState.IsAnimationRunning);
	}

	[Fact]
	public void CompleteTrainInfoAreaAnimation_AfterOpenAnimation_SetsFinalOpenState()
	{
		// Arrange
		var trainInfoState = new TrainInfoAreaState();
		trainInfoState.FullHeight = 90;
		trainInfoState.IsAnimationRunning = true;

		// Act
		VerticalPageStateFactory.CompleteTrainInfoAreaAnimation(trainInfoState, wasOpenAnimation: true);

		// Assert
		Assert.False(trainInfoState.IsAnimationRunning);
		Assert.True(trainInfoState.IsVisible);
		Assert.Equal(90, trainInfoState.CurrentHeight);
	}

	[Fact]
	public void CompleteTrainInfoAreaAnimation_AfterCloseAnimation_SetsFinalClosedState()
	{
		// Arrange
		var trainInfoState = new TrainInfoAreaState();
		trainInfoState.FullHeight = 90;
		trainInfoState.IsAnimationRunning = true;

		// Act
		VerticalPageStateFactory.CompleteTrainInfoAreaAnimation(trainInfoState, wasOpenAnimation: false);

		// Assert
		Assert.False(trainInfoState.IsAnimationRunning);
		Assert.False(trainInfoState.IsVisible);
		Assert.Equal(0, trainInfoState.CurrentHeight);
	}

	[Fact]
	public void UpdateDebugMapState_WithEasterEggEnabledAndLandscape_ShowsMap()
	{
		// Arrange
		var debugMapState = new DebugMapState();

		// Act
		VerticalPageStateFactory.UpdateDebugMapState(debugMapState, isEasterEggEnabled: true, isLandscape: true);

		// Assert
		Assert.True(debugMapState.IsEnabled);
		Assert.True(debugMapState.IsLandscapeMode);
		Assert.True(debugMapState.IsVisible);
	}

	[Fact]
	public void UpdateDebugMapState_WithEasterEggDisabled_HidesMap()
	{
		// Arrange
		var debugMapState = new DebugMapState();

		// Act
		VerticalPageStateFactory.UpdateDebugMapState(debugMapState, isEasterEggEnabled: false, isLandscape: true);

		// Assert
		Assert.False(debugMapState.IsEnabled);
		Assert.False(debugMapState.IsVisible);
	}

	[Fact]
	public void UpdateDebugMapState_WithPortraitMode_HidesMap()
	{
		// Arrange
		var debugMapState = new DebugMapState();

		// Act
		VerticalPageStateFactory.UpdateDebugMapState(debugMapState, isEasterEggEnabled: true, isLandscape: false);

		// Assert
		Assert.True(debugMapState.IsEnabled);
		Assert.False(debugMapState.IsVisible);
	}

	[Fact]
	public void UpdateTimetableActivityIndicatorState_WithBusy_ShowsIndicator()
	{
		// Arrange
		var indicatorState = new TimetableActivityIndicatorState();

		// Act
		VerticalPageStateFactory.UpdateTimetableActivityIndicatorState(indicatorState, isTimetableBusy: true);

		// Assert
		Assert.True(indicatorState.IsBusy);
		Assert.True(indicatorState.IsVisible);
		Assert.True(indicatorState.Opacity > 0);
	}

	[Fact]
	public void UpdateTimetableActivityIndicatorState_NotBusy_HidesIndicator()
	{
		// Arrange
		var indicatorState = new TimetableActivityIndicatorState();
		indicatorState.IsBusy = true;
		indicatorState.IsVisible = true;

		// Act
		VerticalPageStateFactory.UpdateTimetableActivityIndicatorState(indicatorState, isTimetableBusy: false);

		// Assert
		Assert.False(indicatorState.IsBusy);
		Assert.False(indicatorState.IsVisible);
		Assert.Equal(0, indicatorState.Opacity);
	}

	[Fact]
	public void UpdatePageHeaderRunState_SetsRunState()
	{
		// Arrange
		var pageHeaderState = new PageHeaderState();

		// Act
		VerticalPageStateFactory.UpdatePageHeaderRunState(pageHeaderState, isRunning: true);

		// Assert
		Assert.True(pageHeaderState.IsRunning);
	}

	[Fact]
	public void UpdatePageHeaderRunState_WithFalse_ClearsRunState()
	{
		// Arrange
		var pageHeaderState = new PageHeaderState();
		pageHeaderState.IsRunning = true;

		// Act
		VerticalPageStateFactory.UpdatePageHeaderRunState(pageHeaderState, isRunning: false);

		// Assert
		Assert.False(pageHeaderState.IsRunning);
	}

	[Fact]
	public void UpdateGpsLocation_SetsCoordinatesAndAccuracy()
	{
		// Arrange
		var locationState = new LocationServiceState();

		// Act
		VerticalPageStateFactory.UpdateGpsLocation(locationState, latitude: 35.6762, longitude: 139.7674, accuracy: 10.5);

		// Assert
		Assert.Equal(35.6762, locationState.CurrentLatitude);
		Assert.Equal(139.7674, locationState.CurrentLongitude);
		Assert.Equal(10.5, locationState.CurrentAccuracy);
	}

	[Fact]
	public void UpdateGpsLocation_WithZeroAccuracy_SetsSuccessfully()
	{
		// Arrange
		var locationState = new LocationServiceState();

		// Act
		VerticalPageStateFactory.UpdateGpsLocation(locationState, latitude: 35.0, longitude: 139.0, accuracy: 0);

		// Assert
		Assert.Equal(35.0, locationState.CurrentLatitude);
		Assert.Equal(139.0, locationState.CurrentLongitude);
		Assert.Equal(0, locationState.CurrentAccuracy);
	}

	[Fact]
	public void ResetAllRowLocationStates_ResetsAllRowsToUndefined()
	{
		// Arrange
		var pageState = new VerticalPageState();
		pageState.RowStates[0] = new VerticalTimetableRowState { LocationState = 1 };
		pageState.RowStates[1] = new VerticalTimetableRowState { LocationState = 2 };
		pageState.RowStates[2] = new VerticalTimetableRowState { LocationState = 3 };

		// Act
		VerticalPageStateFactory.ResetAllRowLocationStates(pageState);

		// Assert
		foreach (var rowState in pageState.RowStates.Values)
		{
			Assert.Equal(0, rowState.LocationState); // 0 = Undefined
		}
	}

	[Fact]
	public void ResetAllRowLocationStates_WithEmptyRows_DoesNotThrow()
	{
		// Arrange
		var pageState = new VerticalPageState();

		// Act & Assert - Should not throw
		VerticalPageStateFactory.ResetAllRowLocationStates(pageState);
		Assert.Empty(pageState.RowStates);
	}

	[Fact]
	public void ShouldApplyTrainData_AllConditionsTrue_ReturnsTrue()
	{
		// Arrange
		var trainData = new TrainData(
			Id: "train-001",
			WorkName: "Test Work",
			AffectDate: new DateOnly(2024, 1, 15),
			TrainNumber: "101",
			MaxSpeed: "320 km/h",
			SpeedType: "Shinkansen",
			NominalTractiveCapacity: "16 cars",
			CarCount: 16,
			Destination: "Tokyo Station",
			BeginRemarks: null,
			AfterRemarks: null,
			Remarks: null,
			BeforeDeparture: "10 minutes",
			TrainInfo: "Shinkansen 101",
			Rows: null,
			Direction: 0,
			DayCount: 2
		);

		// Act
		var result = VerticalPageStateFactory.ShouldApplyTrainData(trainData, isViewHostVisible: true, isVerticalViewMode: true);

		// Assert
		Assert.True(result);
	}

	[Fact]
	public void ShouldApplyTrainData_TrainDataNull_ReturnsFalse()
	{
		// Act
		var result = VerticalPageStateFactory.ShouldApplyTrainData(null, isViewHostVisible: true, isVerticalViewMode: true);

		// Assert
		Assert.False(result);
	}

	[Fact]
	public void ShouldApplyTrainData_ViewNotVisible_ReturnsFalse()
	{
		// Arrange
		var trainData = new TrainData(
			Id: "train-001",
			WorkName: "Test Work",
			AffectDate: new DateOnly(2024, 1, 15),
			TrainNumber: "101",
			MaxSpeed: "320 km/h",
			SpeedType: "Shinkansen",
			NominalTractiveCapacity: "16 cars",
			CarCount: 16,
			Destination: "Tokyo Station",
			BeginRemarks: null,
			AfterRemarks: null,
			Remarks: null,
			BeforeDeparture: "10 minutes",
			TrainInfo: "Shinkansen 101",
			Rows: null,
			Direction: 0,
			DayCount: 2
		);

		// Act
		var result = VerticalPageStateFactory.ShouldApplyTrainData(trainData, isViewHostVisible: false, isVerticalViewMode: true);

		// Assert
		Assert.False(result);
	}

	[Fact]
	public void ShouldApplyTrainData_NotVerticalViewMode_ReturnsFalse()
	{
		// Arrange
		var trainData = new TrainData(
			Id: "train-001",
			WorkName: "Test Work",
			AffectDate: new DateOnly(2024, 1, 15),
			TrainNumber: "101",
			MaxSpeed: "320 km/h",
			SpeedType: "Shinkansen",
			NominalTractiveCapacity: "16 cars",
			CarCount: 16,
			Destination: "Tokyo Station",
			BeginRemarks: null,
			AfterRemarks: null,
			Remarks: null,
			BeforeDeparture: "10 minutes",
			TrainInfo: "Shinkansen 101",
			Rows: null,
			Direction: 0,
			DayCount: 2
		);

		// Act
		var result = VerticalPageStateFactory.ShouldApplyTrainData(trainData, isViewHostVisible: true, isVerticalViewMode: false);

		// Assert
		Assert.False(result);
	}

	[Fact]
	public void GetTrainDataInfo_WithValidTrainData_ReturnsAllProperties()
	{
		// Arrange
		var trainData = new TrainData(
			Id: "train-001",
			WorkName: "Test Work",
			AffectDate: new DateOnly(2024, 1, 15),
			TrainNumber: "101",
			MaxSpeed: "320 km/h",
			SpeedType: "Shinkansen",
			NominalTractiveCapacity: "16 cars",
			CarCount: 16,
			Destination: "Tokyo Station",
			BeginRemarks: null,
			AfterRemarks: null,
			Remarks: null,
			BeforeDeparture: "10 minutes",
			TrainInfo: "Shinkansen 101",
			Rows: null,
			Direction: 0,
			DayCount: 3
		);

		// Act
		var result = VerticalPageStateFactory.GetTrainDataInfo(trainData);

		// Assert
		// Result is a tuple, so we access by index instead of deconstruction
		Assert.Equal("Tokyo Station", result.Item1);
		Assert.Equal("Shinkansen 101", result.Item2);
		Assert.Equal("10 minutes", result.Item3);
		Assert.Equal(3, result.Item4);
	}

	[Fact]
	public void GetTrainDataInfo_WithNull_ReturnsDefaults()
	{
		// Act
		var result = VerticalPageStateFactory.GetTrainDataInfo(null);

		// Assert
		Assert.Null(result.Item1);
		Assert.Empty(result.Item2);
		Assert.Empty(result.Item3);
		Assert.Equal(0, result.Item4);
	}

	[Fact]
	public void GetTrainDataInfo_WithAllNullProperties_ReturnsEmptyValues()
	{
		// Arrange
		var trainData = new TrainData(
			Id: "train-001",
			WorkName: "Test Work",
			AffectDate: new DateOnly(2024, 1, 15),
			TrainNumber: "101",
			MaxSpeed: "320 km/h",
			SpeedType: "Shinkansen",
			NominalTractiveCapacity: "16 cars",
			CarCount: 16,
			Destination: null,
			BeginRemarks: null,
			AfterRemarks: null,
			Remarks: null,
			BeforeDeparture: null,
			TrainInfo: null,
			Rows: null,
			Direction: 0,
			DayCount: 0
		);

		// Act
		var result = VerticalPageStateFactory.GetTrainDataInfo(trainData);

		// Assert
		Assert.Null(result.Item1);
		Assert.Empty(result.Item2);
		Assert.Empty(result.Item3);
		Assert.Equal(0, result.Item4);
	}

	[Fact]
	public void GetTrainDataRows_WithValidTrainData_ReturnsRows()
	{
		// Arrange
		var rows = new TimetableRow[] {
			new TimetableRow(
				Id: "1",
				Location: new(1),
				DriveTimeMM: 10,
				DriveTimeSS: 30,
				StationName: "Station 1",
				IsOperationOnlyStop: false,
				IsPass: false,
				HasBracket: false,
				IsLastStop: false,
				ArriveTime: null,
				DepartureTime: null,
				TrackName: "1",
				RunInLimit: null,
				RunOutLimit: null,
				Remarks: null
			)
		};
		var trainData = new TrainData(
			Id: "train-001",
			WorkName: "Test Work",
			AffectDate: new DateOnly(2024, 1, 15),
			TrainNumber: "101",
			MaxSpeed: "320 km/h",
			SpeedType: "Shinkansen",
			NominalTractiveCapacity: "16 cars",
			CarCount: 16,
			Destination: "Tokyo Station",
			BeginRemarks: null,
			AfterRemarks: null,
			Remarks: null,
			BeforeDeparture: "10 minutes",
			TrainInfo: "Shinkansen 101",
			Rows: rows,
			Direction: 0,
			DayCount: 2
		);

		// Act
		var result = VerticalPageStateFactory.GetTrainDataRows(trainData);

		// Assert
		Assert.NotNull(result);
		Assert.Equal(rows, result);
	}

	[Fact]
	public void GetTrainDataRows_WithNull_ReturnsNull()
	{
		// Act
		var result = VerticalPageStateFactory.GetTrainDataRows(null);

		// Assert
		Assert.Null(result);
	}

	[Fact]
	public void GetTrainDataRows_WithNullRows_ReturnsNull()
	{
		// Arrange
		var trainData = new TrainData(
			Id: "train-001",
			WorkName: "Test Work",
			AffectDate: new DateOnly(2024, 1, 15),
			TrainNumber: "101",
			MaxSpeed: "320 km/h",
			SpeedType: "Shinkansen",
			NominalTractiveCapacity: "16 cars",
			CarCount: 16,
			Destination: "Tokyo Station",
			BeginRemarks: null,
			AfterRemarks: null,
			Remarks: null,
			BeforeDeparture: "10 minutes",
			TrainInfo: "Shinkansen 101",
			Rows: null,
			Direction: 0,
			DayCount: 2
		);

		// Act
		var result = VerticalPageStateFactory.GetTrainDataRows(trainData);

		// Assert
		Assert.Null(result);
	}

	[Fact]
	public void GpsLocation_MultipleUpdates_LatestValueIsPreserved()
	{
		// Arrange
		var locationState = new LocationServiceState();

		// Act
		VerticalPageStateFactory.UpdateGpsLocation(locationState, latitude: 35.0, longitude: 139.0, accuracy: 5);
		VerticalPageStateFactory.UpdateGpsLocation(locationState, latitude: 36.0, longitude: 140.0, accuracy: 10);

		// Assert - Latest values should be preserved
		Assert.Equal(36.0, locationState.CurrentLatitude);
		Assert.Equal(140.0, locationState.CurrentLongitude);
		Assert.Equal(10, locationState.CurrentAccuracy);
	}

	[Fact]
	public void UpdateLocationServiceEnabledState_EnablesService()
	{
		// Arrange
		var pageState = new VerticalPageState();

		// Act
		VerticalPageStateFactory.UpdateLocationServiceEnabledState(pageState, isEnabled: true);

		// Assert
		Assert.True(pageState.LocationServiceState.IsEnabled);
		Assert.True(pageState.PageHeaderState.IsLocationServiceEnabled);
		Assert.True(pageState.TimetableViewState.IsLocationServiceEnabled);
	}

	[Fact]
	public void UpdateLocationServiceEnabledState_DisablesService()
	{
		// Arrange
		var pageState = new VerticalPageState();
		VerticalPageStateFactory.UpdateLocationServiceEnabledState(pageState, isEnabled: true);

		// Act
		VerticalPageStateFactory.UpdateLocationServiceEnabledState(pageState, isEnabled: false);

		// Assert
		Assert.False(pageState.LocationServiceState.IsEnabled);
		Assert.False(pageState.PageHeaderState.IsLocationServiceEnabled);
		Assert.False(pageState.TimetableViewState.IsLocationServiceEnabled);
	}
}
