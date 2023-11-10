namespace TRViS.IO;

internal static class Utils
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
}
