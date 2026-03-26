using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

[TestFixture]
public class AppLaunchTests : BaseUITest
{
	[Test]
	public void App_Launches_Successfully()
	{
		// On first launch the app navigates to FirebaseSettingPage for user consent.
		var firebasePage = new FirebaseSettingPageObject(Driver);
		Assert.That(firebasePage.IsDisplayed(), Is.True,
			"FirebaseSettingPage should be displayed on first launch.");
	}
}
