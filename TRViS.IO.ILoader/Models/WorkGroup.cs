namespace TRViS.IO.Models;

public record WorkGroup(
	string Id,
	string Name,
	int? DBVersion = 0
)
{ }
