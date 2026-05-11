using NUnit.Framework;
using OpenQA.Selenium.Appium;

namespace TRViS.UITests.Config;

public static class AppiumConfig
{
	public const string AppPackage = "dev.t0r.trvis";

	public static AppiumOptions CreateOptions(TestPlatform platform, string appPath, string? deviceUdid = null)
	{
		var options = new AppiumOptions();

		switch (platform)
		{
			case TestPlatform.Android:
				options.AutomationName = "UiAutomator2";
				options.PlatformName = "Android";
				options.App = appPath;
				options.AddAdditionalAppiumOption("appPackage", AppPackage);
				// .NET MAUI generates activity class names with a CRC64 hash prefix
				// (e.g. crc64a112fd51566f77e9.MainActivity). Setting appWaitActivity=".*"
				// with appWaitPackage causes appium-adb to build the regex
				// /^dev\.t0r\.trvis\..*$/ which never matches crc64…MainActivity.
				// Omit both so Appium falls back to the auto-detected appActivity from the
				// APK manifest (the exact crc64 class name), which matches correctly.
				// Keep a generous appWaitDuration because EmbedAssembliesIntoApk=true
				// means the first-launch Mono JIT compilation is slow.
				options.AddAdditionalAppiumOption("appWaitDuration", 300000);
				options.AddAdditionalAppiumOption("autoGrantPermissions", true);
				// Stability tuning — defaults are optimised for short, snappy native
				// apps. MAUI's Mono runtime + accessibility tree size produces commands
				// that intermittently overshoot the defaults; raising these reduces
				// "socket hang up" / instrumentation-crash flakes that the runner has
				// to recover from via [Retry(2)] on the test fixtures:
				// - newCommandTimeout: how long Appium waits for the next command from
				//   the client before tearing down the session. 60 s default; bumping
				//   to 180 s covers test bodies that include several Thread.Sleep gaps.
				// - adbExecTimeout: per-adb-call timeout. 20 s default; the emulator
				//   under heavy DTAC load can take ~25 s for a single AccessibilityNode
				//   walk.
				// - uiautomator2ServerLaunchTimeout: cold start of the UIA2 server
				//   APK. 30 s default is tight on an EmbedAssembliesIntoApk MAUI build.
				// - uiautomator2ServerInstallTimeout: same idea but for the install
				//   step (a freshly-restarted instrumentation process needs to be
				//   re-installed by the driver).
				options.AddAdditionalAppiumOption("newCommandTimeout", 180);
				options.AddAdditionalAppiumOption("adbExecTimeout", 60000);
				options.AddAdditionalAppiumOption("uiautomator2ServerLaunchTimeout", 90000);
				options.AddAdditionalAppiumOption("uiautomator2ServerInstallTimeout", 90000);
				if (!string.IsNullOrEmpty(deviceUdid))
					options.AddAdditionalAppiumOption("udid", deviceUdid);
				break;

			case TestPlatform.iOS:
				options.AutomationName = "XCUITest";
				options.PlatformName = "iOS";
				options.App = appPath;
				options.AddAdditionalAppiumOption("bundleId", AppPackage);
				options.AddAdditionalAppiumOption("autoAcceptAlerts", true);
				// On macos-26 with Xcode 26.4 the fresh simulator boot + WDA xcodebuild
				// has been observed to take >10 minutes on the matrix runners, so raise
				// each phase's timeout well past those upper bounds. The total
				// session-creation budget on the client side is set by
				// BaseUITest.SetUpDriver's IOSDriver commandTimeout (20 min) — these
				// caps must stay below that or a server-side abort will surface as a
				// confusing "response ended prematurely" error before the client
				// timeout has a chance to fire.
				options.AddAdditionalAppiumOption("simulatorStartupTimeout", 900000);
				options.AddAdditionalAppiumOption("wdaLaunchTimeout", 900000);
				// wdaStartupRetries: retry WDA launch on transient failures (e.g.
				// the WDA process crashes during startup). Keep at 2 for cheap recovery.
				options.AddAdditionalAppiumOption("wdaStartupRetries", 2);
				// usePrebuiltWDA + derivedDataPath: skip Appium's per-session
				// "verify WDA is built" xcodebuild call, which costs ~28 s per
				// new session even when WDA is already up. The runner script
				// (run-ui-tests.sh / CI build step) pre-builds WDA once via
				// `xcodebuild build-for-testing` into a known derivedDataPath,
				// then surfaces that path as a TestRunParameter so this code
				// can wire it onto the capability set. If the parameter is
				// absent we fall through to the default flow (build-on-first-
				// session) so callers that haven't adopted the seam still work.
				var wdaDerivedDataPath = TestContext.Parameters["wdaDerivedDataPath"];
				if (!string.IsNullOrEmpty(wdaDerivedDataPath))
				{
					options.AddAdditionalAppiumOption("usePrebuiltWDA", true);
					options.AddAdditionalAppiumOption("derivedDataPath", wdaDerivedDataPath);
				}
				// noReset:true — do not uninstall/reinstall the app between sessions.
				// run-ui-tests.sh pre-installs the app once (xcrun simctl install) so
				// FrontBoard always knows about the bundle. The xcuitest driver then
				// only terminates and relaunches the app per session, avoiding the
				// repeated uninstall/reinstall cycle that leaves FrontBoard in an
				// inconsistent state and causes "Application is unknown to FrontBoard"
				// session-creation failures mid-run. App data is cleared per-test via
				// BaseUITest.ResetAppState (xcrun simctl spawn defaults delete).
				options.AddAdditionalAppiumOption("noReset", true);
				// Specifying the simulator UDID directly lets xcuitest bypass SDK version
				// matching (which would fail when the app's DTPlatformVersion differs from
				// the only available simulator runtime).
				if (!string.IsNullOrEmpty(deviceUdid))
					options.AddAdditionalAppiumOption("udid", deviceUdid);
				break;

			case TestPlatform.MacCatalyst:
				options.AutomationName = "mac2";
				options.PlatformName = "mac";
				options.App = appPath;
				options.AddAdditionalAppiumOption("bundleId", AppPackage);
				break;

			case TestPlatform.Windows:
				options.AutomationName = "windows";
				options.PlatformName = "Windows";
				options.App = appPath;
				break;

			default:
				throw new ArgumentOutOfRangeException(nameof(platform), platform, null);
		}

		return options;
	}

	public static TestPlatform ParsePlatform(string platformStr) =>
		platformStr.ToLowerInvariant() switch
		{
			"android" => TestPlatform.Android,
			"ios" => TestPlatform.iOS,
			"mac" or "maccatalyst" => TestPlatform.MacCatalyst,
			"windows" => TestPlatform.Windows,
			_ => throw new ArgumentException($"Unknown platform: {platformStr}", nameof(platformStr)),
		};
}
