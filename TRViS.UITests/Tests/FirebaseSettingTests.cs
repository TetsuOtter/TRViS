using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

// See AppLaunchTests for why [Order] is required.
[TestFixture]
[Order(2)]
[Infrastructure.RetryAllTests(2)] // see AppLaunchTests for rationale
public class FirebaseSettingTests : BaseUITest
{
	[Test]
	[Platform(Exclude = "Win", Reason = "MAUI Border has no UIA AutomationPeer on Windows; banner visibility cannot be probed via Appium AccessibilityId.")]
	public void PrivacyDialog_AcceptsAndDismissesReconfirmBanner()
	{
		// First launch lands on StartHomePage with the privacy reconfirm banner
		// visible. Accept via the in-page privacy dialog (replaces the legacy
		// FirebaseSettingPage flow that auto-redirected on first launch).
		var startHome = new StartHomePageObject(Driver);
		Assert.That(startHome.IsDisplayed(), Is.True,
			"StartHomePage should be displayed on first launch.");
		Assert.That(startHome.IsPrivacyReconfirmBannerVisible(), Is.True,
			"Privacy reconfirm banner should be visible before acceptance.");

		startHome.AcceptPrivacyPolicyIfNeeded();

		Assert.That(startHome.IsPrivacyReconfirmBannerVisible(), Is.False,
			"Privacy reconfirm banner should be dismissed after acceptance.");
	}
}
