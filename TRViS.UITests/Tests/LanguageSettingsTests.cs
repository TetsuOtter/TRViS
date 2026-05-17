using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// E2E for #40 (multi-language UI). Switching the app language must update
/// already-rendered, <c>{loc:Translate}</c>-bound labels live.
///
/// The language switch is driven through the UI_TEST-only
/// <c>StartHome.TestSetLanguageEnglishButton</c> seam, which sets
/// <c>EasterEggPageViewModel.SelectedAppLanguage = English</c> — the exact
/// path the Settings language picker uses — so the test exercises the real
/// localization pipeline (resx resolve → LocalizationResourceManager →
/// PropertyChanged("Item[]") → bound label refresh) without driving a
/// platform-specific native Picker.
///
/// Default per-test session isolation (no shared session): each test
/// relaunches the app, and the language change is in-memory only (never
/// Saved to the settings file), so it cannot leak into other fixtures.
/// </summary>
[TestFixture]
[Infrastructure.RetryAllTests(2)] // see AppLaunchTests for rationale
public class LanguageSettingsTests : BaseUITest
{
	private StartHomePageObject _startHomePage = null!;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		_startHomePage = new StartHomePageObject(Driver);

		// A prior fixture's leftover modal could carry over on iOS noReset.
		var dialog = new SelectFileDialogPageObject(Driver);
		if (dialog.PollDisplayed(AutomationIds.SelectFile.Title, timeoutSeconds: 1))
		{
			dialog.Close();
			Thread.Sleep(300);
		}

		if (!_startHomePage.PollDisplayed(AutomationIds.StartHome.Title, timeoutSeconds: 3))
		{
			new AppShellPage(Driver).NavigateToHome();
			_startHomePage = new StartHomePageObject(Driver);
		}

		// On a fresh-install launch the PrivacyPolicyDialog modal is shown over
		// StartHome and StartHome.Title is not in the accessibility tree until
		// it is dismissed. Every other StartHome-based fixture dismisses it in
		// SetUp; this #40-era fixture predated that modal, so mirror the same
		// pattern (no-op after the first call within the session).
		_startHomePage.AcceptPrivacyPolicyIfNeeded();

		Assert.That(_startHomePage.IsDisplayed(), Is.True,
			"StartHomePage should be displayed before the language test.");
	}

	/// <summary>
	/// After switching to English, the Start-mode primary button
	/// (<c>{loc:Translate StartHome_ConnectServer}</c>) must show the English
	/// resource value, proving the binding refreshed live.
	/// </summary>
	[Test]
	public void SwitchToEnglish_UpdatesBoundLabelLive()
	{
		_startHomePage.SetLanguageEnglishForTesting();

		// Give the PropertyChanged("Item[]") refresh a beat to propagate to
		// the bound Button before reading its caption.
		Thread.Sleep(500);

		string connectText = _startHomePage.ConnectServerButton.Text;
		Assert.That(connectText, Does.Contain("Load from Server"),
			"After switching the UI language to English, the "
			+ "{loc:Translate}-bound Connect-to-Server button must show the "
			+ $"English resource value. Got='{connectText}'.");
		Assert.That(connectText, Does.Not.Contain("サーバーから読み込み"),
			"The Japanese caption must no longer be shown after switching "
			+ "to English.");
	}
}
