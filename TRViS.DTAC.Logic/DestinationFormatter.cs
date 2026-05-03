namespace TRViS.DTAC.Logic;

/// <summary>
/// Formats destination strings for D-TAC display
/// </summary>
public static class DestinationFormatter
{
	public const string SPACE_CHAR = "\x2002";

	/// <summary>
	/// Formats a destination string for display with proper spacing
	/// </summary>
	/// <param name="destination">The destination string to format</param>
	/// <returns>The formatted destination string, or null if input is null or empty</returns>
	public static string? FormatDestination(string? destination)
	{
		if (string.IsNullOrEmpty(destination))
			return null;

		string formattedDestination = destination.Length switch
		{
			1 => $"{SPACE_CHAR}{destination}{SPACE_CHAR}",
			2 => $"{destination[0]}{SPACE_CHAR}{destination[1]}",
			_ => destination
		};

		return $"（{formattedDestination}行）";
	}
}
