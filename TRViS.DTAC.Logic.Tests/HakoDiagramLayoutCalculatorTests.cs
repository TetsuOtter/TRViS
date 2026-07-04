using TRViS.DTAC.Logic.Layout;
using TRViS.IO.Models;

namespace TRViS.DTAC.Logic.Tests;

public class HakoDiagramLayoutCalculatorTests
{
	static TimetableRow Row(string stationName, double locationM, bool isInfoRow = false)
		=> new(
			Id: Guid.NewGuid().ToString(),
			Location: new LocationInfo(locationM),
			DriveTimeMM: null,
			DriveTimeSS: null,
			StationName: stationName,
			IsOperationOnlyStop: false,
			IsPass: false,
			HasBracket: false,
			IsLastStop: false,
			ArriveTime: null,
			DepartureTime: null,
			TrackName: null,
			RunInLimit: null,
			RunOutLimit: null,
			Remarks: null,
			IsInfoRow: isInfoRow);

	static TrainData Train(string id, string trainNumber, Direction direction, params TimetableRow[] rows)
		=> new(
			Id: id,
			Direction: direction,
			TrainNumber: trainNumber,
			Rows: rows);

	// ---------- CalculateGridLineCount ----------

	[Theory]
	// usable = width - 2*81 = width - 162; lines = floor(usable / 38) + 1 (0 when usable < 0).
	[InlineData(314, 5)]   // usable 152 -> floor(4.0) + 1 = 5  (minimum diagram width)
	[InlineData(313, 4)]   // usable 151 -> floor(3.97) + 1 = 4 (just below the minimum)
	[InlineData(600, 12)]  // usable 438 -> floor(11.5) + 1 = 12
	[InlineData(162, 1)]   // usable 0   -> floor(0) + 1 = 1
	[InlineData(161, 0)]   // usable -1  -> 0
	[InlineData(0, 0)]
	[InlineData(1200, 28)] // usable 1038 -> floor(27.3) + 1 = 28
	public void CalculateGridLineCount_ReturnsExpected(double width, int expected)
	{
		Assert.Equal(expected, HakoDiagramLayoutCalculator.CalculateGridLineCount(width));
	}

	[Fact]
	public void CalculateGridLineCount_CustomSpacingAndMargin_IsHonored()
	{
		// No margins, 10px spacing: 200px hosts floor(200/10) + 1 = 21 lines.
		Assert.Equal(21, HakoDiagramLayoutCalculator.CalculateGridLineCount(200, lineSpacing: 10, edgeMargin: 0));
		// 90px margins each side leave 20px usable -> floor(20/10) + 1 = 3 lines.
		Assert.Equal(3, HakoDiagramLayoutCalculator.CalculateGridLineCount(200, lineSpacing: 10, edgeMargin: 90));
	}

	[Fact]
	public void CalculateGridLineCount_NonPositiveLineSpacing_Throws()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => HakoDiagramLayoutCalculator.CalculateGridLineCount(600, lineSpacing: 0));
		Assert.Throws<ArgumentOutOfRangeException>(() => HakoDiagramLayoutCalculator.CalculateGridLineCount(600, lineSpacing: -1));
	}

	// ---------- MaxTurnBackStationCount ----------

	[Theory]
	[InlineData(4, 0)]   // below the 5-line minimum -> diagram never offered
	[InlineData(5, 2)]   // 2 edges + 2 stations + 1 blank
	[InlineData(6, 2)]
	[InlineData(7, 3)]
	[InlineData(12, 5)]
	[InlineData(15, 7)]
	[InlineData(28, 13)] // wide screens hold far more than the old fixed max of 7
	public void MaxTurnBackStationCount_ReturnsExpected(int gridLineCount, int expected)
	{
		Assert.Equal(expected, HakoDiagramLayoutCalculator.MaxTurnBackStationCount(gridLineCount));
	}

	// ---------- MinGridLineCountFor ----------

	[Theory]
	[InlineData(1, 5)]   // clamped up to the 5-line minimum
	[InlineData(2, 5)]
	[InlineData(3, 7)]
	[InlineData(7, 15)]
	public void MinGridLineCountFor_ReturnsExpected(int stationCount, int expected)
	{
		Assert.Equal(expected, HakoDiagramLayoutCalculator.MinGridLineCountFor(stationCount));
	}

	[Fact]
	public void MinGridLineCountFor_NonPositive_Throws()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => HakoDiagramLayoutCalculator.MinGridLineCountFor(0));
		Assert.Throws<ArgumentOutOfRangeException>(() => HakoDiagramLayoutCalculator.MinGridLineCountFor(-1));
	}

	[Fact]
	public void MaxTurnBackStationCount_And_MinGridLineCountFor_AreConsistent()
	{
		// The largest station count a grid can hold is exactly the count whose minimum grid still fits.
		for (int gridLineCount = HakoDiagramLayoutCalculator.MinGridLineCount; gridLineCount <= 40; gridLineCount++)
		{
			int max = HakoDiagramLayoutCalculator.MaxTurnBackStationCount(gridLineCount);
			Assert.True(HakoDiagramLayoutCalculator.MinGridLineCountFor(max) <= gridLineCount);
			Assert.True(HakoDiagramLayoutCalculator.MinGridLineCountFor(max + 1) > gridLineCount);
		}
	}

	// ---------- ShouldUseDiagramLayout ----------

	[Theory]
	[InlineData(314, 2, true)]     // exactly 5 lines -> holds the minimum 2 stations
	[InlineData(314, 3, false)]    // 5 lines can't hold 3 stations -> Simple
	[InlineData(313, 2, false)]    // 4 lines -> below the 5-line minimum -> Simple
	[InlineData(313, 1, false)]    // even a single station needs the 5-line minimum
	[InlineData(600, 5, true)]     // 12 lines -> up to 5 stations
	[InlineData(600, 6, false)]
	[InlineData(600, 1, true)]
	[InlineData(600, 0, false)]    // no turn-back stations -> Simple
	[InlineData(1200, 13, true)]   // wide screen -> more than the old max of 7 stations
	[InlineData(1200, 14, false)]
	[InlineData(161, 1, false)]    // no lines fit at all
	public void ShouldUseDiagramLayout_ReturnsExpected(double width, int turnBackStationCount, bool expected)
	{
		bool result = HakoDiagramLayoutCalculator.ShouldUseDiagramLayout(width, turnBackStationCount);
		Assert.Equal(expected, result);
	}

	[Fact]
	public void ShouldUseDiagramLayout_CustomSpacingAndMargin_IsHonored()
	{
		// 200px with tight 10px spacing and no margins fits 21 lines -> up to 10 stations.
		Assert.True(HakoDiagramLayoutCalculator.ShouldUseDiagramLayout(200, 3, lineSpacing: 10, edgeMargin: 0));
		// Same width but 90px margins collapses it below the minimum.
		Assert.False(HakoDiagramLayoutCalculator.ShouldUseDiagramLayout(200, 3, lineSpacing: 10, edgeMargin: 90));
	}

	// ---------- CalculateLineIndex ----------

	[Fact]
	public void CalculateLineIndex_SingleStation_ReturnsFirstUsableLine()
	{
		double result = HakoDiagramLayoutCalculator.CalculateLineIndex(0, 1, 5);
		Assert.Equal(1, result);
	}

	[Fact]
	public void CalculateLineIndex_TwoStations_MinimumGrid_OneBlankLineBetween()
	{
		// The minimum 5-line grid: edge, station, blank, station, edge -> lines 1 and 3.
		Assert.Equal(1, HakoDiagramLayoutCalculator.CalculateLineIndex(0, 2, 5));
		Assert.Equal(3, HakoDiagramLayoutCalculator.CalculateLineIndex(1, 2, 5));
	}

	[Fact]
	public void CalculateLineIndex_TwoStations_WideGrid_SpanFullUsableRange()
	{
		// Fewer stations spread out more: on a 16-line grid 2 stations use the largest whole-number
		// step (13), landing on the very first and very last usable lines.
		Assert.Equal(1, HakoDiagramLayoutCalculator.CalculateLineIndex(0, 2, 16));
		Assert.Equal(14, HakoDiagramLayoutCalculator.CalculateLineIndex(1, 2, 16));
	}

	[Fact]
	public void CalculateLineIndex_FiveStations_WideGrid_ThreeBlankLinesBetweenEach()
	{
		double[] expected = [1, 4, 7, 10, 13];
		for (int i = 0; i < expected.Length; i++)
		{
			Assert.Equal(expected[i], HakoDiagramLayoutCalculator.CalculateLineIndex(i, 5, 16));
		}
	}

	[Fact]
	public void CalculateLineIndex_SevenStations_TightestGrid_OneBlankLineBetweenEach()
	{
		// 7 stations exactly fill a 15-line grid: lines 1,3,5,7,9,11,13 (a single blank between each).
		double[] expected = [1, 3, 5, 7, 9, 11, 13];
		for (int i = 0; i < expected.Length; i++)
		{
			Assert.Equal(expected[i], HakoDiagramLayoutCalculator.CalculateLineIndex(i, 7, 15));
		}
	}

	[Fact]
	public void CalculateLineIndex_GapBetweenConsecutiveStationsIsConstantPerStationCount()
	{
		const int gridLineCount = 16;
		for (int stationCount = 2; stationCount <= HakoDiagramLayoutCalculator.MaxTurnBackStationCount(gridLineCount); stationCount++)
		{
			int expectedStep = 13 / (stationCount - 1); // usable span on a 16-line grid is 14 - 1 = 13
			for (int i = 1; i < stationCount; i++)
			{
				double gap = HakoDiagramLayoutCalculator.CalculateLineIndex(i, stationCount, gridLineCount)
					- HakoDiagramLayoutCalculator.CalculateLineIndex(i - 1, stationCount, gridLineCount);
				Assert.Equal(expectedStep, gap);
			}
		}
	}

	[Fact]
	public void CalculateLineIndex_OutOfRangeOrdinal_Throws()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => HakoDiagramLayoutCalculator.CalculateLineIndex(-1, 3, 7));
		Assert.Throws<ArgumentOutOfRangeException>(() => HakoDiagramLayoutCalculator.CalculateLineIndex(3, 3, 7));
	}

	[Fact]
	public void CalculateLineIndex_ZeroStationCount_Throws()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => HakoDiagramLayoutCalculator.CalculateLineIndex(0, 0, 5));
	}

	[Fact]
	public void CalculateLineIndex_GridTooSmallForStationCount_Throws()
	{
		// 3 stations need at least 7 lines; a 6-line grid can't place them.
		Assert.Throws<ArgumentOutOfRangeException>(() => HakoDiagramLayoutCalculator.CalculateLineIndex(0, 3, 6));
	}

	// ---------- BuildStationColumns ----------

	[Fact]
	public void BuildStationColumns_NullOrEmpty_ReturnsEmpty()
	{
		Assert.Empty(HakoDiagramLayoutCalculator.BuildStationColumns(null, 16));
		Assert.Empty(HakoDiagramLayoutCalculator.BuildStationColumns([], 16));
	}

	[Fact]
	public void BuildStationColumns_DedupesByStationNameAndOrdersByLocation()
	{
		TrainData trainA = Train("a", "1A", Direction.Outbound,
			Row("新宿", 0), Row("東京", 100));
		TrainData trainB = Train("b", "1B", Direction.Inbound,
			Row("東京", 100), Row("新宿", 0));

		var columns = HakoDiagramLayoutCalculator.BuildStationColumns([trainA, trainB], 16);

		Assert.Equal(2, columns.Count);
		Assert.Equal("新宿", columns[0].StationName);
		Assert.Equal(0, columns[0].Location_m);
		Assert.Equal("東京", columns[1].StationName);
		Assert.Equal(100, columns[1].Location_m);
	}

	[Fact]
	public void BuildStationColumns_IgnoresInfoRows()
	{
		TrainData train = Train("a", "1A", Direction.Outbound,
			Row("Info", 0, isInfoRow: true),
			Row("新宿", 10),
			Row("東京", 100),
			Row("Info2", 200, isInfoRow: true));

		var columns = HakoDiagramLayoutCalculator.BuildStationColumns([train], 16);

		Assert.Equal(["新宿", "東京"], columns.Select(c => c.StationName));
	}

	[Fact]
	public void BuildStationColumns_WideGrid_AssignsEquallySpacedLineIndexes()
	{
		TrainData trainA = Train("a", "1A", Direction.Outbound, Row("A", 0), Row("B", 10));
		TrainData trainB = Train("b", "1B", Direction.Outbound, Row("B", 10), Row("C", 20));

		var columns = HakoDiagramLayoutCalculator.BuildStationColumns([trainA, trainB], 16);

		Assert.Equal(3, columns.Count);
		Assert.Equal(1, columns[0].LineIndex);
		Assert.Equal(7, columns[1].LineIndex);
		Assert.Equal(13, columns[2].LineIndex);
	}

	[Fact]
	public void BuildStationColumns_GridTooNarrow_StillReturnsAllStationsAtTightPositions()
	{
		// A 5-line grid can't legibly hold 3 stations (the caller falls back to Simple), but the
		// column list must still be complete and its positions must not underflow — they clamp up to
		// the tightest grid that fits 3 stations (7 lines -> 1,3,5).
		TrainData trainA = Train("a", "1A", Direction.Outbound, Row("A", 0), Row("B", 10));
		TrainData trainB = Train("b", "1B", Direction.Outbound, Row("B", 10), Row("C", 20));

		var columns = HakoDiagramLayoutCalculator.BuildStationColumns([trainA, trainB], 5);

		Assert.Equal(3, columns.Count);
		Assert.Equal([1d, 3d, 5d], columns.Select(c => c.LineIndex));
	}

	// ---------- BuildTrainSegments ----------

	[Fact]
	public void BuildTrainSegments_NullOrEmptyInputs_ReturnsEmpty()
	{
		var columns = HakoDiagramLayoutCalculator.BuildStationColumns([Train("a", "1A", Direction.Outbound, Row("A", 0), Row("B", 10))], 16);

		Assert.Empty(HakoDiagramLayoutCalculator.BuildTrainSegments(null, columns));
		Assert.Empty(HakoDiagramLayoutCalculator.BuildTrainSegments([], columns));
		Assert.Empty(HakoDiagramLayoutCalculator.BuildTrainSegments([Train("a", "1A", Direction.Outbound, Row("A", 0), Row("B", 10))], []));
	}

	[Fact]
	public void BuildTrainSegments_OutboundTrain_IsLeftToRight()
	{
		TrainData train = Train("a", "1A", Direction.Outbound, Row("新宿", 0), Row("東京", 100));
		var columns = HakoDiagramLayoutCalculator.BuildStationColumns([train], 16);

		var segments = HakoDiagramLayoutCalculator.BuildTrainSegments([train], columns);

		var segment = Assert.Single(segments);
		Assert.Equal("a", segment.TrainId);
		Assert.Equal("1A", segment.TrainNumber);
		Assert.Equal(0, segment.RowIndex);
		Assert.True(segment.IsLeftToRight);
		Assert.True(segment.StartLineIndex < segment.EndLineIndex);
	}

	[Fact]
	public void BuildTrainSegments_InboundTrain_IsRightToLeft()
	{
		TrainData outbound = Train("a", "1A", Direction.Outbound, Row("新宿", 0), Row("東京", 100));
		TrainData inbound = Train("b", "1B", Direction.Inbound, Row("東京", 100), Row("新宿", 0));
		var columns = HakoDiagramLayoutCalculator.BuildStationColumns([outbound, inbound], 16);

		var segments = HakoDiagramLayoutCalculator.BuildTrainSegments([outbound, inbound], columns);

		TrainSegmentFor(segments, "b", out var segment);
		Assert.False(segment.IsLeftToRight);
		Assert.True(segment.StartLineIndex > segment.EndLineIndex);
	}

	[Fact]
	public void BuildTrainSegments_PreservesOriginalIndexAsRowIndexWhenEarlierTrainIsSkipped()
	{
		TrainData unresolvable = Train("skip", "9999", Direction.Outbound); // no Rows
		TrainData resolvable = Train("a", "1A", Direction.Outbound, Row("新宿", 0), Row("東京", 100));
		var columns = HakoDiagramLayoutCalculator.BuildStationColumns([resolvable], 16);

		var segments = HakoDiagramLayoutCalculator.BuildTrainSegments([unresolvable, resolvable], columns);

		var segment = Assert.Single(segments);
		Assert.Equal("a", segment.TrainId);
		Assert.Equal(1, segment.RowIndex);
	}

	[Fact]
	public void BuildTrainSegments_PlaceholderFields_DefaultToUndefined()
	{
		TrainData train = Train("a", "1A", Direction.Outbound, Row("新宿", 0), Row("東京", 100));
		var columns = HakoDiagramLayoutCalculator.BuildStationColumns([train], 16);

		var segment = Assert.Single(HakoDiagramLayoutCalculator.BuildTrainSegments([train], columns));

		Assert.Equal(HakoDiagramLayoutCalculator.PreviousTrainConnectorStyle.Undefined, segment.ConnectorFromPreviousTrain);
		Assert.Equal(HakoDiagramLayoutCalculator.TrainBoundaryMark.Undefined, segment.DepartureMark);
		Assert.Equal(HakoDiagramLayoutCalculator.TrainBoundaryMark.Undefined, segment.ArrivalMark);
	}

	static void TrainSegmentFor(IReadOnlyList<HakoDiagramLayoutCalculator.TrainSegment> segments, string trainId, out HakoDiagramLayoutCalculator.TrainSegment segment)
		=> segment = segments.Single(s => s.TrainId == trainId);
}
