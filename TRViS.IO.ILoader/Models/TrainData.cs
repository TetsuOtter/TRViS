namespace TRViS.IO.Models;

public record TrainData(
	string Id,
	Direction Direction,
	string? WorkName = null,
	DateOnly? AffectDate = null,
	string? TrainNumber = null,
	string? MaxSpeed = null,
	string? SpeedType = null,
	string? NominalTractiveCapacity = null,
	int? CarCount = null,
	string? Destination = null,
	string? BeginRemarks = null,
	string? AfterRemarks = null,
	string? Remarks = null,
	string? BeforeDeparture = null,
	string? TrainInfo = null,
	TimetableRow[]? Rows = null,
	string? AfterArrive = null,
	// string? BeforeDepartureOnStationTrackCol = null,
	// string? AfterArriveOnStationTrackCol = null,
	int DayCount = 0,
	bool? IsRideOnMoving = null,
	int? LineColor_RGB = null,
	string? NextTrainId = null
) : IHasRemarksProperty;
