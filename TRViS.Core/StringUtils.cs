using System.Text;

namespace TRViS.Core;

/// <summary>
/// Utility methods for string manipulation
/// </summary>
public static class StringUtils
{
	public const string SPACE_CHAR = "\x2002";
	public const string THIN_SPACE = "\x2009";

	/// <summary>
	/// Inserts a character between each character in the input and converts to wide characters
	/// </summary>
	public static string InsertCharBetweenCharAndMakeWide(string input, string toInsert)
	{
		string? ret = null;
		foreach (char v in input)
		{
			char c = ToWide(v);

			if (ret is null)
				ret = c.ToString();
			else
				ret += toInsert + c;
		}

		return ret ?? "";
	}

	/// <summary>
	/// Converts a character to its wide (full-width) equivalent
	/// </summary>
	public static char ToWide(char c)
		=> c switch
		{
			>= '\x21' and <= '\x7E' => (char)(c - '\x21' + '\xFF01'),
			_ => c
		};

	/// <summary>
	/// Converts a string to its wide (full-width) equivalent
	/// </summary>
	public static string ToWide(string s)
		=> new string(s.Select(ToWide).ToArray());

	/// <summary>
	/// Inserts a string between each character in the input
	/// </summary>
	public static string InsertBetweenChars(ReadOnlySpan<char> chars, string toInsert)
	{
		if (chars.Length == 0)
			return string.Empty;

		StringBuilder builder = new(chars.Length + toInsert.Length * Math.Max(0, chars.Length - 1));

		foreach (char v in chars)
		{
			if (builder.Length != 0)
			{
				builder.Append(toInsert);
			}

			builder.Append(v);
		}

		return builder.ToString();
	}

	/// <summary>
	/// Inserts a character between each character in the input
	/// </summary>
	public static string InsertBetweenChars(ReadOnlySpan<char> chars, char toInsert)
	{
		if (chars.Length == 0)
			return string.Empty;

		StringBuilder builder = new(chars.Length + Math.Max(0, chars.Length - 1));

		foreach (char v in chars)
		{
			if (builder.Length != 0)
			{
				builder.Append(toInsert);
			}

			builder.Append(v);
		}

		return builder.ToString();
	}
}
