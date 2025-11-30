namespace TRViS.IO.Models;

public record StationNameOtherLang(
	string StationId,
	string LanguageId,
	string Name,
	string? FullName = null
)
{ }
