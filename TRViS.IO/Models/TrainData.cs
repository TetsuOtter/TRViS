namespace TRViS.IO.Models;

public record TrainData(
	string? WorkName,
	DateOnly AffectDate,
	string? TrainNumber,
	string? MaxSpeed,
	string? SpeedType,
	string? NominalTractiveCapacity,
	int? CarCount,
	string? BeginRemarks,
	string? Remarks,
	TimetableRow[]? Rows,
	int Direction
);
