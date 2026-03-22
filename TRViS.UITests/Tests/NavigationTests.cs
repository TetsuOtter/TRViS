using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// Navigation tests require Firebase consent to have been accepted first.
/// Each test accepts Firebase consent at the beginning.
/// </summary>
[TestFixture]
public class NavigationTests : BaseUITest
{
	private AppShellPage _shell = null!;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		// Accept Firebase consent so we can reach the main shell.
		// On platforms that persist consent state across sessions (e.g. Windows
		// unpackaged) the page may be skipped on subsequent launches; use a short
		// timeout so we don't block for 30 s in that case.
		var firebasePage = new FirebaseSettingPageObject(Driver);
		if (firebasePage.IsDisplayed(TimeSpan.FromSeconds(15)))
			firebasePage.SaveAndAccept();

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
