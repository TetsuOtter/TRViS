namespace TRViS.DTAC.Logic.Formatters;

/// <summary>
/// Pure layout calculator for TabButton width requests.
/// Contains no UI framework dependencies — fully unit-testable.
/// </summary>
public static class TabButtonLayoutCalculator
{
	/// <summary>
	/// Calculates the WidthRequest for a tab button given the total window width,
	/// the number of tab buttons displayed, and the maximum single-button width.
	/// Returns 0 if <paramref name="windowWidth"/> or <paramref name="tabButtonCount"/> is zero or negative.
	/// </summary>
	/// <param name="windowWidth">Current window width in device-independent pixels.</param>
	/// <param name="tabButtonCount">Number of tab buttons in the tab bar (typically 3).</param>
	/// <param name="maxWidth">Maximum allowed width for a single button.</param>
	/// <returns>Calculated WidthRequest value clamped to <paramref name="maxWidth"/>.</returns>
	public static double CalculateWidthRequest(double windowWidth, int tabButtonCount, double maxWidth)
	{
		if (windowWidth <= 0 || tabButtonCount <= 0)
			return 0;

		// 8dp is the total horizontal padding/margin of the tab bar container
		double calcedMaxWidth = (windowWidth - 8) / tabButtonCount;
		return Math.Min(calcedMaxWidth, maxWidth);
	}
}
