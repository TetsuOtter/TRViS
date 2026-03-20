using OpenQA.Selenium.Appium;

namespace TRViS.UITests.Config;

public static class AppiumConfig
{
	public const string AppPackage = "dev.t0r.trvis";

	public static AppiumOptions CreateOptions(TestPlatform platform, string appPath)
	{
		var options = new AppiumOptions();

		switch (platform)
		{
			case TestPlatform.Android:
				options.AutomationName = "UiAutomator2";
				options.PlatformName = "Android";
				options.App = appPath;
				options.AddAdditionalAppiumOption("appPackage", AppPackage);
				options.AddAdditionalAppiumOption("appActivity", $"{AppPackage}.MainActivity");
				options.AddAdditionalAppiumOption("autoGrantPermissions", true);
				break;

			case TestPlatform.iOS:
				options.AutomationName = "XCUITest";
				options.PlatformName = "iOS";
				options.App = appPath;
				options.AddAdditionalAppiumOption("bundleId", AppPackage);
				options.AddAdditionalAppiumOption("autoAcceptAlerts", true);
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
