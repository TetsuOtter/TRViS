using TRViS.DTAC.Logic.Abstractions;
using TRViS.FirebaseWrapper;

namespace TRViS.DTAC.Adapters;

/// <summary>
/// Adapter that wraps ICrashlyticsWrapper to implement IDtacCrashLogger.
/// </summary>
internal class CrashLoggerAdapter : IDtacCrashLogger
{
	private readonly ICrashlyticsWrapper _crashlytics;

	public CrashLoggerAdapter(ICrashlyticsWrapper crashlytics)
	{
		_crashlytics = crashlytics ?? throw new ArgumentNullException(nameof(crashlytics));
	}

	public void Log(Exception ex, string? context = null)
	{
		_crashlytics.Log(ex, context);
	}
}
