using OpenQA.Selenium;
using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// Tests for the unified Start/Home page (replaces the legacy SelectTrainPage).
/// </summary>
[TestFixture]
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
}
