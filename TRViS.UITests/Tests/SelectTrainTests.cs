using OpenQA.Selenium;
using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

[TestFixture]
public class SelectTrainTests : BaseUITest
{
	private SelectTrainPageObject _selectTrainPage = null!;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		// Accept Firebase consent to reach SelectTrainPage.
		var firebasePage = new FirebaseSettingPageObject(Driver);
		_selectTrainPage = firebasePage.SaveAndAccept();
	}

	[Test]
	public void LoadSample_PopulatesWorkGroupList()
	{
		Assert.That(_selectTrainPage.IsDisplayed(), Is.True,
			"SelectTrainPage should be displayed.");

		_selectTrainPage.LoadSample();

		// Wait for UI re-binding to complete (list has at least one item)
		var workGroupList = _selectTrainPage.WaitForElement(AutomationIds.SelectTrain.WorkGroupList);
		Assert.That(workGroupList.Displayed, Is.True,
			"WorkGroupList should be visible after loading sample data.");

		// Verify the list is not empty by checking descendant elements.
		// Using XPath with .//* works across all platforms without needing
		// platform-specific class names (e.g. android.widget.TextView vs XCUIElementTypeCell).
		var items = workGroupList.FindElements(By.XPath(".//*"));
		Assert.That(items, Is.Not.Empty,
			"WorkGroupList should have items after loading the sample database.");
	}
}
