namespace TRViS.DTAC.Logic.Formatters;

/// <summary>
/// Formats drive time minute/second values for display in the timetable.
/// </summary>
public static class DriveTimeFormatter
{
	/// <summary>
	/// Formats the drive-time minutes value.
	/// Returns "**" when the text exceeds 2 characters (overflow indicator).
	/// Returns null/empty as-is.
	/// </summary>
	public static string? FormatMinutes(string? driveTimeMM)
	{
		if (string.IsNullOrEmpty(driveTimeMM))
			return driveTimeMM;

		return driveTimeMM.Length > 2 ? "**" : driveTimeMM;
	}

	/// <summary>
	/// Formats the drive-time seconds value.
	/// Prepends two spaces when the text is a single character, to align it visually.
	/// Returns null/empty as-is.
	/// </summary>
	public static string? FormatSeconds(string? driveTimeSS)
	{
		if (string.IsNullOrEmpty(driveTimeSS))
			return driveTimeSS;

		return driveTimeSS.Length == 1 ? "  " + driveTimeSS : driveTimeSS;
	}
}
