namespace TRViS.IO.Models;

/// <summary>
/// Extended WorkGroupData class for JSON deserialization that uses WorkDataJson.
/// This mirrors TRViS.JsonModels.WorkGroupData but uses WorkDataJson for extended work and train data properties.
/// </summary>
public record WorkGroupDataJson(
	string? Id,
	string Name,
	int? DBVersion,
	WorkDataJson[] Works
);
