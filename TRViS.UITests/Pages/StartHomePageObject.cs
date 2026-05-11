using OpenQA.Selenium.Appium;
using TRViS.UITests.Infrastructure;

namespace TRViS.UITests.Pages;

/// <summary>
/// Page object for the unified Start/Home page (replaces the legacy SelectTrainPage).
/// In Start mode the Connect/SelectFile/LoadDemo buttons are visible; in Home mode
/// the WorkGroup/Work lists and Open/Disconnect buttons are visible. The animated
/// header toggles between "centered" (Start) and "top-anchored" (Home).
/// </summary>
public class StartHomePageObject : PageObject
{
	public StartHomePageObject(AppiumDriver driver) : base(driver) { }

	// Header — present in both modes. Use a generous timeout because the post-FirebaseSettings
	// navigation on slow simulators (iOS macos-26 iPhone) can take >30 s.
	public AppiumElement Title => WaitForElement(AutomationIds.StartHome.Title, TimeSpan.FromSeconds(60));
	public AppiumElement AppIcon => FindByAutomationId(AutomationIds.StartHome.AppIcon);

	// Start mode — primary buttons.
	// Use WaitForElement for buttons that may be temporarily not findable while a popup
	// is dismissing back to this page.
	public AppiumElement ConnectServerButton => WaitForElement(AutomationIds.StartHome.ConnectServerButton);
	public AppiumElement SelectFileButton => FindByAutomationId(AutomationIds.StartHome.SelectFileButton);
	public AppiumElement LoadDemoButton => FindByAutomationId(AutomationIds.StartHome.LoadDemoButton);

	// Start mode — privacy / footer.
	public AppiumElement PrivacyReconfirmBanner => FindByAutomationId(AutomationIds.StartHome.PrivacyReconfirmBanner);
	public AppiumElement PrivacyPolicyButton => FindByAutomationId(AutomationIds.StartHome.PrivacyPolicyButton);
	public AppiumElement ThirdPartyLicensesButton => FindByAutomationId(AutomationIds.StartHome.ThirdPartyLicensesButton);

	// Home mode. Two-step picker: each step has either a list (full picker) or a
	// chip (compact summary, shown after selection). Only one of {List, Chip} per
	// step is visible at any time; assertions should branch on which is reachable.
	public AppiumElement WorkGroupList => FindByAutomationId(AutomationIds.StartHome.WorkGroupList);
	public AppiumElement WorkGroupChip => FindByAutomationId(AutomationIds.StartHome.WorkGroupChip);
	public AppiumElement WorkList => FindByAutomationId(AutomationIds.StartHome.WorkList);
	public AppiumElement WorkChip => FindByAutomationId(AutomationIds.StartHome.WorkChip);
	public AppiumElement WorkPendingHint => FindByAutomationId(AutomationIds.StartHome.WorkPendingHint);
	public AppiumElement OpenButton => FindByAutomationId(AutomationIds.StartHome.OpenButton);
	public AppiumElement DisconnectButton => FindByAutomationId(AutomationIds.StartHome.DisconnectButton);

	/// <summary>
	/// Returns true when the WorkGroupChip is currently visible (i.e. a tentative
	/// WorkGroup has been selected). Returns false on any lookup error so callers
	/// can treat "absent" as "no tentative selection".
	/// Mirrors <see cref="IsPrivacyReconfirmBannerVisible"/>'s zero-implicit-wait pattern.
	/// </summary>
	public bool IsWorkGroupChipVisible()
		=> PollAutomationIdDisplayed(AutomationIds.StartHome.WorkGroupChip, timeoutSeconds: 1);

	/// <summary>
	/// Polls for the WorkGroupList to become visible. Use after a successful
	/// file load to absorb the Start→Home mode-switch animation
	/// (TRANSITION_MS=380ms) plus any platform-specific layout latency on slow
	/// CI runners (iOS macos-26 simulators have been observed multi-second slow).
	/// </summary>
	public bool IsWorkGroupListVisible(double timeoutSeconds = 5)
		=> PollAutomationIdDisplayed(AutomationIds.StartHome.WorkGroupList, timeoutSeconds);

	// UI_TEST seed seams.
	public AppiumElement TestSeedButton => FindByAutomationId(AutomationIds.StartHome.TestSeedButton);
	public AppiumElement TestSeedGpsButton => FindByAutomationId(AutomationIds.StartHome.TestSeedGpsButton);
	public AppiumElement TestAutoOpenButton => FindByAutomationId(AutomationIds.StartHome.TestAutoOpenButton);
	public AppiumElement TestClearHistoryButton => FindByAutomationId(AutomationIds.StartHome.TestClearHistoryButton);
	public AppiumElement TestSeedHorizontalTimetableButton => FindByAutomationId(AutomationIds.StartHome.TestSeedHorizontalTimetableButton);
	public AppiumElement TestSeedSqliteButton => FindByAutomationId(AutomationIds.StartHome.TestSeedSqliteButton);
	public AppiumElement TestClearTimetablesButton => FindByAutomationId(AutomationIds.StartHome.TestClearTimetablesButton);
	public AppiumElement TestSeedSampleFilesButton => FindByAutomationId(AutomationIds.StartHome.TestSeedSampleFilesButton);
	public AppiumElement TestClearSampleFilesButton => FindByAutomationId(AutomationIds.StartHome.TestClearSampleFilesButton);
	public AppiumElement TestSetupBrowseFallbackButton => FindByAutomationId(AutomationIds.StartHome.TestSetupBrowseFallbackButton);
	public AppiumElement TestSeedNextTrainSelectionButton => FindByAutomationId(AutomationIds.StartHome.TestSeedNextTrainSelectionButton);
	public AppiumElement TestClearLoaderButton => FindByAutomationId(AutomationIds.StartHome.TestClearLoaderButton);

	public bool IsDisplayed()
	{
		try
		{
			return Title.Displayed;
		}
		catch (OpenQA.Selenium.NoSuchElementException)
		{
			return false;
		}
	}

	/// <summary>
	/// Returns true when the privacy-policy reconfirm banner is currently visible
	/// (i.e. the user has not yet accepted the current PRIVACY_POLICY_REVISION).
	/// Polls briefly so a slow first-appearance OnAppearing doesn't lose the race
	/// against the test's first probe. Returns false on timeout / any error so
	/// callers can treat "absent" as "accepted".
	/// </summary>
	public bool IsPrivacyReconfirmBannerVisible()
		=> PollAutomationIdDisplayed(AutomationIds.StartHome.PrivacyReconfirmBanner);

	/// <summary>
	/// Wait up to <paramref name="timeoutSeconds"/> for an element to be findable
	/// AND Displayed=true. Returns false on timeout or any error.
	/// </summary>
	private bool PollAutomationIdDisplayed(string automationId, double timeoutSeconds = 5)
	{
		var prevWait = TimeSpan.FromSeconds(10);
		var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
		try
		{
			Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
			while (DateTime.UtcNow < deadline)
			{
				try
				{
					if (FindByAutomationId(automationId).Displayed)
						return true;
				}
				catch { }
				Thread.Sleep(100);
			}
			return false;
		}
		finally
		{
			Driver.Manage().Timeouts().ImplicitWait = prevWait;
		}
	}

	/// <summary>
	/// Opens the privacy dialog and taps Save so all "feature" buttons
	/// (Connect, SelectFile, LoadDemo) become enabled. Idempotent: re-saving
	/// when privacy is already accepted just re-writes the same values.
	///
	/// Fast-paths on iOS/Mac/Android when the privacy reconfirm banner is
	/// not on screen — in shared-session mode the privacy-accepted flag
	/// persists in NSUserDefaults/SharedPreferences across app restarts,
	/// so re-running the click+save dance on every test costs 3-5 s each
	/// for no behavioural change. Windows MAUI does not expose Border
	/// elements via UIA so the banner-visible probe always returns false
	/// there; on Windows we keep the unconditional flow, which is what
	/// the original implementation always did.
	/// </summary>
	public void AcceptPrivacyPolicyIfNeeded()
	{
		// Fast-path: if the reconfirm banner is not visible on a
		// banner-probe-supporting platform, privacy has already been
		// accepted in this app installation and the click+save dance is
		// just dead weight. IsPrivacyReconfirmBannerVisible reliably
		// returns false on Windows (no UIA peer), so gating on it would
		// strand Windows tests with feature buttons disabled; keep the
		// platform-aware split.
		if (!IsWindows && !IsPrivacyReconfirmBannerVisible())
			return;

		// Use explicit client-side WaitForElement instead of FindByAutomationId.
		// The mac2 driver runs each XCUIElement lookup as a single query and
		// does not honor the Selenium implicit wait the way XCUITest does, so
		// hitting this button immediately after a fresh app launch returns
		// 404 in ~1 s instead of polling for the 10 s implicit wait — and
		// AcceptPrivacyPolicyIfNeeded is the very first call after each
		// SetUp's freshly-created mac session. WaitForElement polls
		// client-side, which works on every driver.
		WaitForElement(AutomationIds.StartHome.PrivacyPolicyButton, TimeSpan.FromSeconds(30)).Click();
		// Wait for the dialog's Save button (acts as ready-signal that the modal is up).
		// Use a 60 s budget for the same reason Title uses 60 s — modal-push +
		// markdown render + first-paint can exceed 30 s on a constrained CI emulator.
		var save = WaitForElement(AutomationIds.PrivacyDialog.SaveButton, TimeSpan.FromSeconds(60));
		save.Click();

		// Accept any "Success!" alert dialog the save handler raises (DISABLE_FIREBASE
		// builds skip it, so missing-alert is fine).
		try
		{
			Driver.SwitchTo().Alert().Accept();
		}
		catch (OpenQA.Selenium.NoAlertPresentException) { }
		catch
		{
			try
			{
				Driver.FindElement(OpenQA.Selenium.By.XPath(
					"//XCUIElementTypeSheet//XCUIElementTypeButton[@label='OK']" +
					" | //XCUIElementTypeAlert//XCUIElementTypeButton[@label='OK']"
				)).Click();
			}
			catch { /* no alert/sheet — DISABLE_FIREBASE build */ }
		}

		// Wait for the dialog to dismiss back to the Start page.
		_ = Title;
		Thread.Sleep(300);
	}

	/// <summary>
	/// Loads the demo (sample) data set. After load, the page transitions to Home mode
	/// and the WorkGroup/Work lists become visible.
	/// </summary>
	public void LoadSample() => LoadDemoButton.Click();

	/// <summary>
	/// Taps "Connect to Server" and returns the dialog's page object.
	/// </summary>
	public ConnectServerDialogPageObject OpenConnectServerDialog()
	{
		ConnectServerButton.Click();
		return new ConnectServerDialogPageObject(Driver);
	}

	/// <summary>
	/// Taps "ファイルを選択" and returns the dialog's page object.
	/// </summary>
	public SelectFileDialogPageObject OpenSelectFileDialog()
	{
		SelectFileButton.Click();
		return new SelectFileDialogPageObject(Driver);
	}

	/// <summary>
	/// Taps the UI_TEST-only test-seed button so tests can populate URL history
	/// without typing through Appium SendKeys (flaky on iOS XCUITest).
	/// </summary>
	public void SeedUrlHistoryForTesting() => TestSeedButton.Click();

	/// <summary>
	/// Taps the UI_TEST-only history-clear button. Use before any test whose
	/// expectation depends on an empty URL history list — on iOS the noReset:true
	/// session option means simctl-level preference deletion isn't always enough
	/// to clear AppViewModel's in-memory list before the test starts.
	/// </summary>
	public void ClearUrlHistoryForTesting() => TestClearHistoryButton.Click();

	/// <summary>
	/// Taps the UI_TEST-only SQLite seed button. Writes a minimal SQLite fixture
	/// (single WorkGroup row) into TimetableFileDirectory using the same
	/// sqlite-net code path LoaderSQL reads from. Tests use this to verify the
	/// MAUI runtime can actually open SQLite — catches regressions where the
	/// SQLitePCLRaw bundle_green provider registration is stripped by the
	/// linker / not initialized at app start, which the netcore-only
	/// TRViS.IO.Tests cannot detect.
	/// </summary>
	public void SeedSqliteForTesting() => TestSeedSqliteButton.Click();

	/// <summary>
	/// Taps the UI_TEST-only timetables-clear button. Removes everything in
	/// TimetableFileDirectory so SelectFile-related tests can guarantee a known
	/// starting state without relying on platform-specific app-data wipe
	/// (Mac Catalyst / iOS keep the documents folder across noReset:true
	/// sessions).
	/// </summary>
	public void ClearTimetablesForTesting() => TestClearTimetablesButton.Click();

	/// <summary>
	/// Taps the UI_TEST-only loader-clear button. Sets AppViewModel.Loader=null
	/// + disposes the previous loader, returning StartHomePage to Start mode
	/// (LoadDemoButton visible, work-group list hidden). Use this between
	/// tests in a fixture that shares one Appium session, so each test starts
	/// from "no loader" regardless of where the previous test left things.
	/// </summary>
	public void ClearLoaderForTesting() => TestClearLoaderButton.Click();

	/// <summary>
	/// Filename written by <see cref="SeedSqliteForTesting"/>. Mirrors the
	/// constant in StartHomePage.xaml.cs so tests can look up the rendered
	/// card by AutomationId.
	/// </summary>
	public const string UITestSqliteFixtureFileName = "uitest_seed.sqlite";

	/// <summary>
	/// Taps the UI_TEST-only GPS-seed button so tests can push a fake GPS coord
	/// without typing through Appium SendKeys.
	/// </summary>
	public void SeedGpsLocationForTesting() => TestSeedGpsButton.Click();

	/// <summary>
	/// Taps the UI_TEST-only auto-open button. Picks the first WorkGroup +
	/// first Work and commits via the same code path as 開く, then navigates
	/// to DTAC. Lets DTAC tests bypass the picker UI.
	/// </summary>
	public DTACViewHostPageObject AutoOpenForTesting()
	{
		TestAutoOpenButton.Click();
		return new DTACViewHostPageObject(Driver);
	}

	/// <summary>
	/// UI_TEST-only seam that picks first WorkGroup/Work, swaps the Work record
	/// for a clone with HasETrainTimetable=true and a 1×1 transparent PNG
	/// payload, then navigates to DTAC. Lets the horizontal-timetable tests
	/// exercise the visible-button + page-navigation paths without doctoring
	/// the sample data fixture on disk.
	/// </summary>
	public DTACViewHostPageObject SeedHorizontalTimetableAndOpenForTesting()
	{
		TestSeedHorizontalTimetableButton.Click();
		return new DTACViewHostPageObject(Driver);
	}

	/// <summary>
	/// Taps the UI_TEST-only seed button that commits selection to
	/// <c>linear-train-1</c> (NextTrainId = <c>linear-train-2</c>) and navigates
	/// to DTAC. Used by the #225 NextTrainButton regression test so it doesn't
	/// rely on the default first-train selection (whose NextTrainId is empty).
	/// </summary>
	public DTACViewHostPageObject SeedTrainSelectionWithNextTrain()
	{
		TestSeedNextTrainSelectionButton.Click();
		return new DTACViewHostPageObject(Driver);
	}

	/// <summary>
	/// URLs that <see cref="SeedUrlHistoryForTesting"/> writes into history.
	/// Mirrors the literals in StartHomePage.xaml.cs so tests can assert against them.
	/// </summary>
	public static readonly string[] SeededHistoryUrls =
	{
		"https://example.com/timetable-a.json",
		"https://example.com/timetable-b.json",
	};

	/// <summary>
	/// Fixture file names written by <see cref="SeedSampleFilesForTesting"/>.
	/// Mirrors the literals in <c>SelectFileDialogTestSeams</c> so tests can
	/// assert against per-row card AutomationIds without duplicating strings.
	/// </summary>
	public const string SeededRootFileName = "ui-test-root.json";
	public const string SeededSubFolderName = "ui-test-folder";
	public const string SeededNestedFileName = "ui-test-nested.json";

	/// <summary>
	/// Taps the UI_TEST-only seed-sample-files button. Writes a known fixture
	/// (root JSON + sub-folder containing another JSON) into TimetableFileDirectory
	/// so drill-down / file-load tests have something to assert against.
	/// </summary>
	public void SeedSampleFilesForTesting() => TestSeedSampleFilesButton.Click();

	/// <summary>
	/// Taps the UI_TEST-only clear-sample-files button. Wipes
	/// TimetableFileDirectory, clears any pending FilePicker override, and
	/// resets the in-memory <c>AppViewModel.Loader</c> so the page flips
	/// back to Start mode if a previous test (or app-launch auto-loader)
	/// left it in Home mode. Use in SetUp because iOS noReset:true and Mac
	/// Catalyst's app sandbox both keep the documents folder warm across
	/// sessions and the FilePicker override static survives Driver.Quit().
	///
	/// Sleeps briefly after the click to let StartHomePage's mode-switch
	/// observer + animation (TRANSITION_MS ≈ 380ms) settle before the
	/// caller queries Start-mode buttons.
	/// </summary>
	public void ClearSampleFilesForTesting()
	{
		TestClearSampleFilesButton.Click();
		Thread.Sleep(500);
	}

	/// <summary>
	/// Taps the UI_TEST-only setup-browse-fallback button. Writes a JSON fixture
	/// outside TimetableFileDirectory and installs a FilePicker override that
	/// returns its path. The next "他の場所からファイルを開く" tap then runs the
	/// real load path with no OS picker dialog.
	/// </summary>
	public void SetupBrowseFallbackForTesting() => TestSetupBrowseFallbackButton.Click();

	/// <summary>
	/// Returns the count of WorkGroup rows after a sample load. Useful to verify
	/// "表示件数" against the known sample-data fixture.
	/// </summary>
	public int CountWorkGroups()
	{
		var list = WaitForElement(AutomationIds.StartHome.WorkGroupList);
		var children = list.FindElements(OpenQA.Selenium.By.XPath(".//*"));
		// Filter to elements whose text is non-empty so we don't double-count
		// child layout primitives (Grid/StackLayout) on platforms whose
		// accessibility tree exposes inner cells.
		int count = 0;
		foreach (var el in children)
		{
			try
			{
				if (!string.IsNullOrEmpty(el.Text))
					count++;
			}
			catch { /* element went stale or text unavailable */ }
		}
		return count;
	}
}
