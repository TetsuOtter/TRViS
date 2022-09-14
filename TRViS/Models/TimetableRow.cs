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
			2 => Utils.InsertCharBetweenCharAndMakeWide(StationName, SPACE_CHAR_4),
			3 => Utils.InsertCharBetweenCharAndMakeWide(StationName, SPACE_CHAR),
			4 => Utils.InsertCharBetweenCharAndMakeWide(StationName, "\x200A"),
			_ => StationName
		};
}
