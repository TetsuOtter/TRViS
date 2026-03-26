using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

[TestFixture]
public class FirebaseSettingTests : BaseUITest
{
	[Test]
	public void SaveButton_AcceptsAndNavigatesToSelectTrain()
	{
		// The app starts on FirebaseSettingPage on first launch.
		var firebasePage = new FirebaseSettingPageObject(Driver);
		Assert.That(firebasePage.IsDisplayed(), Is.True,
			"FirebaseSettingPage should be displayed on first launch.");

		var selectTrainPage = firebasePage.SaveAndAccept();

		Assert.That(selectTrainPage.IsDisplayed(), Is.True,
			"SelectTrainPage should be displayed after accepting Firebase settings.");
	}
}
