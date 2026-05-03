using TRViS.DTAC.Logic.Abstractions;

namespace TRViS.Web.Adapters;

public sealed class ConsoleCrashLogger : IDtacCrashLogger
{
	public void Log(Exception ex, string? context = null)
	{
		Console.Error.WriteLine($"[CrashLog] {context}: {ex}");
	}
}
