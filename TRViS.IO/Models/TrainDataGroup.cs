namespace TRViS.IO.Models;

public record TrainDataFileInfo(
	string WorkID,
	string TrainID,
	string WorkName,
	string TrainNumber
	);

public record TrainDataGroup(
	string ID,
	string GroupName,
	TrainDataFileInfo[] FileInfoArray
	);
