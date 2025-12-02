namespace TRViS.IO.Models;

/// <summary>
/// Extended TrainData class for JSON deserialization that includes NextTrainId property.
/// This mirrors TRViS.JsonModels.TrainData but adds NextTrainId for JSON support.
/// </summary>
public record TrainDataJson(
	string? Id,
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
	JsonModels.TimetableRowData[] TimetableRows,
	/// <summary>
	/// The ID of the next train.
	/// - null: Use default behavior (next train in the list)
	/// - empty string "": No next train (don't show next train button)
	/// - string value: The ID of the train to show as next train
	/// </summary>
	string? NextTrainId = null
);
