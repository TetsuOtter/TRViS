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

		// After loading the sample, the WorkGroupList should contain at least one item.
		var workGroupList = _selectTrainPage.WorkGroupList;
		Assert.That(workGroupList.Displayed, Is.True,
			"WorkGroupList should be visible after loading sample data.");

		// Verify the list is not empty by checking child elements.
		var items = workGroupList.FindElements(MobileBy.ClassName("android.widget.TextView"));
		Assert.That(items, Is.Not.Empty,
			"WorkGroupList should have items after loading the sample database.");
	}
}
