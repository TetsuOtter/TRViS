using TRViS.DTAC.Logic.Abstractions;
using TRViS.FirebaseWrapper;
using TRViS.Services;

namespace TRViS.DTAC.Adapters;

/// <summary>
/// Adapter that wraps ICrashlyticsWrapper to implement IDtacCrashLogger.
/// Also writes to NLog so local file logs retain failure context (preserves
/// pre-Presenter behavior where View code-behind called both NLog and Crashlytics).
/// </summary>
internal class CrashLoggerAdapter : IDtacCrashLogger
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	private readonly ICrashlyticsWrapper _crashlytics;

	public CrashLoggerAdapter(ICrashlyticsWrapper crashlytics)
	{
		_crashlytics = crashlytics ?? throw new ArgumentNullException(nameof(crashlytics));
	}

	public void Log(Exception ex, string? context = null)
	{
		logger.Error(ex, context ?? string.Empty);
		_crashlytics.Log(ex, context);
	}
}
