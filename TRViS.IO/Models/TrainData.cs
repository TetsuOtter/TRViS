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
	string? Remarks
)
{
	public string SpacedTrainNumber
		=> TrainNumber?.Length switch
		{
			<= 5 => Utils.InsertCharBetweenCharAndMakeWide(TrainNumber, Utils.SPACE_CHAR),
			<= 8 => Utils.InsertCharBetweenCharAndMakeWide(TrainNumber, Utils.THIN_SPACE),
			_ => TrainNumber ?? ""
		};

	public string WideMaxSpeed
		=> Utils.ToWide(MaxSpeed ?? "");

	public string WideSpeedType
		=> Utils.ToWide(SpeedType ?? "");

	public string WideNTC
		=> Utils.ToWide(NominalTractiveCapacity ?? "");
}
