namespace TRViS.DTAC.Logic.Tests;

public class TimetableDisplayLogicTests
{
	[Fact]
	public void ShouldShowNextDayIndicator_WithZeroDayCount_ReturnsFalse()
	{
		// Act
		var result = TimetableDisplayLogic.ShouldShowNextDayIndicator(0);

		// Assert
		Assert.False(result);
	}

	[Fact]
	public void ShouldShowNextDayIndicator_WithPositiveDayCount_ReturnsTrue()
	{
		// Act
		var result = TimetableDisplayLogic.ShouldShowNextDayIndicator(1);

		// Assert
		Assert.True(result);
	}

	[Fact]
	public void ShouldShowNextDayIndicator_WithNegativeDayCount_ReturnsFalse()
	{
		// Act
		var result = TimetableDisplayLogic.ShouldShowNextDayIndicator(-1);

		// Assert
		Assert.False(result);
	}

	[Fact]
	public void CalculateNonTimetableContentHeight_SumsAllHeights()
	{
		// Arrange
		double dateHeight = 60;
		double trainHeaderHeight = 54;
		double trainRowHeight = 54;
		double beforeDepartureHeight = 120;
		double carCountHeight = 60;
		double timetableHeaderHeight = 60;

		// Act
		var result = TimetableDisplayLogic.CalculateNonTimetableContentHeight(
			dateHeight,
			trainHeaderHeight,
			trainRowHeight,
			beforeDepartureHeight,
			carCountHeight,
			timetableHeaderHeight);

		// Assert
		Assert.Equal(408, result);
	}

	[Fact]
	public void CalculateScrollViewHeight_ReturnsMaxOfCurrentAndCalculated()
	{
		// Arrange
		double currentHeight = 500;
		double nonTimetableHeight = 400;
		double timetableHeight = 200;

		// Act
		var result = TimetableDisplayLogic.CalculateScrollViewHeight(
			currentHeight,
			nonTimetableHeight,
			timetableHeight);

		// Assert
		Assert.Equal(600, result); // nonTimetableHeight + timetableHeight
	}

	[Fact]
	public void CalculateScrollViewHeight_WithLargerCurrentHeight_ReturnsCurrentHeight()
	{
		// Arrange
		double currentHeight = 800;
		double nonTimetableHeight = 400;
		double timetableHeight = 200;

		// Act
		var result = TimetableDisplayLogic.CalculateScrollViewHeight(
			currentHeight,
			nonTimetableHeight,
			timetableHeight);

		// Assert
		Assert.Equal(800, result);
	}

	[Fact]
	public void CalculateScrollViewHeight_WithNegativeTimetableHeight_UsesZero()
	{
		// Arrange
		double currentHeight = 500;
		double nonTimetableHeight = 400;
		double timetableHeight = -100;

		// Act
		var result = TimetableDisplayLogic.CalculateScrollViewHeight(
			currentHeight,
			nonTimetableHeight,
			timetableHeight);

		// Assert
		Assert.Equal(500, result); // Max(500, 400 + 0)
	}
}
