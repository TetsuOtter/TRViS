using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// Tests covering the horizontal-timetable button + page reachable from
/// PageHeader (時刻表 tab). The button is hidden unless the selected Work
/// carries an embedded horizontal timetable; the UI_TEST seed seam injects
/// one synthetically so the navigation path can be exercised.
/// </summary>
[TestFixture]
[Infrastructure.RetryAllTests(2)]
public class HorizontalTimetableTests : BaseUITest
{
	// Share one Appium session across all tests in this fixture.
	// See BaseUITest.ShareSessionAcrossTestsInFixture for details.
	protected override bool ShareSessionAcrossTestsInFixture => true;

	private StartHomePageObject _startHomePage = null!;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		_startHomePage = new StartHomePageObject(Driver);

		// Shared session can hand off the app on a non-StartHome page
		// (e.g. DTACTimetableTests leaves it on DTAC with sample data
		// loaded). Navigate back and clear the loader so the test
		// re-enters Start mode where LoadSample is meaningful.
		if (!_startHomePage.PollDisplayed(AutomationIds.StartHome.Title, timeoutSeconds: 3))
		{
			new AppShellPage(Driver).NavigateToHome();
			_startHomePage = new StartHomePageObject(Driver);
		}
		_startHomePage.ClearLoaderForTesting();

		_startHomePage.AcceptPrivacyPolicyIfNeeded();
	}

	/// <summary>
	/// Drives the full path: seed seam swaps in a synthetic Work with
	/// HasETrainTimetable=true + a 1×1 PNG, navigates to DTAC, taps the
	/// 時刻表 tab, and asserts the 横型時刻表 button is visible. We do not
	/// further assert WebView render content because cross-platform readback
	/// of WebView body is unreliable; reaching the page is the observable
	/// contract.
	/// </summary>
	[Test]
	public void HorizontalTimetableButton_Visible_NavigatesToHorizontalTimetablePage()
	{
		Assert.That(_startHomePage.IsDisplayed(), Is.True);
		_startHomePage.LoadSample();
		_startHomePage.WaitForElement(AutomationIds.StartHome.WorkGroupList);

		var dtac = _startHomePage.SeedHorizontalTimetableAndOpenForTesting();
		Assert.That(dtac.IsDisplayed(), Is.True);
		dtac.SwitchToTimetableTab();

		Assert.That(
			dtac.IsHorizontalTimetableButtonVisible(timeoutSeconds: 5),
			Is.True,
			"Seeded Work has HasETrainTimetable=true; the button should appear.");

		dtac.TapHorizontalTimetableButton();

		var page = new HorizontalTimetablePageObject(Driver);
		Assert.That(page.IsDisplayed(), Is.True,
			"HorizontalTimetablePage's WebView should be in the accessibility tree after tap.");

		// Leave the app on DTAC (a flyout-aware Shell root) so the next
		// fixture's [SetUp] can NavigateToHome via the flyout. HT is a
		// pushed page and the flyout is not reachable from it, so a
		// fresh test in another fixture would otherwise be stranded.
		page.TapBack();
	}
}
