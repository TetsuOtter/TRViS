using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// Navigation tests require Firebase consent to have been accepted first.
/// Each test accepts Firebase consent at the beginning.
/// </summary>
[TestFixture]
[Infrastructure.RetryAllTests(2)] // see AppLaunchTests for rationale
public class NavigationTests : BaseUITest
{
	// Share one Appium session across all tests in this fixture (iOS only).
	// See BaseUITest.ShareSessionAcrossTestsInFixture for details.
	protected override bool ShareSessionAcrossTestsInFixture => true;

	private AppShellPage _shell = null!;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		// AppShellPage.OpenFlyout works from any flyout-capable page, so
		// in shared-session mode we don't need to navigate back to
		// StartHome between tests — test 1 lands on ThirdParty, test 2's
		// flyout opens from ThirdParty and navigates to Settings, etc.
		// AcceptPrivacyPolicyIfNeeded handles the only state that
		// genuinely needs StartHome: it fast-paths to a no-op when the
		// privacy banner is not visible (always true after the first
		// test, regardless of current page).
		var startHome = new StartHomePageObject(Driver);
		startHome.AcceptPrivacyPolicyIfNeeded();

		_shell = new AppShellPage(Driver);
	}

	[Test]
	public void Flyout_NavigateToThirdPartyLicenses()
	{
		var page = _shell.NavigateToThirdPartyLicenses();
		Assert.That(page.IsDisplayed(), Is.True,
			"ThirdPartyLicensesPage should be displayed after navigation.");
	}

	[Test]
	public void Flyout_NavigateToSettings()
	{
		var page = _shell.NavigateToSettings();
		Assert.That(page.IsDisplayed(), Is.True,
			"Settings (EasterEgg) page should be displayed after navigation.");
	}

	[Test]
	public void Flyout_NavigateToDTAC()
	{
		var page = _shell.NavigateToDTAC();
		Assert.That(page.IsDisplayed(), Is.True,
			"DTACViewHost page should be displayed after navigation.");
	}
}
