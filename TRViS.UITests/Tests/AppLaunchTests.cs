using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

// [Order] is required on Windows: MAUI Preferences for unpackaged WinUI 3
// apps persist across Driver.Quit/restart in a location BaseUITest.ResetAppState
// doesn't clear, so once any fixture's SetUp clicks SaveAndAccept the
// FirebaseSettingViewModel.IsEnabled flag stays true and the consent page is
// no longer shown. AppLaunchTests and FirebaseSettingTests both assert the
// consent page IS shown, so they must run before any saving fixture.
[TestFixture]
[Order(1)]
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
