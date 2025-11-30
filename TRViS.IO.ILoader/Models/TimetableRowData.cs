namespace TRViS.IO.Models;

public record TimetableRowData(
	string Id,
	string TrainId,
	string StationId,
	int? DriveTime_MM,
	int? DriveTime_SS,
	bool? IsOperationOnlyStop,
	bool? IsPass,
	bool? HasBracket,
	bool? IsLastStop,
	int? Arrive_HH,
	int? Arrive_MM,
	int? Arrive_SS,
	string? Arrive_Str,
	int? Departure_HH,
	int? Departure_MM,
	int? Departure_SS,
	string? Departure_Str,
	string? StationTrackId,
	int? RunInLimit,
	int? RunOutLimit,
	string? Remarks,
	string? MarkerColorId,
	string? MarkerText,
	int? WorkType
)
{ }
