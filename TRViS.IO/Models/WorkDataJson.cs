namespace TRViS.IO.Models;

/// <summary>
/// Extended WorkData class for JSON deserialization that uses TrainDataJson.
/// This mirrors TRViS.JsonModels.WorkData but uses TrainDataJson for extended train data properties.
/// </summary>
public record WorkDataJson(
	string? Id,
	string Name,
	string? AffectDate,
	int? AffixContentType,
	string? AffixContent,
	string? Remarks,
	bool? HasETrainTimetable,
	int? ETrainTimetableContentType,
	string? ETrainTimetableContent,
	TrainDataJson[] Trains
);
