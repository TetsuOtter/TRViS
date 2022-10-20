namespace TRViS.IO.Models;

public record TrainData(
	string? WorkName,
	DateOnly AffectDate,
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
	int Direction
) : IHasRemarksProperty;
