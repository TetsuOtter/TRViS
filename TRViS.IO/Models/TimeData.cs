namespace TRViS.IO.Models;

public record TimeData(
	int? Hour,
	int? Minute,
	int? Second,
	string? Text
)
{
	public string GetTimeString()
	{
		if (Hour is null && Minute is null && Second is null)
			return Text ?? string.Empty;

		string hh = Hour?.ToString("D2") ?? string.Empty;
		string mm = Minute?.ToString("D2") ?? string.Empty;
		string ss = Second?.ToString("D2") ?? string.Empty;
		return $"{hh}:{mm}:{ss}";
	}
}
