namespace TRViS.Models;

public record TimetableRow(
	int? DriveTimeMM,
	int? DriveTimeSS,
	string StationName,
	bool IsOperationOnlyStop,
	bool IsPass,
	bool HasBracket,
	bool IsLastStop,
	TimeData? ArriveTime,
	TimeData? DepartureTime,
	string TrackName,
	int? RunInLimit,
	int? RunOutLimit,
	string? Remarks
	)
{
	const string SPACE_CHAR = "\x2002";
	const string SPACE_CHAR_4 = "\x2002\x2002\x2002\x2002";
	public string SpacedStationName
		=> StationName.Length switch
		{
			2 => ToDispString(StationName, SPACE_CHAR_4),
			3 => ToDispString(StationName, SPACE_CHAR),
			4 => ToDispString(StationName, "\x200A"),
			_ => StationName
		};

	static string ToDispString(string input, string toInsert)
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

	static char ToWide(char c)
		=> c switch
		{
			>= '\x21' and <= '\x7E' => (char)(c - '\x21' + '\xFF01'),
			_ => c
		};
}
