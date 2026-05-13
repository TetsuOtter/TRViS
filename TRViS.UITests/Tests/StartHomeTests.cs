using OpenQA.Selenium;
using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// Tests for the unified Start/Home page (replaces the legacy SelectTrainPage).
/// </summary>
[TestFixture]
[Infrastructure.RetryAllTests(2)] // see AppLaunchTests for rationale
public class StartHomeTests : BaseUITest
{
	// Share one Appium session across all tests in this fixture (iOS only).
	// See BaseUITest.ShareSessionAcrossTestsInFixture for details.
	protected override bool ShareSessionAcrossTestsInFixture => true;

	private StartHomePageObject _startHomePage = null!;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		_startHomePage = new StartHomePageObject(Driver);

		// Cross-fixture shared session: the SelectFileDialog fixture's last
		// test (TapUpFolder_ReturnsToRoot) intentionally leaves the dialog
		// open, and with one Appium session for the whole suite that
		// modal carries over into this fixture. Close it before probing
		// StartHome — the Title element is not findable while the modal
		// is on top.
		var dialog = new SelectFileDialogPageObject(Driver);
		if (dialog.PollDisplayed(AutomationIds.SelectFile.Title, timeoutSeconds: 1))
		{
			dialog.Close();
			Thread.Sleep(300);
		}

		// Shared-session recovery: a prior test in this fixture loaded
		// the demo, leaving the app in Home mode with the loader still
		// set. Bring it back to Start mode by clearing the loader.
		if (!_startHomePage.PollDisplayed(AutomationIds.StartHome.Title, timeoutSeconds: 3))
		{
			new AppShellPage(Driver).NavigateToHome();
			_startHomePage = new StartHomePageObject(Driver);
		}
		_startHomePage.ClearLoaderForTesting();

		// Privacy is reset on first launch only; AcceptPrivacyPolicyIfNeeded
		// fast-paths to a no-op after the first call within the fixture.
		_startHomePage.AcceptPrivacyPolicyIfNeeded();

		Assert.That(_startHomePage.IsDisplayed(), Is.True,
			"StartHomePage should be displayed after recovery.");
	}

	[Test]
	public void LoadSample_PopulatesWorkGroupList()
	{
		Assert.That(_startHomePage.IsDisplayed(), Is.True,
			"StartHomePage should be displayed.");

		_startHomePage.LoadSample();

		// After demo load, the page transitions to Home mode and the WorkGroup list becomes visible.
		var workGroupList = _startHomePage.WaitForElement(AutomationIds.StartHome.WorkGroupList);
		Assert.That(workGroupList.Displayed, Is.True,
			"WorkGroupList should be visible after loading sample data.");

		// Verify the list is not empty by checking descendant elements.
		var items = workGroupList.FindElements(By.XPath(".//*"));
		Assert.That(items, Is.Not.Empty,
			"WorkGroupList should have items after loading the sample database.");
	}

	/// <summary>
	/// Regression: after a fresh load no WorkGroup should be auto-picked.
	/// WorkGroupChip (the "selected WG" chip) stays hidden until the user
	/// taps a row in WorkGroupList. If the auto-cascade in
	/// <c>TimetableSelectionManager.OnLoaderChanged</c> ever comes back,
	/// WorkGroupChip would appear and this test fails immediately.
	/// </summary>
	[Test]
	public void LoadSample_DoesNotAutoSelectWorkGroup()
	{
		Assert.That(_startHomePage.IsDisplayed(), Is.True);

		_startHomePage.LoadSample();
		_startHomePage.WaitForElement(AutomationIds.StartHome.WorkGroupList);

		Assert.That(_startHomePage.IsWorkGroupChipVisible(), Is.False,
			"WorkGroupChip should NOT be visible right after load — no tentative selection has been made.");
	}
}
