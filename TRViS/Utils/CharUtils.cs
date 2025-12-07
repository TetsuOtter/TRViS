using System.Text;

namespace TRViS;

public static partial class Util
{
	public const string SPACE_CHAR = "\x2002";
	public const string THIN_SPACE = "\x2009";

	static public string InsertCharBetweenCharAndMakeWide(string input, string toInsert)
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

	static public char ToWide(char c)
		=> c switch
		{
			>= '\x21' and <= '\x7E' => (char)(c - '\x21' + '\xFF01'),
			_ => c
		};

	static public string ToWide(string s)
		=> new string(s.Select(ToWide).ToArray());

	static public string InsertBetweenChars(ReadOnlySpan<char> chars, string toInsert)
	{
		StringBuilder builder = new(chars.Length + toInsert.Length * (chars.Length - 1));

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

	static public string InsertBetweenChars(ReadOnlySpan<char> chars, char toInsert)
	{
		StringBuilder builder = new(chars.Length + (chars.Length - 1));

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
