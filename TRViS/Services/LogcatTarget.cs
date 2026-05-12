#if UI_TEST && ANDROID
using NLog;
using NLog.Targets;

namespace TRViS.Services;

/// <summary>
/// UI_TEST-only NLog target that bridges to <c>Android.Util.Log</c> so the
/// suite's logcat capture (run-ui-tests.sh / GitHub Actions ui-test-diagnostics
/// artifact) sees app-side log lines.
///
/// In a normal build NLog writes to the per-process log file under
/// <c>DirectoryPathProvider.GeneralLogFileDirectory</c>, which the CI runner
/// can't reach without ADB-pulling from the app data dir. The logcat bridge
/// lets us answer diagnostic questions like "did OnSelectFileClicked actually
/// fire?" from the dump artifact alone, without rebuilding with extra trace.
///
/// Gated on <c>UI_TEST</c> so production builds carry no extra Android.Util
/// dependency and no per-log dispatch overhead.
/// </summary>
[Target("Logcat")]
public sealed class LogcatTarget : TargetWithLayout
{
	protected override void Write(LogEventInfo logEvent)
	{
		// MAUI tag convention: prefix everything with our package short-name
		// so logcat filters like `grep TRViS` find the bridged lines easily.
		// NLog category already prefixes our loggers (TRViS.General.<class>),
		// so passing the LoggerName as the Android tag preserves that context.
		string tag = logEvent.LoggerName ?? "TRViS";
		string message = Layout.Render(logEvent);

		switch (logEvent.Level.Ordinal)
		{
			case 0: // Trace
			case 1: // Debug
				Android.Util.Log.Debug(tag, message);
				break;
			case 2: // Info
				Android.Util.Log.Info(tag, message);
				break;
			case 3: // Warn
				Android.Util.Log.Warn(tag, message);
				break;
			case 4: // Error
				Android.Util.Log.Error(tag, message);
				break;
			case 5: // Fatal
				Android.Util.Log.Wtf(tag, message);
				break;
			default:
				Android.Util.Log.Info(tag, message);
				break;
		}
	}
}
#endif
