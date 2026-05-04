namespace TRViS.DTAC.Logic.Abstractions;

/// <summary>
/// Logs crash/exception information for D-TAC
/// </summary>
public interface IDtacCrashLogger
{
	/// <summary>
	/// Logs an exception with optional context
	/// </summary>
	void Log(Exception ex, string? context = null);
}
