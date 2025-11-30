namespace TRViS.IO.Models;

public record Work(
	string Id,
	string WorkGroupId,
	string Name,
	DateOnly? AffectDate = null,
	int? AffixContentType = null,
	byte[]? AffixContent = null,
	string? Remarks = null,
	bool? HasETrainTimetable = null,
	int? ETrainTimetableContentType = null,
	byte[]? ETrainTimetableContent = null
) : IHasRemarksProperty
{
}
