namespace TRViS.Models;

public record LicenseData(
	string id,
	string resolvedVersion,
	string license,
	string author,
	string? projectUrl,
	string? copyrightText
);
