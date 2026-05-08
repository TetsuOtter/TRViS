using System.Diagnostics;
using OpenQA.Selenium.Appium;

namespace TRViS.UITests.Infrastructure;

public abstract class BaseUITest
{
	private const string AppPackage = "dev.t0r.trvis";

	protected AppiumDriver Driver { get; private set; } = null!;

	/// <summary>
	/// True when running on Android. MAUI maps AutomationId to resource-id on Android.
	/// </summary>
	protected bool IsAndroid { get; private set; }

	private static readonly TimeSpan DefaultImplicitWait = TimeSpan.FromSeconds(10);

	/// <summary>
	/// Resets per-test app state so every test begins from a clean slate
	/// (e.g. Firebase consent page visible on Mac/iOS).
	/// </summary>
	private static void ResetAppState(TestPlatform platform)
	{
		switch (platform)
		{
			case TestPlatform.MacCatalyst:
				// Kill any running instance so the app restarts fresh
				RunProcess("pkill", "-f dev.t0r.trvis");
				Thread.Sleep(500);
				// Clear NSUserDefaults (includes preference-daemon cache flush)
				RunProcess("defaults", "delete dev.t0r.trvis");
				Thread.Sleep(200);
				break;

			case TestPlatform.Android:
				// Appium's UiAutomator2 reinstalls the APK on session creation,
				// which resets app data automatically.
				break;

			case TestPlatform.iOS:
				// XCUITest reinstalls the .app on session creation.
				break;

			case TestPlatform.Windows:
				// Kill any running TRViS instances so the next Appium session
				// launches a fresh process rather than attaching to the existing one
				// (which may be showing a page other than FirebaseSettingPage).
				foreach (var proc in Process.GetProcessesByName("TRViS"))
				{
					try { proc.Kill(entireProcessTree: true); } catch { }
				}
				Thread.Sleep(1000);

				// Clear app-specific preferences to ensure a clean state.
				// MAUI Preferences on unpackaged Windows apps may store data under
				// LocalAppData using the ApplicationId or the assembly name.
				// Delete both possible paths to ensure a full reset.
				var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
				foreach (var folderName in new[] { AppPackage, "TRViS" })
				{
					var path = Path.Combine(localAppData, folderName);
					try
					{
						if (Directory.Exists(path))
						{
							Directory.Delete(path, recursive: true);
							TestContext.Out.WriteLine($"Deleted {path}");
						}
					}
					catch (Exception ex)
					{
						TestContext.Out.WriteLine($"Warning: Could not delete {path}: {ex.Message}");
					}
				}
				Thread.Sleep(200);
				break;
		}
	}

	private static void RunProcess(string fileName, string arguments)
	{
		try
		{
			using var p = Process.Start(new ProcessStartInfo(fileName, arguments)
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
			});
			p?.WaitForExit(3000);
		}
		catch (Exception ex)
		{
			TestContext.Out.WriteLine($"ResetAppState: {fileName} {arguments} failed: {ex.Message}");
		}
	}

	[SetUp]
	public virtual void SetUp()
	{
		var platformStr = TestContext.Parameters["platform"]
			?? throw new InvalidOperationException("TestRunParameter 'platform' is required.");
		var appPath = TestContext.Parameters["appPath"]
			?? throw new InvalidOperationException("TestRunParameter 'appPath' is required.");
		var appiumUrl = TestContext.Parameters["appiumUrl"] ?? "http://localhost:4723";

		var platform = AppiumConfig.ParsePlatform(platformStr);
		ResetAppState(platform);
		SetUpDriver(platform, appPath, appiumUrl);
	}

	protected virtual void SetUpDriver(TestPlatform platform, string appPath, string appiumUrl)
	{
		var deviceUdid = TestContext.Parameters["deviceUdid"];
		var options = AppiumConfig.CreateOptions(platform, appPath, deviceUdid);
		var serverUri = new Uri(appiumUrl);

		// On iOS the first session has to build WebDriverAgent via xcodebuild,
		// install the .app, and boot the simulator. On macos-26 + Xcode 26.4
		// the iPhone simulator has been observed to take >10 minutes for that
		// cold start, exceeding the .NET Appium client's default 600 s
		// HTTP timeout. Bump it for iOS so the very first session-creation
		// HTTP request doesn't bail out before WDA finishes coming up.
		var iOSCommandTimeout = TimeSpan.FromMinutes(20);

		Driver = platform switch
		{
			TestPlatform.Android => new AndroidDriver(serverUri, options),
			TestPlatform.iOS => new IOSDriver(serverUri, options, iOSCommandTimeout),
			TestPlatform.MacCatalyst => new MacDriver(serverUri, options),
			TestPlatform.Windows => new WindowsDriver(serverUri, options),
			_ => throw new ArgumentOutOfRangeException(nameof(platform)),
		};

		IsAndroid = platform == TestPlatform.Android;
		Driver.Manage().Timeouts().ImplicitWait = DefaultImplicitWait;

		// On Windows, maximize the window so WinUI NavigationView stays in Left mode
		// (pane always visible). At ≥ExpandedModeThresholdWidth (1008 px) the pane is
		// permanently open and NavigateToXxx calls do not need to click PaneToggleButton.
		if (platform == TestPlatform.Windows)
			Driver.Manage().Window.Maximize();
	}

	[TearDown]
	public void TearDown()
	{
		if (Driver is not null)
		{
			TakeScreenshot();
			Driver.Quit();
		}
	}

	protected void TakeScreenshot()
	{
		try
		{
			var screenshot = Driver.GetScreenshot();
			var testName = TestContext.CurrentContext.Test.FullName
				.Replace(' ', '_')
				.Replace('/', '_');
			var path = Path.Combine(
				TestContext.CurrentContext.WorkDirectory,
				$"{testName}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
			screenshot.SaveAsFile(path);
			TestContext.AddTestAttachment(path, "Screenshot");
		}
		catch (Exception ex)
		{
			TestContext.Out.WriteLine($"Screenshot failed: {ex.Message}");
		}
	}
}
