using System.Diagnostics;
using NUnit.Framework.Interfaces;
using OpenQA.Selenium.Appium;

namespace TRViS.UITests.Infrastructure;

public abstract class BaseUITest
{
	private const string AppPackage = "dev.t0r.trvis";

	// Default lifecycle: one Appium session per test (full isolation; the
	// session is recreated in [SetUp] and torn down in [TearDown]). A
	// fixture can override `ShareSessionAcrossTestsInFixture` to instead
	// share a single session across all tests in the fixture — the session
	// is the dominant cost on iOS (WDA xcodebuild ~3 min cold + ~10-15 s
	// per session attach) and recreating it per test inflates the iOS
	// matrix wall-clock. Opting in is intentional per fixture because
	// shared-session mode keeps in-app state across tests, so the fixture
	// must provide its own cleanup in [SetUp] (see DTACTimetableTests).
	//
	// Storing the driver in a static field is safe because
	// [assembly: Parallelizable(ParallelScope.None)] guarantees only one
	// fixture is active at a time. Lifecycle code below clears it before
	// the next fixture starts.
	private static AppiumDriver? _driver;
	protected AppiumDriver Driver => _driver
		?? throw new InvalidOperationException(
			"Driver is null — SetUp/OneTimeSetUp did not run, or it ran but failed before assigning the driver.");

	/// <summary>
	/// True when running on Android. MAUI maps AutomationId to resource-id on Android.
	/// </summary>
	protected static bool IsAndroid { get; private set; }

	private static TestPlatform _platform;

	/// <summary>
	/// Opt-in: when overridden to true, the fixture's [OneTimeSetUp] creates
	/// a single Appium session that is reused by every test in the fixture,
	/// and [OneTimeTearDown] quits it. Tests that fail after all retries
	/// "cascade" — the next test in the fixture is reported Inconclusive
	/// rather than run against undefined post-failure state. Derived
	/// fixtures that flip this MUST own their inter-test cleanup
	/// (NavigateToHome, ClearLoaderForTesting, etc.) in their [SetUp]
	/// override because the base class only resets app state once at
	/// OneTimeSetUp.
	/// </summary>
	protected virtual bool ShareSessionAcrossTestsInFixture => false;

	// Fail-cascade state for fixtures opting into shared sessions. When any
	// test in such a fixture fails after all of its retries are exhausted,
	// the next test in the same fixture is reported as Inconclusive
	// ("Skipped" in the trx) instead of running against an unknown
	// post-failure state. Both fields are reset in OneTimeSetUp so each
	// fixture starts clean.
	//
	// `_currentTestName` separates "first attempt of a new test" from
	// "retry of the same test" — the [RetryAllTests] wrapper invokes
	// SetUp/TearDown per attempt and `TestContext.CurrentContext.Test.FullName`
	// is stable across retries (NUnit's RetryCommand reuses the same Test
	// object). When the names match, we are mid-retry and must NOT consult
	// the cascade flag — otherwise a transient first-attempt failure would
	// cause its own retry to be skipped.
	private static bool _priorTestFailedInFixture = false;
	private static string? _currentTestName = null;

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
				// Wipe the app's TRViS.UserContents folder so a JSON file seeded
				// by a previous test (e.g. SelectFileDialogTests fixtures) can't
				// trigger DefaultTimetableFileLoader.TryLoadDefaultTimetableAsync's
				// single-file auto-load on the next launch — that auto-load puts
				// StartHomePage in Home mode and hides Start-mode buttons
				// (SelectFileButton / LoadDemoButton), which then break any test
				// that depends on them, even in fixtures that never seeded files.
				//
				// Mac Catalyst is sandboxed (Entitlements.plist sets
				// com.apple.security.app-sandbox=true). MAUI's
				// FileSystem.AppDataDirectory resolves to a path under
				// ~/Library/Containers/dev.t0r.trvis/Data/, but the exact
				// sub-path varies by MAUI / .NET version (Library/Application Support/...
				// vs Documents/... etc.). Recursively glob TRViS.UserContents
				// directories under the container so we don't miss the actual
				// location regardless of MAUI's resolution.
				string? home = Environment.GetEnvironmentVariable("HOME");
				if (!string.IsNullOrEmpty(home))
				{
					string containerDir = Path.Combine(
						home, "Library", "Containers", AppPackage, "Data");
					if (Directory.Exists(containerDir))
					{
						try
						{
							var matches = Directory.EnumerateDirectories(
								containerDir, "TRViS.UserContents", SearchOption.AllDirectories).ToArray();
							foreach (string dir in matches)
							{
								try
								{
									Directory.Delete(dir, recursive: true);
									TestContext.Out.WriteLine($"ResetAppState(MacCatalyst): cleared {dir}");
								}
								catch (Exception ex)
								{
									TestContext.Out.WriteLine($"ResetAppState(MacCatalyst): failed to clear {dir}: {ex.Message}");
								}
							}
							if (matches.Length == 0)
								TestContext.Out.WriteLine($"ResetAppState(MacCatalyst): no TRViS.UserContents under {containerDir}");
						}
						catch (Exception ex)
						{
							TestContext.Out.WriteLine($"ResetAppState(MacCatalyst): enumerate {containerDir} failed: {ex.Message}");
						}
					}
					else
					{
						TestContext.Out.WriteLine($"ResetAppState(MacCatalyst): container {containerDir} does not exist");
					}
				}
				break;

			case TestPlatform.Android:
				// Appium's UiAutomator2 reinstalls the APK on session creation,
				// which resets app data automatically.
				break;

			case TestPlatform.iOS:
				// noReset:true keeps the app installed between sessions, so we
				// reset app data explicitly here instead of relying on reinstall.
				var iosUdid = TestContext.Parameters["deviceUdid"] ?? "";
				if (string.IsNullOrEmpty(iosUdid))
				{
					// Without a UDID we cannot target the right simulator; the next
					// session will inherit prior state (URL history, privacy flag,
					// etc.) and tests assuming "clean install" will fail. Surface
					// it instead of silently no-oping.
					TestContext.Out.WriteLine("ResetAppState(iOS): TestParameter 'deviceUdid' is empty — skipping app-data reset. Tests assuming a clean install will likely fail.");
					break;
				}
				// Terminate any leftover app process from the previous session
				// (noReset:true may leave the app running after Driver.Quit()).
				RunProcess("xcrun", $"simctl terminate {iosUdid} {AppPackage}");
				Thread.Sleep(300);
				// Clear NSUserDefaults (MAUI Preferences) directly inside the app's
				// data container. `simctl spawn defaults delete` writes to the
				// simulator's user defaults database, but MAUI Preferences on iOS
				// are persisted to the app's sandboxed
				// Library/Preferences/<bundle>.plist — defaults delete does not
				// reliably wipe that file, leaving URL history and other prefs
				// stale across sessions. Resolve the data container, then rm the
				// Library/Preferences folder so the app re-creates it fresh on
				// next launch.
				string? dataContainer = GetAppDataContainer(iosUdid, AppPackage);
				if (!string.IsNullOrEmpty(dataContainer))
				{
					string prefsDir = Path.Combine(dataContainer, "Library", "Preferences");
					try
					{
						if (Directory.Exists(prefsDir))
							Directory.Delete(prefsDir, recursive: true);
					}
					catch (Exception ex)
					{
						TestContext.Out.WriteLine($"ResetAppState(iOS): failed to clear {prefsDir}: {ex.Message}");
					}
					// Also wipe TRViS.UserContents so a single seeded JSON from a
					// previous test (SelectFileDialogTests fixture) can't trigger
					// DefaultTimetableFileLoader.TryLoadDefaultTimetableAsync's
					// single-file auto-load on the next launch — same Start-mode
					// vs. Home-mode regression the MacCatalyst path is fixing.
					// Glob recursively rather than hard-coding Documents/, since
					// the exact sub-path within the data container can shift
					// between MAUI / .NET / iOS-SDK versions.
					try
					{
						foreach (string dir in Directory.EnumerateDirectories(
							dataContainer, "TRViS.UserContents", SearchOption.AllDirectories))
						{
							try
							{
								Directory.Delete(dir, recursive: true);
								TestContext.Out.WriteLine($"ResetAppState(iOS): cleared {dir}");
							}
							catch (Exception ex)
							{
								TestContext.Out.WriteLine($"ResetAppState(iOS): failed to clear {dir}: {ex.Message}");
							}
						}
					}
					catch (Exception ex)
					{
						TestContext.Out.WriteLine($"ResetAppState(iOS): enumerate {dataContainer} failed: {ex.Message}");
					}
				}
				// Belt-and-braces: also try the global defaults database in case
				// any code path falls back to it.
				RunProcess("xcrun", $"simctl spawn {iosUdid} defaults delete {AppPackage}");
				Thread.Sleep(200);
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

	/// <summary>
	/// Returns the absolute path to the simulator app's data container, or null
	/// if the lookup failed. simctl prints the path on stdout for an installed
	/// app; absent → non-zero exit and empty stdout.
	/// </summary>
	private static string? GetAppDataContainer(string udid, string bundleId)
	{
		try
		{
			using var p = Process.Start(new ProcessStartInfo("xcrun", $"simctl get_app_container {udid} {bundleId} data")
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
			});
			if (p is null)
				return null;
			string stdout = p.StandardOutput.ReadToEnd().Trim();
			p.WaitForExit(3000);
			return p.ExitCode == 0 && !string.IsNullOrEmpty(stdout) ? stdout : null;
		}
		catch (Exception ex)
		{
			TestContext.Out.WriteLine($"GetAppDataContainer({udid}, {bundleId}) failed: {ex.Message}");
			return null;
		}
	}

	[OneTimeSetUp]
	public virtual void OneTimeSetUp()
	{
		// Reset fixture-scope state so each fixture starts independent of
		// the previous one's outcome.
		_priorTestFailedInFixture = false;
		_currentTestName = null;

		if (!ShareSessionAcrossTestsInFixture)
			return;

		// Shared-session fixture: build the driver once here. Per-test
		// [SetUp] then only runs the fail-cascade check + the fixture's
		// own cleanup. Tear down in [OneTimeTearDown].
		(_platform, string appPath, string appiumUrl) = ReadFixtureParameters();
		ResetAppState(_platform);
		SetUpDriver(_platform, appPath, appiumUrl);
	}

	[SetUp]
	public virtual void SetUp()
	{
		if (ShareSessionAcrossTestsInFixture)
		{
			var testName = TestContext.CurrentContext.Test.FullName;
			// Detect a "new test starting" rather than a retry of the same
			// test. NUnit's RetryCommand re-invokes SetUp/TearDown per
			// attempt with the same Test object, so retries share
			// `FullName`; only the first attempt of a new test method
			// sees a name change.
			if (testName != _currentTestName)
			{
				if (_priorTestFailedInFixture)
					Assert.Inconclusive(
						"Skipped: an earlier test in this fixture failed after all retries. " +
						"Tests share one Appium session, so post-failure state is undefined.");
				_currentTestName = testName;
			}
			// No driver creation here — the fixture's OneTimeSetUp owns
			// the driver and Quit() runs in OneTimeTearDown.
			return;
		}

		// Per-test driver lifecycle (default): full isolation. Recreates
		// the session for every test method.
		(_platform, string appPath2, string appiumUrl2) = ReadFixtureParameters();
		ResetAppState(_platform);
		SetUpDriver(_platform, appPath2, appiumUrl2);
	}

	private static (TestPlatform platform, string appPath, string appiumUrl) ReadFixtureParameters()
	{
		var platformStr = TestContext.Parameters["platform"]
			?? throw new InvalidOperationException("TestRunParameter 'platform' is required.");
		var appPath = TestContext.Parameters["appPath"]
			?? throw new InvalidOperationException("TestRunParameter 'appPath' is required.");
		var appiumUrl = TestContext.Parameters["appiumUrl"] ?? "http://localhost:4723";
		return (AppiumConfig.ParsePlatform(platformStr), appPath, appiumUrl);
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

		_driver = platform switch
		{
			TestPlatform.Android => new AndroidDriver(serverUri, options),
			TestPlatform.iOS => new IOSDriver(serverUri, options, iOSCommandTimeout),
			TestPlatform.MacCatalyst => new MacDriver(serverUri, options),
			TestPlatform.Windows => new WindowsDriver(serverUri, options),
			_ => throw new ArgumentOutOfRangeException(nameof(platform)),
		};

		IsAndroid = platform == TestPlatform.Android;
		_driver.Manage().Timeouts().ImplicitWait = DefaultImplicitWait;

		// On Windows, maximize the window so WinUI NavigationView stays in Left mode
		// (pane always visible). At ≥ExpandedModeThresholdWidth (1008 px) the pane is
		// permanently open and NavigateToXxx calls do not need to click PaneToggleButton.
		if (platform == TestPlatform.Windows)
			_driver.Manage().Window.Maximize();
	}

	[TearDown]
	public virtual void TearDown()
	{
		if (_driver is null)
			return;

		TakeScreenshot();
		var status = TestContext.CurrentContext.Result.Outcome.Status;
		if (status == TestStatus.Failed)
		{
			// Dump the accessibility tree on failure so we can diagnose "element
			// not found / not displayed" timeouts after the run.
			DumpPageSource();
			if (ShareSessionAcrossTestsInFixture)
				_priorTestFailedInFixture = true;
		}
		else if (status == TestStatus.Passed && ShareSessionAcrossTestsInFixture)
		{
			// Retried tests that eventually pass should NOT poison the cascade
			// flag for subsequent tests — clear it once a green outcome lands.
			_priorTestFailedInFixture = false;
		}
		// status == Inconclusive (this test was skipped by the cascade)
		// or Skipped (NUnit's own ignore mechanism) leaves the flag alone.

		if (!ShareSessionAcrossTestsInFixture)
		{
			try { _driver.Quit(); } catch { /* best-effort */ }
			_driver = null;
		}
	}

	[OneTimeTearDown]
	public virtual void OneTimeTearDown()
	{
		if (!ShareSessionAcrossTestsInFixture)
			return;
		try { _driver?.Quit(); } catch { /* best-effort */ }
		_driver = null;
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

	protected void DumpPageSource()
	{
		try
		{
			string source = Driver.PageSource;
			var testName = TestContext.CurrentContext.Test.FullName
				.Replace(' ', '_')
				.Replace('/', '_');
			var path = Path.Combine(
				TestContext.CurrentContext.WorkDirectory,
				$"{testName}_{DateTime.Now:yyyyMMdd_HHmmss}.pagesource.xml");
			File.WriteAllText(path, source);
			TestContext.AddTestAttachment(path, "PageSource");
		}
		catch (Exception ex)
		{
			TestContext.Out.WriteLine($"PageSource dump failed: {ex.Message}");
		}
	}
}
