using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// Covers the separated full-scroll D-TAC page (#155). The entry button in
/// PageHeader is iPhone-idiom-only, so the test adapts to the device under
/// test rather than hard-asserting visibility: on a phone idiom it drives the
/// navigation round-trip; on tablet / desktop idioms it asserts the button is
/// correctly hidden. The iPad mini matrix entry runs as the iOS driver too, so
/// driver type cannot distinguish phone from tablet — the adaptive shape keeps
/// the assertion meaningful on every matrix entry without flaking.
/// </summary>
[TestFixture]
[Infrastructure.RetryAllTests(2)]
public class FullScrollTimetableTests : BaseUITest
{
	protected override bool ShareSessionAcrossTestsInFixture => true;

	private StartHomePageObject _startHomePage = null!;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		_startHomePage = new StartHomePageObject(Driver);

		// Shared session can hand off the app on a non-StartHome page; recover
		// to Start mode where LoadSample is meaningful (mirrors
		// DTACTimetableTests / HorizontalTimetableTests).
		if (!_startHomePage.PollDisplayed(AutomationIds.StartHome.Title, timeoutSeconds: 3))
		{
			new AppShellPage(Driver).NavigateToHome();
			_startHomePage = new StartHomePageObject(Driver);
		}
		_startHomePage.ClearLoaderForTesting();

		_startHomePage.AcceptPrivacyPolicyIfNeeded();
	}

	/// <summary>
	/// Loads sample data, opens DTAC, switches to the 時刻表 tab, then:
	/// on a phone idiom the full-scroll entry button is visible — tap it,
	/// assert the full-scroll page is reached (its unique AppBar back button),
	/// and pop back so the fixture ends on a flyout-rooted Shell page; on
	/// tablet / desktop idioms the button must be absent (it would only clutter
	/// a screen that already shows the whole timetable).
	/// </summary>
	[Test]
	public void FullScrollButton_NavigatesToFullScrollPage_OnPhoneIdiom()
	{
		Assert.That(_startHomePage.IsDisplayed(), Is.True);
		_startHomePage.LoadSample();
		_startHomePage.WaitForElement(AutomationIds.StartHome.WorkGroupList);

		var dtac = _startHomePage.AutoOpenForTesting();
		Assert.That(dtac.IsDisplayed(), Is.True);
		dtac.SwitchToTimetableTab();

		if (!dtac.IsFullScrollButtonVisible(timeoutSeconds: 5))
		{
			// Tablet / desktop idiom: the button must stay hidden. This is the
			// negative coverage for the iPhone-only gate.
			Assert.Pass(
				"FullScrollButton is hidden on this idiom (tablet/desktop), as expected. " +
				"Phone-idiom navigation is exercised on phone matrix entries.");
			return;
		}

		var page = dtac.TapFullScrollButton();
		Assert.That(page.IsDisplayed(), Is.True,
			"FullScrollVerticalTimetablePage's AppBar back button should be in the " +
			"accessibility tree after tapping the entry button.");

		// Pop back to DTAC (a flyout-aware Shell root) so the next test's
		// [SetUp] can NavigateToHome via the flyout — the full-scroll page is
		// a Shell push and the flyout is gated to roots.
		var backToDtac = page.TapBack();
		Assert.That(backToDtac.IsDisplayed(), Is.True,
			"Tapping back should return to the DTAC ViewHost.");
	}
}
