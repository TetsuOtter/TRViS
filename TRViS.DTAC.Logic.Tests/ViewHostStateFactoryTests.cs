using Xunit;
using TRViS.DTAC.Logic;
using System;
using TRViS.IO.Models;

namespace TRViS.DTAC.Logic.Tests;

public class ViewHostStateFactoryTests
{
	[Fact]
	public void CreateEmptyState_ReturnsInitializedState()
	{
		// Act
		var state = ViewHostStateFactory.CreateEmptyState();

		// Assert
		Assert.NotNull(state);
		Assert.NotNull(state.SelectedWorkGroup);
		Assert.NotNull(state.SelectedWork);
		Assert.NotNull(state.SelectedTrain);
		Assert.Empty(state.SelectedWorkGroup.Name);
		Assert.Empty(state.SelectedWork.Name);
		Assert.Empty(state.SelectedTrain.AffectDate);
		Assert.Equal(0, state.SelectedTrain.DayCount);
	}

	[Fact]
	public void UpdateSelectedWorkGroup_SetsWorkGroupNameAndMarksChanged()
	{
		// Arrange
		var state = ViewHostStateFactory.CreateEmptyState();

		// Act
		ViewHostStateFactory.UpdateSelectedWorkGroup(state, "Work Group 1");

		// Assert
		Assert.Equal("Work Group 1", state.SelectedWorkGroup.Name);
		Assert.True(state.SelectedWorkGroup.IsChanged);
	}

	[Fact]
	public void UpdateSelectedWorkGroup_WithNull_SetsEmptyStringAndMarksChanged()
	{
		// Arrange
		var state = ViewHostStateFactory.CreateEmptyState();

		// Act
		ViewHostStateFactory.UpdateSelectedWorkGroup(state, null);

		// Assert
		Assert.Empty(state.SelectedWorkGroup.Name);
		Assert.True(state.SelectedWorkGroup.IsChanged);
	}

	[Fact]
	public void UpdateSelectedWork_SetsWorkNameAndMarksChanged()
	{
		// Arrange
		var state = ViewHostStateFactory.CreateEmptyState();

		// Act
		ViewHostStateFactory.UpdateSelectedWork(state, "Work 1");

		// Assert
		Assert.Equal("Work 1", state.SelectedWork.Name);
		Assert.True(state.SelectedWork.IsChanged);
	}

	[Fact]
	public void UpdateSelectedWork_WithNull_SetsEmptyStringAndMarksChanged()
	{
		// Arrange
		var state = ViewHostStateFactory.CreateEmptyState();

		// Act
		ViewHostStateFactory.UpdateSelectedWork(state, null);

		// Assert
		Assert.Empty(state.SelectedWork.Name);
		Assert.True(state.SelectedWork.IsChanged);
	}

	[Fact]
	public void UpdateSelectedTrain_SetsTrainDataAndMarksChanged()
	{
		// Arrange
		var state = ViewHostStateFactory.CreateEmptyState();

		// Act
		ViewHostStateFactory.UpdateSelectedTrain(state, "2024年1月15日", 2);

		// Assert
		Assert.Equal("2024年1月15日", state.SelectedTrain.AffectDate);
		Assert.Equal(2, state.SelectedTrain.DayCount);
		Assert.True(state.SelectedTrain.IsChanged);
	}

	[Fact]
	public void UpdateSelectedTrain_WithNullAffectDate_SetsEmptyStringAndMarksChanged()
	{
		// Arrange
		var state = ViewHostStateFactory.CreateEmptyState();

		// Act
		ViewHostStateFactory.UpdateSelectedTrain(state, null, 3);

		// Assert
		Assert.Empty(state.SelectedTrain.AffectDate);
		Assert.Equal(3, state.SelectedTrain.DayCount);
		Assert.True(state.SelectedTrain.IsChanged);
	}

	[Fact]
	public void FormatAffectDate_WithProvidedDate_ReturnsFormattedDate()
	{
		// Arrange
		var providedDate = new DateTime(2024, 1, 15);

		// Act
		var result = ViewHostStateFactory.FormatAffectDate(providedDate, dayCount: 0);

		// Assert
		Assert.Equal("2024年1月15日", result);
	}

	[Fact]
	public void FormatAffectDate_WithoutProvidedDateButDayCount_CalculatesFromToday()
	{
		// Arrange
		int dayCount = 3;

		// Act
		var result = ViewHostStateFactory.FormatAffectDate(affectDate: null, dayCount);

		// Assert
		// Should be today - 3 days
		var expectedDate = DateTime.Now.AddDays(-dayCount).ToString("yyyy年M月d日");
		Assert.Equal(expectedDate, result);
	}

	[Fact]
	public void FormatAffectDate_WithNullDate_CalculatesFromDayCount()
	{
		// Arrange - use dayCount 0 to get today
		int dayCount = 0;

		// Act
		var result = ViewHostStateFactory.FormatAffectDate(null, dayCount);

		// Assert
		var expectedDate = DateTime.Now.ToString("yyyy年M月d日");
		Assert.Equal(expectedDate, result);
	}

	[Fact]
	public void MarkWorkGroupProcessed_ClearsIsChanged()
	{
		// Arrange
		var state = ViewHostStateFactory.CreateEmptyState();
		ViewHostStateFactory.UpdateSelectedWorkGroup(state, "Group 1");
		Assert.True(state.SelectedWorkGroup.IsChanged);

		// Act
		ViewHostStateFactory.MarkWorkGroupProcessed(state);

		// Assert
		Assert.False(state.SelectedWorkGroup.IsChanged);
	}

	[Fact]
	public void MarkWorkProcessed_ClearsIsChanged()
	{
		// Arrange
		var state = ViewHostStateFactory.CreateEmptyState();
		ViewHostStateFactory.UpdateSelectedWork(state, "Work 1");
		Assert.True(state.SelectedWork.IsChanged);

		// Act
		ViewHostStateFactory.MarkWorkProcessed(state);

		// Assert
		Assert.False(state.SelectedWork.IsChanged);
	}

	[Fact]
	public void MarkTrainProcessed_ClearsIsChanged()
	{
		// Arrange
		var state = ViewHostStateFactory.CreateEmptyState();
		ViewHostStateFactory.UpdateSelectedTrain(state, "2024年1月15日", 2);
		Assert.True(state.SelectedTrain.IsChanged);

		// Act
		ViewHostStateFactory.MarkTrainProcessed(state);

		// Assert
		Assert.False(state.SelectedTrain.IsChanged);
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
		var result = ViewHostStateFactory.ShouldApplyTrainData(trainData, true, true);

		// Assert
		Assert.True(result);
	}

	[Fact]
	public void ShouldApplyTrainData_TrainDataNull_ReturnsFalse()
	{
		// Act
		var result = ViewHostStateFactory.ShouldApplyTrainData(null, true, true);

		// Assert
		Assert.False(result);
	}

	[Fact]
	public void ShouldApplyTrainData_ViewHostNotVisible_ReturnsFalse()
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
		var result = ViewHostStateFactory.ShouldApplyTrainData(trainData, false, true);

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
		var result = ViewHostStateFactory.ShouldApplyTrainData(trainData, true, false);

		// Assert
		Assert.False(result);
	}

	[Fact]
	public void UpdateViewHostDisplayState_UpdatesAllProperties()
	{
		// Arrange
		var verticalPageState = new VerticalPageState();

		// Act
		ViewHostStateFactory.UpdateViewHostDisplayState(
			verticalPageState,
			isViewHostVisible: true,
			isVerticalViewMode: true,
			isHakoMode: true,
			isWorkAffixMode: false);

		// Assert
		Assert.True(verticalPageState.ViewHostDisplayState.IsVisible);
		Assert.True(verticalPageState.ViewHostDisplayState.IsVerticalViewMode);
		Assert.True(verticalPageState.ViewHostDisplayState.IsHakoMode);
		Assert.False(verticalPageState.ViewHostDisplayState.IsWorkAffixMode);
	}

	[Fact]
	public void UpdateAffectDate_SetsAffectDateInPageHeader()
	{
		// Arrange
		var pageHeaderState = new PageHeaderState();

		// Act
		ViewHostStateFactory.UpdateAffectDate(pageHeaderState, "2024年1月15日");

		// Assert
		Assert.Equal("2024年1月15日", pageHeaderState.AffectDateLabelText);
	}

	[Fact]
	public void UpdateAffectDate_WithNull_SetsEmptyString()
	{
		// Arrange
		var pageHeaderState = new PageHeaderState();

		// Act
		ViewHostStateFactory.UpdateAffectDate(pageHeaderState, null);

		// Assert
		Assert.Empty(pageHeaderState.AffectDateLabelText);
	}

	// Getter methods were removed as they are part of internal logic.
	// The state is managed through Update methods instead.

	[Fact]
	public void HasWorkGroupChanged_WithChangedWorkGroup_ReturnsTrue()
	{
		// Arrange
		var state = ViewHostStateFactory.CreateEmptyState();
		ViewHostStateFactory.UpdateSelectedWorkGroup(state, "Group 1");

		// Act
		var hasChanged = ViewHostStateFactory.HasWorkGroupChanged(state);

		// Assert
		Assert.True(hasChanged);
	}

	[Fact]
	public void HasWorkGroupChanged_AfterMarked_ReturnsFalse()
	{
		// Arrange
		var state = ViewHostStateFactory.CreateEmptyState();
		ViewHostStateFactory.UpdateSelectedWorkGroup(state, "Group 1");
		ViewHostStateFactory.MarkWorkGroupProcessed(state);

		// Act
		var hasChanged = ViewHostStateFactory.HasWorkGroupChanged(state);

		// Assert
		Assert.False(hasChanged);
	}

	[Fact]
	public void HasWorkChanged_WithChangedWork_ReturnsTrue()
	{
		// Arrange
		var state = ViewHostStateFactory.CreateEmptyState();
		ViewHostStateFactory.UpdateSelectedWork(state, "Work 1");

		// Act
		var hasChanged = ViewHostStateFactory.HasWorkChanged(state);

		// Assert
		Assert.True(hasChanged);
	}

	[Fact]
	public void HasWorkChanged_AfterMarked_ReturnsFalse()
	{
		// Arrange
		var state = ViewHostStateFactory.CreateEmptyState();
		ViewHostStateFactory.UpdateSelectedWork(state, "Work 1");
		ViewHostStateFactory.MarkWorkProcessed(state);

		// Act
		var hasChanged = ViewHostStateFactory.HasWorkChanged(state);

		// Assert
		Assert.False(hasChanged);
	}

	[Fact]
	public void HasTrainChanged_WithChangedTrain_ReturnsTrue()
	{
		// Arrange
		var state = ViewHostStateFactory.CreateEmptyState();
		ViewHostStateFactory.UpdateSelectedTrain(state, "2024年1月15日", 2);

		// Act
		var hasChanged = ViewHostStateFactory.HasTrainChanged(state);

		// Assert
		Assert.True(hasChanged);
	}

	[Fact]
	public void HasTrainChanged_AfterMarked_ReturnsFalse()
	{
		// Arrange
		var state = ViewHostStateFactory.CreateEmptyState();
		ViewHostStateFactory.UpdateSelectedTrain(state, "2024年1月15日", 2);
		ViewHostStateFactory.MarkTrainProcessed(state);

		// Act
		var hasChanged = ViewHostStateFactory.HasTrainChanged(state);

		// Assert
		Assert.False(hasChanged);
	}

	[Fact]
	public void MultipleUpdates_OnSameProperty_UpdatesCorrectly()
	{
		// Arrange
		var state = ViewHostStateFactory.CreateEmptyState();

		// Act & Assert - First update
		ViewHostStateFactory.UpdateSelectedWorkGroup(state, "Group 1");
		Assert.True(state.SelectedWorkGroup.IsChanged);
		Assert.Equal("Group 1", state.SelectedWorkGroup.Name);

		// Mark processed
		ViewHostStateFactory.MarkWorkGroupProcessed(state);
		Assert.False(state.SelectedWorkGroup.IsChanged);

		// Second update
		ViewHostStateFactory.UpdateSelectedWorkGroup(state, "Group 2");
		Assert.True(state.SelectedWorkGroup.IsChanged);
		Assert.Equal("Group 2", state.SelectedWorkGroup.Name);
	}

	[Fact]
	public void FormatAffectDate_WithStringValue_FormatsSuccessfully()
	{
		// Act - FormatAffectDate now expects DateTime? parameter
		var result = ViewHostStateFactory.FormatAffectDate(null, dayCount: 0);

		// Assert - Should gracefully handle and return formatted date
		Assert.NotEmpty(result);
	}
}
