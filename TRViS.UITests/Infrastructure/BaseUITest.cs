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
	/// Opt-in: when overridden to true, the fixture uses one Appium session
	/// for the whole fixture instead of one session per test.
	/// [OneTimeSetUp] creates the driver; [OneTimeTearDown] quits it.
	/// Per-test cleanup is handled by terminating + relaunching the app
	/// via Appium-platform-specific commands (mobile: terminateApp /
	/// launchApp on iOS/Android/Mac; windows: closeApp / launchApp on
	/// Windows), which resets in-app singletons / view-model state without
	/// paying a full session-attach again. Tests that fail after all
	/// retries "cascade" — the next test in the fixture is reported
	/// Inconclusive rather than run against undefined post-failure state.
	/// </summary>
	protected virtual bool ShareSessionAcrossTestsInFixture => false;

	// Honor session sharing on every platform. Per-platform app-restart
	// strategies are dispatched inside RestartAppInSharedSession. Windows
	// additionally falls back to creating a fresh session if its driver
	// cannot re-launch the closed app within the existing session.
	private bool EffectiveSharedSession
		=> ShareSessionAcrossTestsInFixture;

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

	/// <summary>
	/// Tracks whether AcceptPrivacyPolicyIfNeeded has already run
	/// successfully within the CURRENT Appium session. Reset in
	/// OneTimeSetUp when a fresh session is created (i.e. once per
	/// fixture in shared-session mode, or once per test in per-test
	/// mode). Lets fixture [SetUp]s call AcceptPrivacyPolicyIfNeeded
	/// idempotently — the second-and-later calls skip the click+save
	/// dance on every platform, including Windows where the
	/// banner-visible probe can't gate on banner state.
	/// </summary>
	internal static bool PrivacyAcceptedInCurrentSession = false;

	/// <summary>
	/// True when an assembly-level <see cref="AssemblyUITestSetUp"/> has
	/// created the Appium session and owns its lifecycle. In that case
	/// per-fixture OneTimeSetUp / OneTimeTearDown skip ResetAppState +
	/// SetUpDriver + Driver.Quit and reuse the long-lived driver across
	/// every fixture in the assembly — the app starts once for the
	/// whole suite run and is quit once at the end. Each fixture's
	/// [SetUp] recovers app state via in-app test seams
	/// (NavigateToHome, ClearLoaderForTesting, ClearSampleFilesForTesting,
	/// dialog Close, …) rather than relying on a fresh process.
	/// </summary>
	internal static bool GlobalSessionActive = false;

	private static readonly TimeSpan DefaultImplicitWait = TimeSpan.FromSeconds(10);

	/// <summary>
	/// Resets per-test app state so every test begins from a clean slate
	/// (e.g. Firebase consent page visible on Mac/iOS).
	/// </summary>
	private static void ResetAppState(TestPlatform platform)
	{
		switch (platform)
		{
			case TestPlatform.Android:
				// Appium's UiAutomator2 reinstalls the APK on session creation,
				// which resets app data automatically.
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

	[OneTimeSetUp]
	public virtual void OneTimeSetUp()
	{
		// Reset fixture-scope cascade state so each fixture's pass/fail
		// chain is independent of the previous fixture's outcome.
		_priorTestFailedInFixture = false;
		_currentTestName = null;

		// Read platform first so EffectiveSharedSession / GlobalSessionActive
		// branches can be evaluated; the params are also needed when we go
		// on to set up the driver.
		(_platform, string appPath, string appiumUrl) = ReadFixtureParameters();

		// Assembly-level [SetUpFixture] already created the driver and ran
		// ResetAppState exactly once at the start of the suite. Subsequent
		// fixtures reuse that driver — no per-fixture ResetAppState /
		// SetUpDriver / privacy-flag reset, because each of those would
		// kill the app or invalidate persisted state we now intentionally
		// carry across fixtures. Per-fixture [SetUp]s recover via in-app
		// seams.
		if (GlobalSessionActive)
			return;

		// PrivacyAcceptedInCurrentSession is reset because a fresh Appium
		// session means fresh NSUserDefaults / SharedPreferences
		// (ResetAppState wipes them just before SetUpDriver), so the
		// privacy banner is visible again and the flag must reflect that.
		PrivacyAcceptedInCurrentSession = false;

		if (!EffectiveSharedSession)
			return;

		// Shared-session fixture (no assembly setup): build the driver
		// once here. [SetUp] returns early for the rest of the fixture so
		// tests share this session. Driver.Quit lives in [OneTimeTearDown].
		ResetAppState(_platform);
		SetUpDriver(_platform, appPath, appiumUrl);
	}

	[SetUp]
	public virtual void SetUp()
	{
		if (GlobalSessionActive || EffectiveSharedSession)
		{
			var testName = TestContext.CurrentContext.Test.FullName;
			// Detect a "new test starting" rather than a retry of the same
			// test. NUnit's RetryCommand re-invokes SetUp/TearDown per
			// attempt with the same Test object, so retries share
			// `FullName`; only the first attempt of a new test method
			// sees a name change. The cascade-skip only kicks in when a
			// genuinely-new test starts after a prior failure — retries
			// must continue against the same in-app state to do anything
			// useful.
			if (testName != _currentTestName)
			{
				if (_priorTestFailedInFixture)
					Assert.Inconclusive(
						"Skipped: an earlier test in this fixture failed after all retries. " +
						"Tests share one Appium session, so post-failure state is undefined.");
				_currentTestName = testName;
			}
			// No per-test app-restart and no Driver.Quit. Tests within a
			// fixture run against the same continuously-running app
			// instance; each test owns its own pre-state via in-app
			// UI_TEST seams (ClearLoaderForTesting,
			// ClearUrlHistoryForTesting, ClearSampleFilesForTesting,
			// etc.) or via re-navigating to a known page in its own
			// [SetUp] override. This avoids the ~3-10 s per-test
			// terminate+launch cost where one wasn't actually needed —
			// e.g. consecutive flyout navigations or open/close cycles
			// of the same dialog don't need a clean app to be meaningful.
			return;
		}

		// Per-test driver lifecycle (default): full isolation. Recreates
		// the session for every test method. ResetAppState wipes
		// preferences, so the privacy-accept flag is fresh again.
		PrivacyAcceptedInCurrentSession = false;
		(_platform, string appPath2, string appiumUrl2) = ReadFixtureParameters();
		ResetAppState(_platform);
		SetUpDriver(_platform, appPath2, appiumUrl2);
	}

	/// <summary>
	/// Terminates the app via Appium, wipes the data directories that
	/// could trigger auto-load on the next launch, and relaunches the app
	/// — all without quitting the Appium session. Only valid on iOS.
	///
	/// Leaves Library/Preferences alone so the privacy-policy-accepted
	/// flag survives (otherwise every test would have to re-dismiss the
	/// banner). Tests that rely on cleared preferences should use the
	/// in-app UI_TEST seams (ClearHistoryForTesting etc.) instead.
	/// </summary>
	private void RestartAppInSharedSession()
	{
		switch (_platform)
		{
			case TestPlatform.Android:
				RestartAppAndroid();
				break;
			case TestPlatform.Windows:
				RestartAppWindows();
				break;
		}
	}

	private void RestartAppAndroid()
	{
		// Terminate via Appium.
		TryExecuteScript("mobile: terminateApp",
			new Dictionary<string, object> { { "appId", AppPackage } });

		// Clear app data via Appium's adb wrapper — this resets
		// SharedPreferences too, so a re-accept of privacy is needed (no
		// platform-equivalent of "keep prefs across restart" because the
		// per-test driver-creation path normally reinstalls the APK,
		// which wipes everything; mirror that here for behavioural
		// equivalence).
		TryExecuteScript("mobile: clearApp",
			new Dictionary<string, object> { { "appId", AppPackage } });

		// Activate (launch the cleared app via its launcher activity).
		Driver.ExecuteScript("mobile: activateApp",
			new Dictionary<string, object> { { "appId", AppPackage } });
	}

	private void RestartAppWindows()
	{
		// windows-driver's session lifecycle is tightly coupled to the
		// originally-launched app. Try the documented close+launch surface
		// first; if either fails, fall back to recreating the Appium
		// session entirely (slower but reliable). The session-create retry
		// added separately to SetUpDriver covers the rapid-recreate flake
		// on windows-driver.
		bool restartOk = false;
		try
		{
			TryExecuteScript("windows: closeApp", new Dictionary<string, object>());
			// Wipe LocalAppData same as ResetAppState's Windows path.
			var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			foreach (var folderName in new[] { AppPackage, "TRViS" })
			{
				var path = Path.Combine(localAppData, folderName);
				try
				{
					if (Directory.Exists(path))
						Directory.Delete(path, recursive: true);
				}
				catch { /* best-effort */ }
			}
			Driver.ExecuteScript("windows: launchApp", new Dictionary<string, object>());
			restartOk = true;
		}
		catch (Exception ex)
		{
			TestContext.Out.WriteLine(
				$"RestartAppInSharedSession (Windows): closeApp/launchApp failed ({ex.GetType().Name}: {ex.Message}). " +
				$"Falling back to fresh Appium session.");
		}

		if (!restartOk)
		{
			// Tear down and recreate the session. The per-test
			// ResetAppState handles pkill + LocalAppData wipe; SetUpDriver
			// retries on transient SocketException.
			try { _driver?.Quit(); } catch { /* best-effort */ }
			_driver = null;
			ResetAppState(_platform);
			var appPath = TestContext.Parameters["appPath"]
				?? throw new InvalidOperationException("TestRunParameter 'appPath' is required.");
			var appiumUrl = TestContext.Parameters["appiumUrl"] ?? "http://localhost:4723";
			SetUpDriver(_platform, appPath, appiumUrl);
		}
	}

	private void TryExecuteScript(string script, Dictionary<string, object> args)
	{
		try
		{
			Driver.ExecuteScript(script, args);
		}
		catch (Exception ex)
		{
			TestContext.Out.WriteLine($"RestartAppInSharedSession: '{script}' threw {ex.GetType().Name}: {ex.Message}");
		}
	}

	private static void WipeUserContentsUnder(string containerDir)
	{
		// Wipe every TRViS.UserContents folder under the data container so a
		// JSON file seeded by a previous test (SelectFileDialogTests etc.)
		// doesn't trigger DefaultTimetableFileLoader's single-file
		// auto-load on the next launch — that would put the app in Home
		// mode and break Start-mode tests.
		try
		{
			foreach (string dir in Directory.EnumerateDirectories(
				containerDir, "TRViS.UserContents", SearchOption.AllDirectories))
			{
				try
				{
					Directory.Delete(dir, recursive: true);
					TestContext.Out.WriteLine($"RestartAppInSharedSession: cleared {dir}");
				}
				catch (Exception ex)
				{
					TestContext.Out.WriteLine($"RestartAppInSharedSession: failed to clear {dir}: {ex.Message}");
				}
			}
		}
		catch (Exception ex)
		{
			TestContext.Out.WriteLine($"RestartAppInSharedSession: enumerate {containerDir} failed: {ex.Message}");
		}
	}

	/// <summary>
	/// Entry point for assembly-level <see cref="AssemblyUITestSetUp"/>: do
	/// the one-time ResetAppState + driver creation for the whole suite
	/// and flip <see cref="GlobalSessionActive"/> so subsequent fixture
	/// lifecycle hooks reuse this driver instead of recreating it.
	/// </summary>
	internal static void InitGlobalSession()
	{
		(_platform, string appPath, string appiumUrl) = ReadFixtureParameters();
		PrivacyAcceptedInCurrentSession = false;
		ResetAppState(_platform);
		// SetUpDriver assigns _driver; cast back through the public API so
		// we go through the same retry path as per-fixture setup.
		new BootstrapDriver().BuildDriver(_platform, appPath, appiumUrl);
		GlobalSessionActive = true;
	}

	/// <summary>
	/// Entry point for assembly-level <see cref="AssemblyUITestSetUp"/>: tear
	/// down the suite-wide Appium session. Safe to call when the session
	/// was never created (e.g. if BeforeAnyTests failed before driver
	/// assignment) — Quit is a no-op on null.
	/// </summary>
	internal static void QuitGlobalSession()
	{
		try { _driver?.Quit(); } catch { /* best-effort */ }
		_driver = null;
		GlobalSessionActive = false;
	}

	/// <summary>
	/// Concrete shim that lets <see cref="AssemblyUITestSetUp"/> reach
	/// SetUpDriver (which lives on <see cref="BaseUITest"/> but is
	/// `protected virtual`). Instantiates a throw-away subclass so the
	/// access modifier is satisfied; the resulting driver is held in the
	/// static <c>_driver</c> field shared by all instances.
	/// </summary>
	private sealed class BootstrapDriver : BaseUITest
	{
		internal void BuildDriver(TestPlatform platform, string appPath, string appiumUrl)
			=> SetUpDriver(platform, appPath, appiumUrl);
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

		// On Windows the appium-windows-driver has been observed to abort the
		// HTTP connection on the second-or-later session creation in a single
		// run (CI run 25678232517 / PR #243: first session succeeded, second
		// session failed with "An existing connection was forcibly closed by
		// the remote host" and every subsequent attempt got "actively
		// refused"). Surface as SocketException through the .NET client.
		// Retry the session-create on those transient socket errors with a
		// short backoff so the run does not need a manual rerun. Other
		// platforms keep the default single-attempt behaviour because their
		// drivers haven't exhibited this failure mode.
		int maxAttempts = (platform == TestPlatform.Windows) ? 3 : 1;
		Exception? lastEx = null;
		for (int attempt = 1; attempt <= maxAttempts; attempt++)
		{
			try
			{
				_driver = platform switch
				{
					TestPlatform.Android => new AndroidDriver(serverUri, options),
					TestPlatform.Windows => new WindowsDriver(serverUri, options),
					_ => throw new ArgumentOutOfRangeException(nameof(platform)),
				};
				lastEx = null;
				break;
			}
			catch (Exception ex) when (attempt < maxAttempts && IsTransientSessionCreateError(ex))
			{
				lastEx = ex;
				TestContext.Out.WriteLine(
					$"SetUpDriver: transient session-create error on attempt {attempt}/{maxAttempts}: {ex.GetType().Name}: {ex.Message}. Retrying after backoff.");
				Thread.Sleep(TimeSpan.FromSeconds(3 * attempt));
			}
		}
		if (_driver is null)
			throw lastEx ?? new InvalidOperationException("SetUpDriver failed to create a session");

		IsAndroid = platform == TestPlatform.Android;
		_driver.Manage().Timeouts().ImplicitWait = DefaultImplicitWait;

		// On Windows, maximize the window so WinUI NavigationView stays in Left mode
		// (pane always visible). At ≥ExpandedModeThresholdWidth (1008 px) the pane is
		// permanently open and NavigateToXxx calls do not need to click PaneToggleButton.
		if (platform == TestPlatform.Windows)
			_driver.Manage().Window.Maximize();
	}

	private static bool IsTransientSessionCreateError(Exception ex)
	{
		// Walk the inner-exception chain; the .NET HTTP stack reports
		// "connection forcibly closed" as a SocketException wrapped in
		// IOException + HttpRequestException + WebDriverException.
		for (var cur = ex; cur is not null; cur = cur.InnerException)
		{
			if (cur is System.Net.Sockets.SocketException)
				return true;
			if (cur is System.IO.IOException && cur.Message.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase))
				return true;
		}
		return false;
	}

	[TearDown]
	public virtual void TearDown()
	{
		if (_driver is null)
			return;

		TakeScreenshot();
		var status = TestContext.CurrentContext.Result.Outcome.Status;
		bool sharedLifecycle = GlobalSessionActive || EffectiveSharedSession;
		if (status == TestStatus.Failed)
		{
			// Dump the accessibility tree on failure so we can diagnose "element
			// not found / not displayed" timeouts after the run.
			DumpPageSource();
			if (sharedLifecycle)
				_priorTestFailedInFixture = true;
		}
		else if (status == TestStatus.Passed && sharedLifecycle)
		{
			// Retried tests that eventually pass should NOT poison the cascade
			// flag for subsequent tests — clear it once a green outcome lands.
			_priorTestFailedInFixture = false;
		}
		// status == Inconclusive (this test was skipped by the cascade)
		// or Skipped (NUnit's own ignore mechanism) leaves the flag alone.

		if (!sharedLifecycle)
		{
			try { _driver.Quit(); } catch { /* best-effort */ }
			_driver = null;
		}
	}

	[OneTimeTearDown]
	public virtual void OneTimeTearDown()
	{
		// When the assembly setup owns the driver, fixture teardown is a
		// no-op — the driver outlives this fixture.
		if (GlobalSessionActive)
			return;
		if (!EffectiveSharedSession)
			return;
		try { _driver?.Quit(); } catch { /* best-effort */ }
		_driver = null;
	}

	// Parameterized test FullName (e.g. CaptureAndDiffAllScreens("light","en"))
	// contains characters GitHub Actions artifact upload rejects (" : < > | * ?
	// CR LF). Allowlist to [A-Za-z0-9._-]; everything else -> '_'. Replacing
	// only space and slash (the old behaviour) left the double-quote and broke
	// the "Upload test results" step on PR #281.
	static string SanitizeForFileName(string name)
	{
		var chars = name.ToCharArray();
		for (int i = 0; i < chars.Length; i++)
		{
			char c = chars[i];
			bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')
				|| (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-';
			if (!ok)
				chars[i] = '_';
		}
		return new string(chars);
	}

	protected void TakeScreenshot()
	{
		try
		{
			var screenshot = Driver.GetScreenshot();
			var testName = SanitizeForFileName(TestContext.CurrentContext.Test.FullName);
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
			var testName = SanitizeForFileName(TestContext.CurrentContext.Test.FullName);
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
