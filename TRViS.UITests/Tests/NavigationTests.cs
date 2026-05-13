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

		// Navigate to StartHome at the top of each test so OpenFlyout starts
		// from a flyout-rooted page. The earlier "OpenFlyout works from any
		// flyout-capable page" assumption breaks on Android in the
		// shared-session run: the previous fixture (HorizontalTimetableTests)
		// lands the app on DTAC's VerticalView tab which has orientation
		// locked to Landscape, and on Android the OpenFlyout probes (DTAC
		// MenuButton click or "Open navigation drawer") don't open the
		// NavigationView under that lock (CI run 25727806170 / 25729263553).
		// NavigateToHome uses the DTAC.TestNavigateHomeButton seam to GoToAsync
		// past the broken flyout path, and the seam's OnDisappearing also
		// unlocks the orientation. From StartHome OpenFlyout works fine on
		// every platform.
		var startHome = new StartHomePageObject(Driver);
		if (!startHome.PollDisplayed(AutomationIds.StartHome.Title, timeoutSeconds: 3))
		{
			new AppShellPage(Driver).NavigateToHome();
			startHome = new StartHomePageObject(Driver);
		}
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
