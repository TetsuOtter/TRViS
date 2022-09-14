namespace TRViS;

public static partial class Utils
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
}
