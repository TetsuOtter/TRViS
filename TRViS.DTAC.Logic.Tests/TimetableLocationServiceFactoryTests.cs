using Xunit;

namespace TRViS.DTAC.Logic.Tests;

public class TimetableLocationServiceFactoryTests
{
  [Fact]
  public void CreateEmptyState_ReturnsValidState()
  {
    // Act
    var state = TimetableLocationServiceFactory.CreateEmptyState();

    // Assert
    Assert.NotNull(state);
    Assert.False(state.IsLocationServiceEnabled);
    Assert.False(state.CanUseLocationService);
    Assert.Equal(0, state.TotalRows);
    Assert.Equal(-1, state.CurrentRunningRow.RowIndex);
    Assert.Equal(TimetableLocationServiceState.LocationStates.Undefined, state.CurrentRunningRow.LocationState);
    Assert.False(state.LocationMarker.BoxIsVisible);
    Assert.False(state.LocationMarker.LineIsVisible);
    Assert.True(state.IsHapticEnabled);
  }

  [Fact]
  public void InitializeTotalRows_SetsTotalRowsCorrectly()
  {
    // Arrange
    var state = TimetableLocationServiceFactory.CreateEmptyState();

    // Act
    TimetableLocationServiceFactory.InitializeTotalRows(state, 50);

    // Assert
    Assert.Equal(50, state.TotalRows);
  }

  [Fact]
  public void InitializeTotalRows_PreventNegativeRows()
  {
    // Arrange
    var state = TimetableLocationServiceFactory.CreateEmptyState();

    // Act
    TimetableLocationServiceFactory.InitializeTotalRows(state, -5);

    // Assert
    Assert.Equal(0, state.TotalRows);
  }

  [Fact]
  public void UpdateLocationServiceEnabled_EnablesService()
  {
    // Arrange
    var state = TimetableLocationServiceFactory.CreateEmptyState();

    // Act
    TimetableLocationServiceFactory.UpdateLocationServiceEnabled(state, true);

    // Assert
    Assert.True(state.IsLocationServiceEnabled);
  }

  [Fact]
  public void UpdateLocationServiceEnabled_DisablesServiceAndClearsMarker()
  {
    // Arrange
    var state = TimetableLocationServiceFactory.CreateEmptyState();
    state.IsLocationServiceEnabled = true;
    state.CurrentRunningRow.RowIndex = 5;
    state.LocationMarker.BoxIsVisible = true;

    // Act
    TimetableLocationServiceFactory.UpdateLocationServiceEnabled(state, false);

    // Assert
    Assert.False(state.IsLocationServiceEnabled);
    Assert.False(state.LocationMarker.BoxIsVisible);
    Assert.Equal(-1, state.CurrentRunningRow.RowIndex);
  }

  [Fact]
  public void UpdateLocationServiceCapability_UpdatesCapability()
  {
    // Arrange
    var state = TimetableLocationServiceFactory.CreateEmptyState();

    // Act
    TimetableLocationServiceFactory.UpdateLocationServiceCapability(state, true);

    // Assert
    Assert.True(state.CanUseLocationService);
  }

  [Fact]
  public void ProcessLocationStateChanged_SucceedsWithValidInput()
  {
    // Arrange
    var state = TimetableLocationServiceFactory.CreateEmptyState();
    TimetableLocationServiceFactory.InitializeTotalRows(state, 50);
    TimetableLocationServiceFactory.UpdateLocationServiceEnabled(state, true);

    // Act
    bool result = TimetableLocationServiceFactory.ProcessLocationStateChanged(
      state, 10, false, "Station 10");

    // Assert
    Assert.True(result);
    Assert.Equal(10, state.CurrentRunningRow.RowIndex);
    Assert.Equal("Station 10", state.CurrentRunningRow.StationName);
    Assert.Equal(TimetableLocationServiceState.LocationStates.AroundThisStation, state.CurrentRunningRow.LocationState);
    Assert.True(state.LocationMarker.BoxIsVisible);
    Assert.False(state.LocationMarker.LineIsVisible);
  }

  [Fact]
  public void ProcessLocationStateChanged_FailsWhenDisabled()
  {
    // Arrange
    var state = TimetableLocationServiceFactory.CreateEmptyState();
    TimetableLocationServiceFactory.InitializeTotalRows(state, 50);
    TimetableLocationServiceFactory.UpdateLocationServiceEnabled(state, false);

    // Act
    bool result = TimetableLocationServiceFactory.ProcessLocationStateChanged(state, 10, false, "Station");

    // Assert
    Assert.False(result);
  }

  [Fact]
  public void ProcessLocationStateChanged_FailsWithNegativeIndex()
  {
    // Arrange
    var state = TimetableLocationServiceFactory.CreateEmptyState();
    TimetableLocationServiceFactory.InitializeTotalRows(state, 50);
    TimetableLocationServiceFactory.UpdateLocationServiceEnabled(state, true);

    // Act
    bool result = TimetableLocationServiceFactory.ProcessLocationStateChanged(state, -1, false, "Station");

    // Assert
    Assert.False(result);
  }

  [Fact]
  public void ProcessLocationStateChanged_FailsWithIndexOutOfBounds()
  {
    // Arrange
    var state = TimetableLocationServiceFactory.CreateEmptyState();
    TimetableLocationServiceFactory.InitializeTotalRows(state, 50);
    TimetableLocationServiceFactory.UpdateLocationServiceEnabled(state, true);

    // Act
    bool result = TimetableLocationServiceFactory.ProcessLocationStateChanged(state, 50, false, "Station");

    // Assert
    Assert.False(result);
  }

  [Fact]
  public void ProcessLocationStateChanged_SetsRunningToNextStationState()
  {
    // Arrange
    var state = TimetableLocationServiceFactory.CreateEmptyState();
    TimetableLocationServiceFactory.InitializeTotalRows(state, 50);
    TimetableLocationServiceFactory.UpdateLocationServiceEnabled(state, true);

    // Act
    TimetableLocationServiceFactory.ProcessLocationStateChanged(state, 5, true, "Station 5");

    // Assert
    Assert.Equal(TimetableLocationServiceState.LocationStates.RunningToNextStation, state.CurrentRunningRow.LocationState);
    Assert.True(state.LocationMarker.BoxIsVisible);
    Assert.True(state.LocationMarker.LineIsVisible);
  }

  [Fact]
  public void ProcessLocationStateChanged_SetsMarkerMargin()
  {
    // Arrange
    var state = TimetableLocationServiceFactory.CreateEmptyState();
    TimetableLocationServiceFactory.InitializeTotalRows(state, 50);
    TimetableLocationServiceFactory.UpdateLocationServiceEnabled(state, true);
    TimetableLocationServiceFactory.SetRowHeight(state, 60);

    // Act - AroundThisStation
    TimetableLocationServiceFactory.ProcessLocationStateChanged(state, 5, false, "Station");
    var marginAround = state.LocationMarker.MarkerTopMargin;

    // Act - RunningToNextStation
    TimetableLocationServiceFactory.ProcessLocationStateChanged(state, 5, true, "Station");
    var marginRunning = state.LocationMarker.MarkerTopMargin;

    // Assert
    Assert.Equal(0, marginAround);
    Assert.Equal(-30, marginRunning);
  }

  [Fact]
  public void SetCurrentRunningRow_SetsRowCorrectly()
  {
    // Arrange
    var state = TimetableLocationServiceFactory.CreateEmptyState();
    TimetableLocationServiceFactory.InitializeTotalRows(state, 50);

    // Act
    TimetableLocationServiceFactory.SetCurrentRunningRow(state, 10, "Station 10", false);

    // Assert
    Assert.Equal(10, state.CurrentRunningRow.RowIndex);
    Assert.Equal("Station 10", state.CurrentRunningRow.StationName);
    Assert.Equal(TimetableLocationServiceState.LocationStates.AroundThisStation, state.CurrentRunningRow.LocationState);
  }

  [Fact]
  public void SetCurrentRunningRow_PreventRunningToNextStationOnLastRow()
  {
    // Arrange
    var state = TimetableLocationServiceFactory.CreateEmptyState();
    TimetableLocationServiceFactory.InitializeTotalRows(state, 50);

    // Act
    TimetableLocationServiceFactory.SetCurrentRunningRow(
      state, 49, "Last Station", true,
      TimetableLocationServiceState.LocationStates.RunningToNextStation);

    // Assert - Should not be set to RunningToNextStation on last row
    Assert.Equal(TimetableLocationServiceState.LocationStates.Undefined, state.CurrentRunningRow.LocationState);
  }

  [Fact]
  public void SetCurrentRunningRow_UnsetsRowWhenIndexIsNegative()
  {
    // Arrange
    var state = TimetableLocationServiceFactory.CreateEmptyState();
    TimetableLocationServiceFactory.InitializeTotalRows(state, 50);
    TimetableLocationServiceFactory.SetCurrentRunningRow(state, 10, "Station", false);

    // Act
    TimetableLocationServiceFactory.SetCurrentRunningRow(state, -1, "", false);

    // Assert
    Assert.Equal(-1, state.CurrentRunningRow.RowIndex);
    Assert.Equal(TimetableLocationServiceState.LocationStates.Undefined, state.CurrentRunningRow.LocationState);
    Assert.False(state.LocationMarker.BoxIsVisible);
  }

  [Fact]
  public void AdvanceLocationState_CyclesThroughStates()
  {
    // Arrange
    var state = TimetableLocationServiceFactory.CreateEmptyState();
    TimetableLocationServiceFactory.InitializeTotalRows(state, 50);
    TimetableLocationServiceFactory.SetCurrentRunningRow(
      state, 10, "Station", false,
      TimetableLocationServiceState.LocationStates.Undefined);

    // Act & Assert - Undefined -> AroundThisStation
    TimetableLocationServiceFactory.AdvanceLocationState(state, state.CurrentRunningRow);
    Assert.Equal(TimetableLocationServiceState.LocationStates.AroundThisStation, state.CurrentRunningRow.LocationState);

    // Act & Assert - AroundThisStation -> RunningToNextStation
    TimetableLocationServiceFactory.AdvanceLocationState(state, state.CurrentRunningRow);
    Assert.Equal(TimetableLocationServiceState.LocationStates.RunningToNextStation, state.CurrentRunningRow.LocationState);

    // Act & Assert - RunningToNextStation -> AroundThisStation
    TimetableLocationServiceFactory.AdvanceLocationState(state, state.CurrentRunningRow);
    Assert.Equal(TimetableLocationServiceState.LocationStates.AroundThisStation, state.CurrentRunningRow.LocationState);
  }

  [Fact]
  public void AdvanceLocationState_StopsAtAroundThisStationOnLastRow()
  {
    // Arrange
    var state = TimetableLocationServiceFactory.CreateEmptyState();
    TimetableLocationServiceFactory.InitializeTotalRows(state, 50);
    TimetableLocationServiceFactory.SetCurrentRunningRow(state, 49, "Last Station", true);

    // Act
    TimetableLocationServiceFactory.AdvanceLocationState(state, state.CurrentRunningRow);
    var stateAfterFirst = state.CurrentRunningRow.LocationState;

    TimetableLocationServiceFactory.AdvanceLocationState(state, state.CurrentRunningRow);
    var stateAfterSecond = state.CurrentRunningRow.LocationState;

    // Assert
    Assert.Equal(TimetableLocationServiceState.LocationStates.AroundThisStation, stateAfterFirst);
    Assert.Equal(TimetableLocationServiceState.LocationStates.AroundThisStation, stateAfterSecond); // Should not advance to RunningToNextStation
  }

  [Fact]
  public void RecordTapForDoubleTapDetection_RecordsFirstTap()
  {
    // Arrange
    var state = TimetableLocationServiceFactory.CreateEmptyState();
    var now = DateTime.Now;

    // Act
    bool isDoubleTap = TimetableLocationServiceFactory.RecordTapForDoubleTapDetection(state, 5, now);

    // Assert
    Assert.False(isDoubleTap);
    Assert.Equal(5, state.DoubleTapDetection.LastTappedRowIndex);
    Assert.Equal(now, state.DoubleTapDetection.LastTapTime);
  }

  [Fact]
  public void RecordTapForDoubleTapDetection_DetectsDoubleTap()
  {
    // Arrange
    var state = TimetableLocationServiceFactory.CreateEmptyState();
    var time1 = DateTime.Now;
    var time2 = time1.AddMilliseconds(100);

    // Act - First tap
    TimetableLocationServiceFactory.RecordTapForDoubleTapDetection(state, 5, time1);

    // Act - Second tap (within threshold)
    bool isDoubleTap = TimetableLocationServiceFactory.RecordTapForDoubleTapDetection(state, 5, time2);

    // Assert
    Assert.True(isDoubleTap);
  }

  [Fact]
  public void RecordTapForDoubleTapDetection_IgnoresSlowTaps()
  {
    // Arrange
    var state = TimetableLocationServiceFactory.CreateEmptyState();
    var time1 = DateTime.Now;
    var time2 = time1.AddMilliseconds(600); // Beyond threshold

    // Act - First tap
    TimetableLocationServiceFactory.RecordTapForDoubleTapDetection(state, 5, time1);

    // Act - Second tap (too slow)
    bool isDoubleTap = TimetableLocationServiceFactory.RecordTapForDoubleTapDetection(state, 5, time2);

    // Assert
    Assert.False(isDoubleTap);
    Assert.Equal(5, state.DoubleTapDetection.LastTappedRowIndex); // Updated to new tap
  }

  [Fact]
  public void RecordTapForDoubleTapDetection_IgnoresDifferentRows()
  {
    // Arrange
    var state = TimetableLocationServiceFactory.CreateEmptyState();
    var time1 = DateTime.Now;
    var time2 = time1.AddMilliseconds(100);

    // Act - First tap on row 5
    TimetableLocationServiceFactory.RecordTapForDoubleTapDetection(state, 5, time1);

    // Act - Second tap on row 6 (different row)
    bool isDoubleTap = TimetableLocationServiceFactory.RecordTapForDoubleTapDetection(state, 6, time2);

    // Assert
    Assert.False(isDoubleTap);
    Assert.Equal(6, state.DoubleTapDetection.LastTappedRowIndex); // Changed to new row
  }

  [Fact]
  public void ClearDoubleTapDetection_ClearsState()
  {
    // Arrange
    var state = TimetableLocationServiceFactory.CreateEmptyState();
    state.DoubleTapDetection.LastTappedRowIndex = 5;

    // Act
    TimetableLocationServiceFactory.ClearDoubleTapDetection(state);

    // Assert
    Assert.Equal(-1, state.DoubleTapDetection.LastTappedRowIndex);
    Assert.Equal(DateTime.MinValue, state.DoubleTapDetection.LastTapTime);
  }

  [Fact]
  public void SetHapticEnabled_UpdatesState()
  {
    // Arrange
    var state = TimetableLocationServiceFactory.CreateEmptyState();

    // Act
    TimetableLocationServiceFactory.SetHapticEnabled(state, false);

    // Assert
    Assert.False(state.IsHapticEnabled);
  }

  [Fact]
  public void SetRowHeight_UpdatesMarkerRowHeight()
  {
    // Arrange
    var state = TimetableLocationServiceFactory.CreateEmptyState();

    // Act
    TimetableLocationServiceFactory.SetRowHeight(state, 80);

    // Assert
    Assert.Equal(80, state.LocationMarker.RowHeight);
  }

  [Fact]
  public void SetRowHeight_IgnoresZeroHeight()
  {
    // Arrange
    var state = TimetableLocationServiceFactory.CreateEmptyState();
    var originalHeight = state.LocationMarker.RowHeight;

    // Act
    TimetableLocationServiceFactory.SetRowHeight(state, 0);

    // Assert
    Assert.Equal(originalHeight, state.LocationMarker.RowHeight);
  }

  [Fact]
  public void CompleteWorkflow_EnableLocationService()
  {
    // Arrange
    var state = TimetableLocationServiceFactory.CreateEmptyState();

    // Act
    TimetableLocationServiceFactory.InitializeTotalRows(state, 50);
    TimetableLocationServiceFactory.UpdateLocationServiceCapability(state, true);
    TimetableLocationServiceFactory.UpdateLocationServiceEnabled(state, true);
    var result = TimetableLocationServiceFactory.ProcessLocationStateChanged(state, 10, false, "Station 10");

    // Assert
    Assert.True(result);
    Assert.True(state.IsLocationServiceEnabled);
    Assert.True(state.CanUseLocationService);
    Assert.Equal(10, state.CurrentRunningRow.RowIndex);
    Assert.True(state.LocationMarker.BoxIsVisible);
  }

  [Fact]
  public void CompleteWorkflow_ManualRowSelection()
  {
    // Arrange
    var state = TimetableLocationServiceFactory.CreateEmptyState();
    TimetableLocationServiceFactory.InitializeTotalRows(state, 50);

    // Act - User taps row 5, which sets it to Undefined initially for manual selection
    TimetableLocationServiceFactory.SetCurrentRunningRow(
      state, 5, "Station 5", false,
      TimetableLocationServiceState.LocationStates.Undefined);

    // Assert initial state
    Assert.Equal(5, state.CurrentRunningRow.RowIndex);
    Assert.Equal(TimetableLocationServiceState.LocationStates.Undefined, state.CurrentRunningRow.LocationState);

    // Act - User taps to advance to AroundThisStation
    TimetableLocationServiceFactory.AdvanceLocationState(state, state.CurrentRunningRow);

    // Assert
    Assert.Equal(TimetableLocationServiceState.LocationStates.AroundThisStation, state.CurrentRunningRow.LocationState);

    // Act - User taps again to advance to RunningToNextStation
    TimetableLocationServiceFactory.AdvanceLocationState(state, state.CurrentRunningRow);

    // Assert
    Assert.Equal(TimetableLocationServiceState.LocationStates.RunningToNextStation, state.CurrentRunningRow.LocationState);
  }

  [Fact]
  public void CurrentRunningRowInfo_IsValidWhenIndexIsNonNegative()
  {
    // Arrange & Act
    var info1 = new TimetableLocationServiceState.CurrentRunningRowInfo { RowIndex = 0 };
    var info2 = new TimetableLocationServiceState.CurrentRunningRowInfo { RowIndex = 10 };
    var info3 = new TimetableLocationServiceState.CurrentRunningRowInfo { RowIndex = -1 };

    // Assert
    Assert.True(info1.IsValid);
    Assert.True(info2.IsValid);
    Assert.False(info3.IsValid);
  }
}
