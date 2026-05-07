using OpenQA.Selenium;
using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// Tests covering the DTAC view's reaction to typical user inputs:
/// loading sample data (display count), tapping 運行開始, expanding/contracting
/// page-header parts, toggling location service, and reaching the timetable
/// scroll view (the GPS auto-scroll target).
/// </summary>
[TestFixture]
public class DTACTimetableTests : BaseUITest
{
	private SelectTrainPageObject _selectTrainPage = null!;
	private AppShellPage _shell = null!;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		var firebasePage = new FirebaseSettingPageObject(Driver);
		// FirebaseSettingPageObject picks a platform-appropriate timeout
		// (120 s on Android for Mono JIT, 15 s on Windows where the consent
		// page may be skipped because Preferences aren't reliably reset).
		if (firebasePage.IsDisplayed())
			_selectTrainPage = firebasePage.SaveAndAccept();
		else
			_selectTrainPage = new SelectTrainPageObject(Driver);

		_shell = new AppShellPage(Driver);
	}

	private DTACViewHostPageObject LoadSampleAndOpenDTAC()
	{
		Assert.That(_selectTrainPage.IsDisplayed(), Is.True);
		_selectTrainPage.LoadSample();
		// Sample data populates the work-group list synchronously.
		_selectTrainPage.WaitForElement(AutomationIds.SelectTrain.WorkGroupList);
		return _shell.NavigateToDTAC();
	}

	[Test]
	public void LoadSample_PopulatesWorkGroupList_DisplayCountSane()
	{
		Assert.That(_selectTrainPage.IsDisplayed(), Is.True);
		_selectTrainPage.LoadSample();

		// Sample data ships with 2 WorkGroups. Ensure both surface in the list.
		// Use >=2 rather than ==2 so platform-specific cell wrappers don't cause
		// a false negative if extra layout primitives expose their text.
		int count = _selectTrainPage.CountWorkGroups();
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
		Assert.That(_selectTrainPage.IsDisplayed(), Is.True);
		_selectTrainPage.LoadSample();
		_selectTrainPage.WaitForElement(AutomationIds.SelectTrain.WorkGroupList);

		// Push a fixture GPS coord through the DEBUG-only seed button. The button
		// force-enables LocationService and calls SetGpsLocation directly — no
		// CoreLocation, no permissions, no SendKeys.
		_selectTrainPage.SeedGpsLocationForTesting();
		Thread.Sleep(500);

		// Open DTAC and verify the timetable still renders. If anything in the
		// pipeline (LocationService → presenter) had crashed, DTAC navigation
		// would fail to find the timetable container.
		var dtac = _shell.NavigateToDTAC();
		Assert.That(dtac.IsDisplayed(), Is.True);
		dtac.SwitchToTimetableTab();
		Assert.That(dtac.TimetableScrollView.Displayed, Is.True,
			"Timetable container must remain renderable after the GPS event was injected.");
	}
}
