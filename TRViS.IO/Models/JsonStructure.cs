namespace TRViS.IO.Models.Json;

public record TimetableRowData(
	string StationName,
	double Location_m,
	double? Longitude_deg,
	double? Latitude_deg,
	double? OnStationDetectRadius_m,
	string? FullName,
	int? RecordType,

	string? TrackName,

	int? DriveTime_MM,
	int? DriveTime_SS,
	bool? IsOperationOnlyStop,
	bool? IsPass,
	bool? HasBracket,
	bool? IsLastStop,
	string? Arrive,
	string? Departure,
	int? RunInLimit,
	int? RunOutLimit,
	string? Remarks,
	string? MarkerColor,
	string? MarkerText,
	int? WorkType
);

public record TrainData(
	string TrainNumber,
	string? MaxSpeed,
	string? SpeedType,
	string? NominalTractiveCapacity,
	int? CarCount,
	string? Destination,
	string? BeginRemarks,
	string? AfterRemarks,
	string? Remarks,
	string? BeforeDeparture,
	string? TrainInfo,
	int Direction,
	int? WorkType,
	string? AfterArrive,
	string? BeforeDeparture_OnStationTrackCol,
	string? AfterArrive_OnStationTrackCol,
	int? DayCount,
	bool? IsRideOnMoving,
	string? Color,
	TimetableRowData[] TimetableRows
);

public record WorkData(
	string Name,
	string? AffectDate,
	int? AffixContentType,
	string? AffixContent,
	string? Remarks,
	bool? HasETrainTimetable,
	int? ETrainTimetableContentType,
	string? ETrainTimetableContent,
	TrainData[] Trains
);

public record WorkGroupData(
	string Name,
	int? DBVersion,
	WorkData[] Works
);
