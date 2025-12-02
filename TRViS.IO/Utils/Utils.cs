using System.Globalization;

namespace TRViS.IO;

public static partial class Utils
{
	public static bool IsArrayEquals<T>(T[]? arr1, T[]? arr2, IEqualityComparer<T>? comparer = null)
	{
		if (arr1 == arr2)
			return true;
		else if (arr1 is null || arr2 is null)
			return false;
		else if (arr1.Length != arr2.Length)
			return false;

		return arr1.AsSpan().SequenceEqual(arr2.AsSpan(), comparer);
	}

	/// <summary>
	/// Converts a hex color string (e.g., "3366CC" or "CC") to an RGB integer.
	/// Returns null if the input is null, empty, or invalid.
	/// </summary>
	/// <remarks>
	/// Note: This method is duplicated in JsonModelsConverter due to the project dependency structure.
	/// TRViS.IO.ILoader cannot reference TRViS.IO to avoid circular dependencies.
	/// </remarks>
	public static int? HexStringToRgbInt(string? hexString)
	{
		if (string.IsNullOrEmpty(hexString))
			return null;

		// Remove leading '#' if present
		if (hexString.StartsWith('#'))
			hexString = hexString.Substring(1);

		if (int.TryParse(hexString, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int result))
			return result;

		return null;
	}
}
