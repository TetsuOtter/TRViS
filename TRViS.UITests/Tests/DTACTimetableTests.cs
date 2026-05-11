using OpenQA.Selenium;
using OpenQA.Selenium.Appium.iOS;
using OpenQA.Selenium.Appium.Mac;
using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// Tests covering the DTAC view's reaction to typical user inputs:
/// loading sample data (display count), tapping 運行開始, expanding/contracting
/// page-header parts, toggling location service, and reaching the timetable
/// scroll view (the GPS auto-scroll target).
/// </summary>
[TestFixture]
[Infrastructure.RetryAllTests(2)] // see AppLaunchTests for rationale
public class DTACTimetableTests : BaseUITest
{
	private StartHomePageObject _startHomePage = null!;

	// Shared-session mode: one Appium session for the whole fixture, no
	// app-restart between tests. Each test starts by re-asserting its
	// pre-state via in-app seams (NavigateToHome + ClearLoaderForTesting)
	// rather than relying on a fresh process — terminate+launch is
	// expensive (~3-10 s) and most tests in this fixture only need to
	// know they're on StartHome with no loader, which the seams give
	// idempotently.
	protected override bool ShareSessionAcrossTestsInFixture => true;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		_startHomePage = new StartHomePageObject(Driver);

		// If a prior test in this fixture left the app on DTAC (or
		// elsewhere via the flyout), navigate back. Then clear any
		// AppViewModel.Loader so the page renders in Start mode with
		// LoadDemoButton visible. Both calls are idempotent — they no-op
		// on the first test where the app just finished launching at
		// StartHome.
		if (!_startHomePage.PollDisplayed(AutomationIds.StartHome.Title, timeoutSeconds: 3))
		{
			new AppShellPage(Driver).NavigateToHome();
			_startHomePage = new StartHomePageObject(Driver);
		}
		_startHomePage.ClearLoaderForTesting();

		_startHomePage.AcceptPrivacyPolicyIfNeeded();
	}

	private DTACViewHostPageObject LoadSampleAndOpenDTAC()
	{
		Assert.That(_startHomePage.IsDisplayed(), Is.True);
		_startHomePage.LoadSample();
		// Sample data populates the work-group list synchronously.
		_startHomePage.WaitForElement(AutomationIds.StartHome.WorkGroupList);
		// Use the UI_TEST auto-open seam: picks first WorkGroup + first Work and
		// commits via the same code path as 開く, then navigates to DTAC. Avoids
		// flaky CollectionView row tapping on iOS while still exercising the
		// Home -> commit -> DTAC pipeline.
		return _startHomePage.AutoOpenForTesting();
	}

	/// <summary>
	/// Combined demo-data happy path: previously five separate tests, each
	/// of which paid LoadSample + AutoOpen + privacy-accept ≈ a full app
	/// restart on iOS. Merged into one flow so the heavy setup runs once;
	/// each Assert carries an explicit reason so a failure still points
	/// at the specific sub-step (TimetableScrollView visible, StartEndRun
	/// toggle, OpenClose toggle). Replaces the prior
	/// LoadSample_PopulatesWorkGroupList_DisplayCountSane /
	/// OpenDTAC_TimetableContainerVisible /
	/// Tap運行開始_TogglesStartEndRunButton /
	/// OpenCloseButton_TogglesPageHeader /
	/// TimetableScrollView_IsPresentAfterSampleLoad tests.
	/// </summary>
	[Test]
	public void DemoData_LoadOpenAndExerciseTimetable_HappyPath()
	{
		Assert.That(_startHomePage.IsDisplayed(), Is.True,
			"StartHome should be displayed at fixture entry.");

		// Phase 1: LoadSample populates WorkGroup list.
		_startHomePage.LoadSample();
		_startHomePage.WaitForElement(AutomationIds.StartHome.WorkGroupList);
		// Sample data ships with 2 WorkGroups. Use >=2 rather than ==2 so
		// platform-specific cell wrappers don't false-negative when extra
		// layout primitives expose their text.
		int count = _startHomePage.CountWorkGroups();
		Assert.That(count, Is.GreaterThanOrEqualTo(2),
			"LoadSample should produce at least 2 work-group rows.");

		// Phase 2: AutoOpenForTesting commits selection + navigates to DTAC.
		var dtac = _startHomePage.AutoOpenForTesting();
		Assert.That(dtac.IsDisplayed(), Is.True,
			"DTAC view should be displayed after AutoOpenForTesting.");

		// Phase 3: Switch to timetable tab.
		dtac.SwitchToTimetableTab();
		// VerticalTimetableView is a MAUI Grid that isn't reliably exposed
		// as an accessibility element on iOS. Assert against the
		// surrounding ScrollView, which IS exposed and is the same id
		// SwitchToTimetableTab waits for.
		Assert.That(dtac.TimetableScrollView.Displayed, Is.True,
			"TimetableScrollView should be visible after switching to the 時刻表 tab " +
			"(also the GPS auto-scroll target).");

		// Phase 4: 運行開始 toggle round-trip.
		var startEnd = dtac.StartEndRunButton;
		Assert.That(startEnd.Displayed, Is.True,
			"StartEndRunButton should be visible in the timetable tab.");

		dtac.TapStartEndRun();
		Thread.Sleep(400);
		// We can't read the IsChecked state directly across all platforms,
		// but LocationServiceButton is enabled only when CanUseLocationService
		// && IsRunning — its presence in the tree after the tap is the
		// meaningful side-effect to assert.
		Assert.That(dtac.LocationServiceButton.Displayed, Is.True,
			"LocationServiceButton should remain in the tree after toggling 運行開始 on.");

		dtac.TapStartEndRun();
		Thread.Sleep(400);
		Assert.That(dtac.StartEndRunButton.Displayed, Is.True,
			"StartEndRunButton should still be visible after toggling 運行開始 off (repeated taps OK).");

		// Phase 5: OpenClose toggle round-trip. Last sub-flow so any
		// page-header state left behind is reset by the next test's
		// app-restart.
		var openClose = dtac.OpenCloseButton;
		Assert.That(openClose.Displayed, Is.True,
			"OpenCloseButton should be visible in the timetable tab.");

		string initialText = openClose.Text ?? string.Empty;
		dtac.TapOpenClose();
		Thread.Sleep(400);
		Assert.That(dtac.OpenCloseButton.Text ?? string.Empty, Is.Not.EqualTo(initialText),
			"OpenCloseButton text should differ after the first tap (open/closed icons swap).");

		dtac.TapOpenClose();
		Thread.Sleep(400);
		Assert.That(dtac.OpenCloseButton.Text ?? string.Empty, Is.EqualTo(initialText),
			"Second tap should return OpenCloseButton to its original state.");
	}

	/// <summary>
	/// Regression for #225: when the selected train has a non-empty NextTrainId,
	/// the NextTrainButton must appear in the accessibility tree after switching
	/// to the timetable tab. The button sits at the bottom of the timetable Grid
	/// and may be off-screen on small viewports, so the helper scrolls as needed
	/// before failing.
	/// </summary>
	[Test]
	public void NextTrainButton_Present_WhenSelectedTrainHasNextTrainId()
	{
		Assert.That(_startHomePage.IsDisplayed(), Is.True);
		_startHomePage.LoadSample();
		_startHomePage.WaitForElement(AutomationIds.StartHome.WorkGroupList);

		// Cascade to a sample-data train known to have NextTrainId set:
		// linear-train-1 (NextTrainId = "linear-train-2"). The seam commits and
		// navigates to DTAC, mirroring the AutoOpenForTesting pattern.
		var dtac = _startHomePage.SeedTrainSelectionWithNextTrain();
		dtac.SwitchToTimetableTab();

		Assert.That(dtac.IsNextTrainButtonPresent(), Is.True,
			"NextTrainButton must be reachable when SelectedTrainData.NextTrainId is non-empty " +
			"(scroll-to-bottom retries are built into IsNextTrainButtonPresent).");
	}

	/// <summary>
	/// Negative: the default sample-data first train (1-1-1) has NextTrainId = "",
	/// so the button must not be visible to the user. Guards against the inverse
	/// regression where a fix accidentally always shows the button.
	///
	/// Skipped on Mac Catalyst and iOS: XCUITest / mac2 surface unparented MAUI
	/// Grid elements that have an AutomationId set in their constructor as
	/// accessibility elements with Displayed=true and a non-zero Size, regardless
	/// of whether they are in any window. The exact behaviour varies by iOS
	/// version (iPadOS 17 surfaces them while iOS 26 prunes them), so the safest
	/// thing is to skip on the entire Apple family rather than play whack-a-mole.
	/// Production code correctly removes the button from the visual tree (see
	/// VerticalTimetableView.OnViewModelNextTrainIdChanged); Android and Windows
	/// coverage is sufficient to catch the inverse regression.
	/// </summary>
	[Test]
	public void NextTrainButton_Hidden_WhenSelectedTrainHasNoNextTrainId()
	{
		if (Driver is MacDriver || Driver is IOSDriver)
			Assert.Ignore(
				"Apple's accessibility tree (XCUITest / mac2) surfaces unparented " +
				"elements with AutomationId as visible on some OS versions — we " +
				"cannot reliably distinguish \"hidden\" from \"displayed\" via Appium " +
				"here. Coverage on Android and Windows is sufficient for this assertion.");

		var dtac = LoadSampleAndOpenDTAC();
		dtac.SwitchToTimetableTab();

		Assert.That(dtac.IsNextTrainButtonPresent(TimeSpan.FromSeconds(3)), Is.False,
			"NextTrainButton must not be visible when SelectedTrainData.NextTrainId is empty.");
	}

	/// <summary>
	/// Verifies the GPS auto-scroll pipeline survives a fake GPS coord injected
	/// via the test deeplink. The deeplink handler force-enables LocationService
	/// and calls SetGpsLocation directly — no CoreLocation, no permissions.
	///
	/// Cross-platform reading of ScrollView.ScrollY isn't supported by every
	/// Appium driver, so the strict assertion is "app stays responsive and
	/// timetable remains renderable" after the GPS event; the side effect
	/// (presenter ScrollRequested firing) is exercised but not asserted here.
	/// </summary>
	[Test]
	public void GpsLocation_DeeplinkReachesLocationService()
	{
		Assert.That(_startHomePage.IsDisplayed(), Is.True);
		_startHomePage.LoadSample();
		_startHomePage.WaitForElement(AutomationIds.StartHome.WorkGroupList);

		// Push a fixture GPS coord through the DEBUG-only seed button. The button
		// force-enables LocationService and calls SetGpsLocation directly — no
		// CoreLocation, no permissions, no SendKeys.
		_startHomePage.SeedGpsLocationForTesting();
		Thread.Sleep(500);

		// Commit a selection via the auto-open seam (selecting on Home is now
		// tentative until 開く). Verifying GPS pipeline survives requires DTAC
		// to have a real Work selected.
		var dtac = _startHomePage.AutoOpenForTesting();
		Assert.That(dtac.IsDisplayed(), Is.True);
		dtac.SwitchToTimetableTab();
		Assert.That(dtac.TimetableScrollView.Displayed, Is.True,
			"Timetable container must remain renderable after the GPS event was injected.");
	}
}
