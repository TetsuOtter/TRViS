namespace TRViS.DTAC.Logic;

/// <summary>
/// Contains logic for displaying timetable information in D-TAC
/// </summary>
public class TimetableDisplayLogic
{
	/// <summary>
	/// Determines if the next day indicator should be visible based on day count
	/// </summary>
	public static bool ShouldShowNextDayIndicator(int dayCount)
	{
		return dayCount > 0;
	}

	/// <summary>
	/// Calculates the total height of content other than the timetable
	/// </summary>
	public static double CalculateNonTimetableContentHeight(
		double dateAndStartButtonHeight,
		double trainInfoHeaderHeight,
		double trainInfoRowHeight,
		double trainInfoBeforeDepartureHeight,
		double carCountAndBeforeRemarksHeight,
		double timetableHeaderHeight)
	{
		return dateAndStartButtonHeight
			+ trainInfoHeaderHeight
			+ trainInfoRowHeight
			+ trainInfoBeforeDepartureHeight
			+ carCountAndBeforeRemarksHeight
			+ timetableHeaderHeight;
	}

	/// <summary>
	/// Determines the appropriate scroll view height for the full content
	/// </summary>
	public static double CalculateScrollViewHeight(
		double currentHeight,
		double nonTimetableContentHeight,
		double timetableHeight)
	{
		double heightRequest = nonTimetableContentHeight + Math.Max(0, timetableHeight);
		return Math.Max(currentHeight, heightRequest);
	}
}
