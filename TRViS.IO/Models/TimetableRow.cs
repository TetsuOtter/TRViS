namespace TRViS.IO.Models;

public record TimetableRow(
	string Id,
	LocationInfo Location,
	int? DriveTimeMM,
	int? DriveTimeSS,
	string StationName,
	bool IsOperationOnlyStop,
	bool IsPass,
	bool HasBracket,
	bool IsLastStop,
	TimeData? ArriveTime,
	TimeData? DepartureTime,
	string? TrackName,
	int? RunInLimit,
	int? RunOutLimit,
	string? Remarks,
	bool IsInfoRow = false,
	int? DefaultMarkerColor_RGB = null,
	string? DefaultMarkerText = null
);
