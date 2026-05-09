using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

// [Order] is required on Windows: MAUI Preferences for unpackaged WinUI 3
// apps persist across Driver.Quit/restart in a location BaseUITest.ResetAppState
// doesn't clear, so once any fixture's SetUp accepts the privacy policy the
// FirebaseSettingViewModel.IsPrivacyPolicyAccepted flag stays true and the
// reconfirm banner is no longer shown. AppLaunchTests asserts the banner IS
// visible on first launch, so it must run before any saving fixture.
[TestFixture]
[Order(1)]
public class AppLaunchTests : BaseUITest
{
	[Test]
	[Platform(Exclude = "Win", Reason = "MAUI Border has no UIA AutomationPeer on Windows; banner is visually rendered but not findable via Appium AccessibilityId.")]
	public void App_Launches_Into_StartHome_With_Privacy_Banner()
	{
		// On a clean install the app navigates directly to StartHomePage in Start
		// mode, with the privacy-policy reconfirm banner shown until the user
		// accepts via the in-page privacy dialog.
		var startHome = new StartHomePageObject(Driver);
		Assert.That(startHome.IsDisplayed(), Is.True,
			"StartHomePage should be displayed on first launch.");

		Assert.That(startHome.IsPrivacyReconfirmBannerVisible(), Is.True,
			"Privacy reconfirm banner should be visible on a fresh install.");
	}
}
