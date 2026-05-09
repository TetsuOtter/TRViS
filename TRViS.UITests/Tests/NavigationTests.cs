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
	private AppShellPage _shell = null!;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		// The app launches into StartHomePage. Accept privacy via the in-page dialog
		// so feature buttons (Connect, SelectFile, etc.) aren't gated on the privacy
		// modal during navigation tests.
		var startHome = new StartHomePageObject(Driver);
		_ = startHome.Title; // wait for first render
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
