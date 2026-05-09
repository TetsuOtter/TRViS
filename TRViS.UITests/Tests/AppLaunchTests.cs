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
// RetryAllTests(2): UI tests share a single Appium UIA2 / XCUITest
// instrumentation process per session, and that process is known to die
// mid-test under memory / accessibility-tree pressure (Android logcat:
// "instrumentation process is not running (probably crashed)") — once it
// dies, every subsequent test in the same fixture run instantly fails on
// "socket hang up". Per-test retry recovers via the per-test SetUp tearing
// down and rebuilding the Driver. Real assertion bugs typically fail across
// all retries, so this masks infrastructure flakes without hiding
// regressions.
// NUnit's built-in [Retry] is method-only; RetryAllTestsAttribute (in
// Infrastructure/) is a class-level wrapper that applies the same
// RetryCommand uniformly across every [Test] in the fixture.
[Infrastructure.RetryAllTests(2)]
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
