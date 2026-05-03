using TRViS.DTAC.Logic.Abstractions;
using TRViS.DTAC.Logic.Formatters;

namespace TRViS.DTAC.Logic.Tests;

public class TimetableLayoutCalculatorTests
{
	private const double RowHeight = 65.0;

	#region CalculateRowDefinitionCount – phone idiom

	[Fact]
	public void RowDefinitionCount_Phone_ZeroRows_NoAfterArrive_NoNextTrain_Returns1()
	{
		// AfterRemarks row is always added → 0 + 1 = 1
		int result = TimetableLayoutCalculator.CalculateRowDefinitionCount(
			rowCount: 0, hasAfterRemarks: false, hasAfterArrive: false,
			hasNextTrainButton: false, isPhoneIdiom: true,
			scrollViewHeight: 0, rowHeight: RowHeight);

		Assert.Equal(1, result);
	}

	[Fact]
	public void RowDefinitionCount_Phone_3Rows_NoExtras_Returns4()
	{
		// 3 + 1 (AfterRemarks)
		int result = TimetableLayoutCalculator.CalculateRowDefinitionCount(
			rowCount: 3, hasAfterRemarks: false, hasAfterArrive: false,
			hasNextTrainButton: false, isPhoneIdiom: true,
			scrollViewHeight: 0, rowHeight: RowHeight);

		Assert.Equal(4, result);
	}

	[Fact]
	public void RowDefinitionCount_Phone_3Rows_WithAfterArrive_Returns5()
	{
		// 3 + 1 (AfterRemarks) + 1 (AfterArrive) = 5
		int result = TimetableLayoutCalculator.CalculateRowDefinitionCount(
			rowCount: 3, hasAfterRemarks: false, hasAfterArrive: true,
			hasNextTrainButton: false, isPhoneIdiom: true,
			scrollViewHeight: 0, rowHeight: RowHeight);

		Assert.Equal(5, result);
	}

	[Fact]
	public void RowDefinitionCount_Phone_3Rows_AllExtras_Returns6()
	{
		// 3 + 1 (AfterRemarks) + 1 (AfterArrive) + 1 (NextTrain) = 6
		int result = TimetableLayoutCalculator.CalculateRowDefinitionCount(
			rowCount: 3, hasAfterRemarks: true, hasAfterArrive: true,
			hasNextTrainButton: true, isPhoneIdiom: true,
			scrollViewHeight: 0, rowHeight: RowHeight);

		Assert.Equal(6, result);
	}

	[Fact]
	public void RowDefinitionCount_Phone_Negative_Throws()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			TimetableLayoutCalculator.CalculateRowDefinitionCount(
				rowCount: -1, hasAfterRemarks: false, hasAfterArrive: false,
				hasNextTrainButton: false, isPhoneIdiom: true,
				scrollViewHeight: 0, rowHeight: RowHeight));
	}

	#endregion

	#region CalculateRowDefinitionCount – tablet idiom

	[Fact]
	public void RowDefinitionCount_Tablet_SmallScrollView_ReturnsAtLeastMinCount()
	{
		// scrollViewHeight=650 → minCount = floor(650/65) = 10
		// additional = max(2, ceil(650/65) - 2) = max(2, 10-2) = 8
		// count = 3 + 8 = 11, max(10,11) = 11
		int result = TimetableLayoutCalculator.CalculateRowDefinitionCount(
			rowCount: 3, hasAfterRemarks: false, hasAfterArrive: false,
			hasNextTrainButton: false, isPhoneIdiom: false,
			scrollViewHeight: 650, rowHeight: RowHeight);

		Assert.Equal(11, result);
	}

	[Fact]
	public void RowDefinitionCount_Tablet_ZeroRows_MinCountDominates()
	{
		// scrollViewHeight=650 → minCount=10, additional=max(2,8)=8
		// count=0+8=8, max(10,8)=10
		int result = TimetableLayoutCalculator.CalculateRowDefinitionCount(
			rowCount: 0, hasAfterRemarks: false, hasAfterArrive: false,
			hasNextTrainButton: false, isPhoneIdiom: false,
			scrollViewHeight: 650, rowHeight: RowHeight);

		Assert.Equal(10, result);
	}

	[Fact]
	public void RowDefinitionCount_Tablet_ZeroScrollViewHeight_ZeroRows_Returns0()
	{
		// scrollViewHeight=0 → minCount=0, additional=max(2,ceil(0)-2)=max(2,-2)=2
		// count=0+2=2, max(0,2)=2
		int result = TimetableLayoutCalculator.CalculateRowDefinitionCount(
			rowCount: 0, hasAfterRemarks: false, hasAfterArrive: false,
			hasNextTrainButton: false, isPhoneIdiom: false,
			scrollViewHeight: 0, rowHeight: RowHeight);

		Assert.Equal(2, result);
	}

	#endregion

	#region CalculateGridHeightRequest

	[Fact]
	public void GridHeightRequest_10Rows_Returns650()
	{
		double result = TimetableLayoutCalculator.CalculateGridHeightRequest(10, RowHeight);
		Assert.Equal(650.0, result, precision: 3);
	}

	[Fact]
	public void GridHeightRequest_ZeroRows_Returns0()
	{
		double result = TimetableLayoutCalculator.CalculateGridHeightRequest(0, RowHeight);
		Assert.Equal(0.0, result, precision: 3);
	}

	#endregion

	#region CalculateAfterArriveRowIndex / CalculateNextTrainButtonRowIndex

	[Fact]
	public void AfterArriveRowIndex_5Rows_Returns6()
	{
		Assert.Equal(6, TimetableLayoutCalculator.CalculateAfterArriveRowIndex(5));
	}

	[Fact]
	public void NextTrainButtonRowIndex_5Rows_WithAfterArrive_Returns7()
	{
		Assert.Equal(7, TimetableLayoutCalculator.CalculateNextTrainButtonRowIndex(5, hasAfterArrive: true));
	}

	[Fact]
	public void NextTrainButtonRowIndex_5Rows_WithoutAfterArrive_Returns6()
	{
		Assert.Equal(6, TimetableLayoutCalculator.CalculateNextTrainButtonRowIndex(5, hasAfterArrive: false));
	}

	#endregion

	#region FindLastTimetableRowIndex

	[Fact]
	public void FindLastTimetableRowIndex_AllInfoRows_Returns0()
	{
		var list = new List<bool> { true, true, true };
		// No non-info row found → last stays 0
		Assert.Equal(0, TimetableLayoutCalculator.FindLastTimetableRowIndex(list));
	}

	[Fact]
	public void FindLastTimetableRowIndex_LastIsStation_ReturnsLastIndex()
	{
		var list = new List<bool> { true, false, false };
		Assert.Equal(2, TimetableLayoutCalculator.FindLastTimetableRowIndex(list));
	}

	[Fact]
	public void FindLastTimetableRowIndex_MixedRows_ReturnsLastStation()
	{
		// Rows: S=station, I=info: S,I,S,I,S,I → last station at index 4
		var list = new List<bool> { false, true, false, true, false, true };
		Assert.Equal(4, TimetableLayoutCalculator.FindLastTimetableRowIndex(list));
	}

	[Fact]
	public void FindLastTimetableRowIndex_EmptyList_Returns0()
	{
		Assert.Equal(0, TimetableLayoutCalculator.FindLastTimetableRowIndex(new List<bool>()));
	}

	[Fact]
	public void FindLastTimetableRowIndex_NullList_Throws()
	{
		Assert.Throws<ArgumentNullException>(() =>
			TimetableLayoutCalculator.FindLastTimetableRowIndex(null!));
	}

	#endregion

	#region CalculateScrollTargetY

	[Fact]
	public void ScrollTargetY_Position0_Returns0()
	{
		Assert.Equal(0.0, TimetableLayoutCalculator.CalculateScrollTargetY(0, RowHeight));
	}

	[Fact]
	public void ScrollTargetY_Position1_Returns0()
	{
		// (1-1)*65 = 0
		Assert.Equal(0.0, TimetableLayoutCalculator.CalculateScrollTargetY(1, RowHeight));
	}

	[Fact]
	public void ScrollTargetY_Position3_Returns130()
	{
		// (3-1)*65 = 130
		Assert.Equal(130.0, TimetableLayoutCalculator.CalculateScrollTargetY(3, RowHeight));
	}

	[Fact]
	public void ScrollTargetY_NegativePosition_Returns0()
	{
		Assert.Equal(0.0, TimetableLayoutCalculator.CalculateScrollTargetY(-1, RowHeight));
	}

	#endregion

	#region CalculateLocationMarkerDisplay

	[Fact]
	public void LocationMarkerDisplay_Undefined_BothHidden()
	{
		var result = TimetableLayoutCalculator.CalculateLocationMarkerDisplay(TimetableLocationState.Undefined, RowHeight);
		Assert.False(result.IsBoxVisible);
		Assert.False(result.IsLineVisible);
		Assert.Equal(0, result.BoxMarginTop);
	}

	[Fact]
	public void LocationMarkerDisplay_AroundThisStation_BoxVisibleLineHidden()
	{
		var result = TimetableLayoutCalculator.CalculateLocationMarkerDisplay(TimetableLocationState.AroundThisStation, RowHeight);
		Assert.True(result.IsBoxVisible);
		Assert.False(result.IsLineVisible);
		Assert.Equal(0, result.BoxMarginTop);
	}

	[Fact]
	public void LocationMarkerDisplay_RunningToNextStation_BothVisibleNegativeMargin()
	{
		var result = TimetableLayoutCalculator.CalculateLocationMarkerDisplay(TimetableLocationState.RunningToNextStation, RowHeight);
		Assert.True(result.IsBoxVisible);
		Assert.True(result.IsLineVisible);
		Assert.Equal(-(RowHeight / 2), result.BoxMarginTop);
	}

	[Fact]
	public void LocationMarkerDisplay_InvalidRowHeight_Throws()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			TimetableLayoutCalculator.CalculateLocationMarkerDisplay(TimetableLocationState.Undefined, rowHeight: 0));
	}

	#endregion
}
