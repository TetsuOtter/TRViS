namespace TRViS.IO.Models;

public record TrainData(
	string Id,
	string? WorkName,
	DateOnly? AffectDate,
	string? TrainNumber,
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
	TimetableRow[]? Rows,
	int Direction,
	string? AfterArrive = null,
	string? BeforeDepartureOnStationTrackCol = null,
	string? AfterArriveOnStationTrackCol = null,
	int DayCount = 0,
	bool? IsRideOnMoving = null,
	int? LineColor_RGB = null,
	string? NextTrainId = null
) : IHasRemarksProperty;
