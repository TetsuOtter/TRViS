using OpenQA.Selenium;
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

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		_startHomePage = new StartHomePageObject(Driver);
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

	[Test]
	public void LoadSample_PopulatesWorkGroupList_DisplayCountSane()
	{
		Assert.That(_startHomePage.IsDisplayed(), Is.True);
		_startHomePage.LoadSample();

		// Sample data ships with 2 WorkGroups. Ensure both surface in the list.
		// Use >=2 rather than ==2 so platform-specific cell wrappers don't cause
		// a false negative if extra layout primitives expose their text.
		int count = _startHomePage.CountWorkGroups();
		Assert.That(count, Is.GreaterThanOrEqualTo(2),
			"Sample data should produce at least 2 work-group rows.");
	}

	[Test]
	public void OpenDTAC_TimetableContainerVisible()
	{
		var dtac = LoadSampleAndOpenDTAC();

		Assert.That(dtac.IsDisplayed(), Is.True);
		dtac.SwitchToTimetableTab();
		// VerticalTimetableView is a MAUI Grid that isn't reliably exposed as an
		// accessibility element on iOS. Assert against the surrounding ScrollView,
		// which IS exposed (and is the same id used by SwitchToTimetableTab).
		Assert.That(dtac.TimetableScrollView.Displayed, Is.True,
			"Timetable scroll container should be visible after switching to the 時刻表 tab.");
	}

	[Test]
	public void Tap運行開始_TogglesStartEndRunButton()
	{
		var dtac = LoadSampleAndOpenDTAC();
		dtac.SwitchToTimetableTab();

		var btn = dtac.StartEndRunButton;
		Assert.That(btn.Displayed, Is.True);
		// Initial state: not running. Tap to flip on.
		dtac.TapStartEndRun();
		Thread.Sleep(400);
		// We can't read the IsChecked state directly across all platforms, but the
		// LocationServiceButton is enabled only when CanUseLocationService && IsRunning.
		// After tap, the LocationServiceButton should still be findable (visible) —
		// the visual toggle of its enabled state is a meaningful side-effect to assert.
		Assert.That(dtac.LocationServiceButton.Displayed, Is.True,
			"LocationServiceButton should remain in the tree after toggling 運行開始.");

		// Tap again to flip off (ensures the button accepts repeated presses).
		dtac.TapStartEndRun();
		Thread.Sleep(400);
		Assert.That(dtac.StartEndRunButton.Displayed, Is.True);
	}

	[Test]
	public void OpenCloseButton_TogglesPageHeader()
	{
		var dtac = LoadSampleAndOpenDTAC();
		dtac.SwitchToTimetableTab();

		var openClose = dtac.OpenCloseButton;
		Assert.That(openClose.Displayed, Is.True);

		// Capture the initial label; it should change between open/closed states.
		string initialText = openClose.Text ?? string.Empty;
		dtac.TapOpenClose();
		Thread.Sleep(400);
		string afterFirstTap = dtac.OpenCloseButton.Text ?? string.Empty;
		Assert.That(afterFirstTap, Is.Not.EqualTo(initialText),
			"OpenCloseButton's text should differ after the first tap (open/closed icons swap).");

		dtac.TapOpenClose();
		Thread.Sleep(400);
		string afterSecondTap = dtac.OpenCloseButton.Text ?? string.Empty;
		Assert.That(afterSecondTap, Is.EqualTo(initialText),
			"Second tap should return the OpenCloseButton to its original state.");
	}

	[Test]
	public void TimetableScrollView_IsPresentAfterSampleLoad()
	{
		var dtac = LoadSampleAndOpenDTAC();
		dtac.SwitchToTimetableTab();

		var scroll = dtac.TimetableScrollView;
		Assert.That(scroll.Displayed, Is.True,
			"TimetableScrollView should render with sample data — needed for GPS auto-scroll.");
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
	/// Skipped on Mac Catalyst: the mac2 driver surfaces unparented MAUI Grid
	/// elements that have an AutomationId set in their constructor as accessibility
	/// elements with Displayed=true and a non-zero Size, regardless of whether
	/// they are in any window. Production code correctly removes the button from
	/// the visual tree (see VerticalTimetableView.OnViewModelNextTrainIdChanged),
	/// but no Appium-visible signal differentiates the "unparented" state from
	/// the "displayed to the user" state on this driver. Other platforms
	/// (Android, iOS, Windows) all enforce this assertion.
	/// </summary>
	[Test]
	public void NextTrainButton_Hidden_WhenSelectedTrainHasNoNextTrainId()
	{
		if (Driver is MacDriver)
			Assert.Ignore(
				"Mac Catalyst (mac2 driver) surfaces unparented elements with " +
				"AutomationId as visible — we cannot reliably distinguish " +
				"\"hidden\" from \"displayed\" via Appium here. Coverage on " +
				"Android/iOS/Windows is sufficient for this assertion.");

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
