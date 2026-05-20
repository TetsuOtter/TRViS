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
			"windows" => TestPlatform.Windows,
			_ => throw new ArgumentException($"Unknown platform: {platformStr}", nameof(platformStr)),
		};
}
