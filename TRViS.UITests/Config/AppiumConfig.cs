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
				// .NET MAUI Android generates activity class names with a CRC64 hash prefix
				// (e.g. crc64a112fd51566f77e9.MainActivity). The class is NOT in the app's
				// Java package, so "dev.t0r.trvis.*" never matches. Use ".*" to accept any
				// activity in the package. Also increase appWaitDuration to 60 s for slow
				// cold-starts on CI emulators.
				options.AddAdditionalAppiumOption("appWaitPackage", AppPackage);
				options.AddAdditionalAppiumOption("appWaitActivity", ".*");
				options.AddAdditionalAppiumOption("appWaitDuration", 60000);
				options.AddAdditionalAppiumOption("autoGrantPermissions", true);
				if (!string.IsNullOrEmpty(deviceUdid))
					options.AddAdditionalAppiumOption("udid", deviceUdid);
				break;

			case TestPlatform.iOS:
				options.AutomationName = "XCUITest";
				options.PlatformName = "iOS";
				options.App = appPath;
				options.AddAdditionalAppiumOption("bundleId", AppPackage);
				options.AddAdditionalAppiumOption("autoAcceptAlerts", true);
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
