using TRViS.DTAC.Logic.Abstractions;

namespace TRViS.DTAC.Logic.Layout;

/// <summary>
/// Pure static helper for layout calculations in the vertical timetable view.
/// All methods are side-effect free and depend only on their arguments.
/// </summary>
public static class TimetableLayoutCalculator
{
	/// <summary>
	/// Display state for the current-location marker box and line.
	/// </summary>
	public readonly record struct LocationMarkerDisplay(
		bool IsBoxVisible,
		bool IsLineVisible,
		double BoxMarginTop);

	/// <summary>
	/// Calculates the total number of Grid row definitions needed.
	/// </summary>
	/// <param name="rowCount">Number of timetable data rows.</param>
	/// <param name="hasAfterRemarks">True when an AfterRemarks text is present.</param>
	/// <param name="hasAfterArrive">True when an AfterArrive text is present.</param>
	/// <param name="hasNextTrainButton">True when a NextTrainId is present.</param>
	/// <param name="isPhoneIdiom">True for phone/unknown idiom; false for tablet/desktop.</param>
	/// <param name="scrollViewHeight">Current ScrollView height (used for tablet idiom).</param>
	/// <param name="rowHeight">Height of a single row.</param>
	/// <returns>The required number of row definitions.</returns>
	public static int CalculateRowDefinitionCount(
		int rowCount,
		bool hasAfterRemarks,
		bool hasAfterArrive,
		bool hasNextTrainButton,
		bool isPhoneIdiom,
		double scrollViewHeight,
		double rowHeight)
	{
		if (rowCount < 0)
			throw new ArgumentOutOfRangeException(nameof(rowCount), "rowCount must be 0 or more");
		if (rowHeight <= 0)
			throw new ArgumentOutOfRangeException(nameof(rowHeight), "rowHeight must be positive");

		int count = rowCount;

		if (isPhoneIdiom)
		{
			// AfterRemarks row is always appended for phone
			count += 1;
			if (hasAfterArrive)
				count += 1;
			if (hasNextTrainButton)
				count += 1;
		}
		else
		{
			// Tablet: fill to at least fill the visible area, with extra rows
			int minCount = (int)Math.Floor(scrollViewHeight / rowHeight);
			int additionalRowsCount = Math.Max(2, (int)Math.Ceiling(scrollViewHeight / rowHeight) - 2);
			count += additionalRowsCount;
			count = Math.Max(minCount, count);
		}

		return Math.Max(0, count);
	}

	/// <summary>
	/// Calculates the Grid HeightRequest from the number of row definitions.
	/// </summary>
	public static double CalculateGridHeightRequest(int rowDefinitionCount, double rowHeight)
	{
		if (rowHeight <= 0)
			throw new ArgumentOutOfRangeException(nameof(rowHeight), "rowHeight must be positive");

		return rowDefinitionCount * rowHeight;
	}

	/// <summary>
	/// Returns the Grid row index at which AfterArrive should be placed.
	/// </summary>
	public static int CalculateAfterArriveRowIndex(int rowsCount)
		=> rowsCount + 1;

	/// <summary>
	/// Returns the Grid row index at which the NextTrainButton should be placed.
	/// </summary>
	public static int CalculateNextTrainButtonRowIndex(int rowsCount, bool hasAfterArrive)
		=> hasAfterArrive ? rowsCount + 2 : rowsCount + 1;

	/// <summary>
	/// Finds the index of the last non-info (station) row in the list.
	/// Returns 0 when the list is empty or all rows are info rows.
	/// </summary>
	/// <param name="isInfoRowList">Ordered list of IsInfoRow flags (one per row).</param>
	public static int FindLastTimetableRowIndex(IReadOnlyList<bool> isInfoRowList)
	{
		if (isInfoRowList is null)
			throw new ArgumentNullException(nameof(isInfoRowList));

		int last = 0;
		for (int i = 0; i < isInfoRowList.Count; i++)
		{
			if (!isInfoRowList[i])
				last = i;
		}
		return last;
	}

	/// <summary>
	/// Calculates the scroll target Y position (pixels) for the given marker row position.
	/// </summary>
	/// <param name="locationMarkerPosition">Zero-based row index of the marker, or negative if none.</param>
	/// <param name="rowHeight">Height of a single row.</param>
	/// <returns>Y coordinate to scroll to.</returns>
	public static double CalculateScrollTargetY(int locationMarkerPosition, double rowHeight)
	{
		if (rowHeight <= 0)
			throw new ArgumentOutOfRangeException(nameof(rowHeight), "rowHeight must be positive");

		if (locationMarkerPosition <= 0)
			return 0;

		return (locationMarkerPosition - 1) * rowHeight;
	}

	/// <summary>
	/// Calculates the display properties of the current-location marker.
	/// </summary>
	/// <param name="state">Current location state.</param>
	/// <param name="rowHeight">Height of a single row (used to compute margin offset).</param>
	/// <returns>Visibility and margin values for the marker box and line.</returns>
	public static LocationMarkerDisplay CalculateLocationMarkerDisplay(
		TimetableLocationState state,
		double rowHeight)
	{
		if (rowHeight <= 0)
			throw new ArgumentOutOfRangeException(nameof(rowHeight), "rowHeight must be positive");

		return state switch
		{
			TimetableLocationState.Undefined =>
				new LocationMarkerDisplay(IsBoxVisible: false, IsLineVisible: false, BoxMarginTop: 0),

			TimetableLocationState.AroundThisStation =>
				new LocationMarkerDisplay(IsBoxVisible: true, IsLineVisible: false, BoxMarginTop: 0),

			TimetableLocationState.RunningToNextStation =>
				new LocationMarkerDisplay(IsBoxVisible: true, IsLineVisible: true, BoxMarginTop: -(rowHeight / 2)),

			_ =>
				new LocationMarkerDisplay(IsBoxVisible: false, IsLineVisible: false, BoxMarginTop: 0),
		};
	}
}
