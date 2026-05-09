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
	private StartHomePageObject _startHomePage = null!;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		// The app launches directly into StartHomePage. On a clean install the privacy
		// reconfirm banner is shown; accept it via the in-page dialog so subsequent
		// feature-button taps don't get gated on the privacy dialog.
		_startHomePage = new StartHomePageObject(Driver);
		Assert.That(_startHomePage.IsDisplayed(), Is.True,
			"StartHomePage should appear on launch.");
		_startHomePage.AcceptPrivacyPolicyIfNeeded();
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
	/// Regression: after a fresh load no WorkGroup should be auto-picked. The
	/// WorkGroupChip stays hidden and the WorkPendingHint ("Work Group を選択してください")
	/// stays visible until the user taps a row. If the auto-cascade in
	/// <c>TimetableSelectionManager.OnLoaderChanged</c> ever comes back, this
	/// test fails immediately.
	/// </summary>
	[Test]
	public void LoadSample_DoesNotAutoSelectWorkGroup()
	{
		Assert.That(_startHomePage.IsDisplayed(), Is.True);

		_startHomePage.LoadSample();
		_startHomePage.WaitForElement(AutomationIds.StartHome.WorkGroupList);

		Assert.That(_startHomePage.IsWorkGroupChipVisible(), Is.False,
			"WorkGroupChip should NOT be visible right after load — no tentative selection has been made.");

		var hint = _startHomePage.WorkPendingHint;
		Assert.That(hint.Displayed, Is.True,
			"WorkPendingHint should be visible while no WorkGroup is selected.");
	}
}
