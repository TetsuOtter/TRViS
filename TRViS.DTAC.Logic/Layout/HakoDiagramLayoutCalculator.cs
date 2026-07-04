using TRViS.IO.Models;

namespace TRViS.DTAC.Logic.Layout;

/// <summary>
/// Pure layout calculator for the diagram-style ("ハコ図") rendering of the Hako tab.
/// Contains no UI framework dependencies — fully unit-testable.
/// </summary>
/// <remarks>
/// The diagram is a graph-paper style grid of equally spaced vertical dashed lines. The number of
/// lines is not fixed: it grows with the available width so the diagram can also be shown on narrow
/// screens (see <see cref="CalculateGridLineCount"/>). Each line is <see cref="DefaultLineSpacing"/>
/// device-independent pixels from the next, and the two outermost (edge) lines must each keep at
/// least <see cref="DefaultEdgeMarginPx"/> px of clearance to the screen edge.
///
/// The two outermost lines are margins only; the remaining interior lines are where turn-back
/// stations (折り返し駅 — every station that is some train's departure or arrival station) are
/// placed. Stations are placed one after another starting at <see cref="FirstUsableLineIndex"/>,
/// each the same whole number of lines after the previous one — i.e. always the same number of
/// blank lines between any two consecutive stations for a given station count, but that per-count
/// step is the *largest* whole number that still lets all of them fit within
/// [<see cref="FirstUsableLineIndex"/>, gridLineCount - 2] (see <see cref="CalculateLineIndex"/>).
/// So fewer stations get spread out more, while the maximum station count a grid can hold
/// (see <see cref="MaxTurnBackStationCount"/>) is packed at the tightest step (a single blank line
/// between each). Each train is drawn as a horizontal line spanning from its departure station's
/// line to its arrival station's line, on its own row.
///
/// The minimum diagram is 5 lines (<see cref="MinGridLineCount"/>): two edge lines, two station
/// lines, and one blank line between them. When even that does not fit (or there are more turn-back
/// stations than the width can hold), the caller falls back to the simple stacked-row list.
/// </remarks>
public static class HakoDiagramLayoutCalculator
{
	/// <summary>
	/// Default horizontal distance (device-independent pixels) between two adjacent grid lines.
	/// Must match the value <see cref="TRViS.DTAC.HakoParts.DiagramView"/> renders at.
	/// </summary>
	public const double DefaultLineSpacing = 38;

	/// <summary>
	/// Default minimum clearance (device-independent pixels) kept between each edge (outermost) line
	/// and the screen edge on that side.
	/// </summary>
	public const double DefaultEdgeMarginPx = 81;

	/// <summary>
	/// Minimum number of grid lines required to show the diagram at all: two edge lines, two station
	/// lines, and one blank line between them.
	/// </summary>
	public const int MinGridLineCount = 5;

	/// <summary>
	/// Line index (in the 0..gridLineCount-1 coordinate space) of the first usable interior line.
	/// </summary>
	public const int FirstUsableLineIndex = 1;

	/// <summary>
	/// Placeholder for how a train's line should be visually connected to the previous train's
	/// line (or left unconnected). Concrete members are intentionally not defined yet —
	/// this exists so callers have a stable field to populate once the display rules are decided.
	/// </summary>
	public enum PreviousTrainConnectorStyle
	{
		/// <summary>
		/// Not yet determined / not yet implemented.
		/// </summary>
		Undefined = 0,
	}

	/// <summary>
	/// Placeholder for which mark should be drawn at a train's departure or arrival station.
	/// Concrete members are intentionally not defined yet — this exists so callers have a
	/// stable field to populate once the display rules are decided.
	/// </summary>
	public enum TrainBoundaryMark
	{
		/// <summary>
		/// Not yet determined / not yet implemented.
		/// </summary>
		Undefined = 0,
	}

	/// <summary>
	/// One turn-back station column in the diagram.
	/// </summary>
	/// <param name="StationName">Station name, as it appears in <see cref="TimetableRow.StationName"/>.</param>
	/// <param name="Location_m">Route-relative position, used to order columns left-to-right.</param>
	/// <param name="LineIndex">
	/// Position of this station's line in the 0..gridLineCount-1 coordinate space (always within
	/// [<see cref="FirstUsableLineIndex"/>, gridLineCount - 2]).
	/// </param>
	public readonly record struct StationColumn(
		string StationName,
		double Location_m,
		double LineIndex);

	/// <summary>
	/// One train's horizontal line segment in the diagram.
	/// </summary>
	/// <param name="TrainId"><see cref="TrainData.Id"/> of the train this segment represents.</param>
	/// <param name="TrainNumber"><see cref="TrainData.TrainNumber"/> shown on the segment's button.</param>
	/// <param name="RowIndex">Zero-based row position, in the same order as the input train list.</param>
	/// <param name="StartLineIndex">Line index of the departure station.</param>
	/// <param name="EndLineIndex">Line index of the arrival station.</param>
	/// <param name="IsLeftToRight">
	/// True when the train runs toward increasing line index (departure is left of arrival),
	/// false when it runs toward decreasing line index.
	/// </param>
	/// <param name="ConnectorFromPreviousTrain">
	/// Placeholder — how this segment connects to the previous train's segment. Not yet computed.
	/// </param>
	/// <param name="DepartureMark">Placeholder — mark drawn at the departure station. Not yet computed.</param>
	/// <param name="ArrivalMark">Placeholder — mark drawn at the arrival station. Not yet computed.</param>
	public readonly record struct TrainSegment(
		string TrainId,
		string? TrainNumber,
		int RowIndex,
		double StartLineIndex,
		double EndLineIndex,
		bool IsLeftToRight,
		PreviousTrainConnectorStyle ConnectorFromPreviousTrain,
		TrainBoundaryMark DepartureMark,
		TrainBoundaryMark ArrivalMark);

	/// <summary>
	/// Number of grid lines that fit in <paramref name="availableWidth"/> while keeping at least
	/// <paramref name="edgeMargin"/> px of clearance on each side. Returns 0 when the width cannot
	/// even hold a single line pair honoring the margins. The two edge lines span
	/// (count - 1) * <paramref name="lineSpacing"/> px, centered within the available width.
	/// </summary>
	public static int CalculateGridLineCount(
		double availableWidth,
		double lineSpacing = DefaultLineSpacing,
		double edgeMargin = DefaultEdgeMarginPx)
	{
		if (lineSpacing <= 0)
			throw new ArgumentOutOfRangeException(nameof(lineSpacing), "lineSpacing must be positive");

		double usableWidth = availableWidth - (2 * edgeMargin);
		if (usableWidth < 0)
			return 0;

		return (int)Math.Floor(usableWidth / lineSpacing) + 1;
	}

	/// <summary>
	/// Maximum number of turn-back stations that can be placed in a grid of
	/// <paramref name="gridLineCount"/> lines (one blank line between each). Returns 0 when the grid
	/// is smaller than <see cref="MinGridLineCount"/>, so the diagram is never offered below that.
	/// </summary>
	public static int MaxTurnBackStationCount(int gridLineCount)
		=> gridLineCount < MinGridLineCount ? 0 : (gridLineCount - 1) / 2;

	/// <summary>
	/// Smallest grid line count that can hold <paramref name="stationCount"/> stations: two edge
	/// lines, one line per station, and one blank line between each pair — but never fewer than
	/// <see cref="MinGridLineCount"/>.
	/// </summary>
	public static int MinGridLineCountFor(int stationCount)
	{
		if (stationCount <= 0)
			throw new ArgumentOutOfRangeException(nameof(stationCount), "stationCount must be positive");

		return Math.Max(MinGridLineCount, (2 * stationCount) + 1);
	}

	/// <summary>
	/// Decides whether the diagram layout should be used, based on the available width and the
	/// number of turn-back stations that would need to be placed. Falls back to the simple
	/// stacked-row list (by returning false) when the screen is too narrow to fit the required
	/// number of lines (honoring the edge margins), or when there are no turn-back stations at all.
	/// </summary>
	public static bool ShouldUseDiagramLayout(
		double availableWidth,
		int turnBackStationCount,
		double lineSpacing = DefaultLineSpacing,
		double edgeMargin = DefaultEdgeMarginPx)
	{
		if (turnBackStationCount <= 0)
			return false;

		int gridLineCount = CalculateGridLineCount(availableWidth, lineSpacing, edgeMargin);
		return turnBackStationCount <= MaxTurnBackStationCount(gridLineCount);
	}

	/// <summary>
	/// Builds the ordered list of turn-back stations (every distinct station that is some train's
	/// departure or arrival station), positioned at equal intervals across the usable interior grid
	/// lines of a <paramref name="gridLineCount"/>-line grid.
	/// </summary>
	/// <remarks>
	/// The station set (and therefore <see cref="IReadOnlyList{T}.Count"/>) depends only on the data,
	/// not on <paramref name="gridLineCount"/>. Positions are still computed even when
	/// <paramref name="gridLineCount"/> is too small to display the diagram — the caller hides it in
	/// that case — so the line count used for positioning is clamped up to
	/// <see cref="MinGridLineCountFor"/> to avoid underflow.
	/// </remarks>
	public static IReadOnlyList<StationColumn> BuildStationColumns(
		IReadOnlyList<TrainData>? trains,
		int gridLineCount)
	{
		if (trains is null || trains.Count == 0)
			return [];

		Dictionary<string, double> locationByStationName = [];
		foreach (TrainData train in trains)
		{
			TimetableRow? firstRow = FindFirstStationRow(train);
			TimetableRow? lastRow = FindLastStationRow(train);

			if (firstRow is not null && !locationByStationName.ContainsKey(firstRow.StationName))
				locationByStationName[firstRow.StationName] = firstRow.Location.Location_m;
			if (lastRow is not null && !locationByStationName.ContainsKey(lastRow.StationName))
				locationByStationName[lastRow.StationName] = lastRow.Location.Location_m;
		}

		var orderedStations = locationByStationName
			.OrderBy(static kv => kv.Value)
			.ToList();

		int stationCount = orderedStations.Count;
		if (stationCount == 0)
			return [];

		int positioningLineCount = Math.Max(gridLineCount, MinGridLineCountFor(stationCount));
		var result = new List<StationColumn>(stationCount);
		for (int i = 0; i < stationCount; i++)
		{
			(string stationName, double locationM) = (orderedStations[i].Key, orderedStations[i].Value);
			double lineIndex = CalculateLineIndex(i, stationCount, positioningLineCount);
			result.Add(new StationColumn(stationName, locationM, lineIndex));
		}

		return result;
	}

	/// <summary>
	/// Builds the per-train horizontal line segments, one per train that has resolvable
	/// departure/arrival stations within <paramref name="columns"/>. Trains whose stations
	/// cannot be resolved (e.g. no timetable rows) are skipped, but <see cref="TrainSegment.RowIndex"/>
	/// still reflects the train's original position in <paramref name="trains"/> so row placement
	/// stays consistent with the un-skipped list.
	/// </summary>
	public static IReadOnlyList<TrainSegment> BuildTrainSegments(
		IReadOnlyList<TrainData>? trains,
		IReadOnlyList<StationColumn> columns)
	{
		if (trains is null || trains.Count == 0 || columns is null || columns.Count == 0)
			return [];

		Dictionary<string, double> lineIndexByStationName = columns
			.ToDictionary(static c => c.StationName, static c => c.LineIndex);

		var result = new List<TrainSegment>(trains.Count);
		for (int i = 0; i < trains.Count; i++)
		{
			TrainData train = trains[i];
			TimetableRow? firstRow = FindFirstStationRow(train);
			TimetableRow? lastRow = FindLastStationRow(train);

			if (firstRow is null || lastRow is null)
				continue;
			if (!lineIndexByStationName.TryGetValue(firstRow.StationName, out double startLineIndex))
				continue;
			if (!lineIndexByStationName.TryGetValue(lastRow.StationName, out double endLineIndex))
				continue;

			result.Add(new TrainSegment(
				TrainId: train.Id,
				TrainNumber: train.TrainNumber,
				RowIndex: i,
				StartLineIndex: startLineIndex,
				EndLineIndex: endLineIndex,
				IsLeftToRight: startLineIndex < endLineIndex,
				ConnectorFromPreviousTrain: PreviousTrainConnectorStyle.Undefined,
				DepartureMark: TrainBoundaryMark.Undefined,
				ArrivalMark: TrainBoundaryMark.Undefined));
		}

		return result;
	}

	/// <summary>
	/// Calculates the line index (0..<paramref name="gridLineCount"/>-1 coordinate space) of the
	/// <paramref name="stationOrdinal"/>-th of <paramref name="stationCount"/> stations. Stations
	/// are placed one after another starting at <see cref="FirstUsableLineIndex"/>, each the same
	/// whole number of lines after the previous one. That step is the largest whole number for which
	/// all stations still fit within [<see cref="FirstUsableLineIndex"/>, <paramref name="gridLineCount"/> - 2]
	/// — so fewer stations get spread out more, while a station count that just fills the grid gets
	/// packed at a 1-blank-line step.
	/// </summary>
	public static double CalculateLineIndex(int stationOrdinal, int stationCount, int gridLineCount)
	{
		if (stationCount <= 0)
			throw new ArgumentOutOfRangeException(nameof(stationCount), "stationCount must be positive");
		if (stationOrdinal < 0 || stationOrdinal >= stationCount)
			throw new ArgumentOutOfRangeException(nameof(stationOrdinal), "stationOrdinal must be within [0, stationCount)");
		if (gridLineCount < MinGridLineCountFor(stationCount))
			throw new ArgumentOutOfRangeException(nameof(gridLineCount), $"gridLineCount {gridLineCount} is too small to place {stationCount} stations");

		if (stationCount == 1)
			return FirstUsableLineIndex;

		int lastUsableLineIndex = gridLineCount - 2;
		int usableSpan = lastUsableLineIndex - FirstUsableLineIndex;
		int step = usableSpan / (stationCount - 1);
		return FirstUsableLineIndex + (stationOrdinal * step);
	}

	static TimetableRow? FindFirstStationRow(TrainData train)
		=> train.Rows?.FirstOrDefault(static r => !r.IsInfoRow);

	static TimetableRow? FindLastStationRow(TrainData train)
		=> train.Rows?.LastOrDefault(static r => !r.IsInfoRow);
}
