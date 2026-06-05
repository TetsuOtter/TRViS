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
/// CI runs every fixture in one assembly-shared Appium session (iOS
/// <c>noReset</c>; Android/Windows keep app data warm too), so this fixture
/// is NOT isolated in practice. Two consequences are handled here:
/// <list type="bullet">
/// <item>The prior fixture (alphabetically <c>DTACTimetableTests</c>) leaves
/// a loaded "Demo data" loader, putting StartHome in Home mode where the
/// Start-mode <c>Title</c>/<c>ConnectServerButton</c> are absent — so SetUp
/// clears the loader (mirroring <see cref="WebSocketReconnectTests"/>) to
/// return to Start mode before asserting.</item>
/// <item>The English switch mutates the process-wide UI language, but no
/// TearDown restore is performed: an audit of every fixture that runs after
/// this one in the shared session found none that asserts a localized
/// Japanese caption (the only candidate, <see cref="WebSocketReconnectTests"/>,
/// was made language-agnostic; <see cref="StationNameDisplayTests"/>'s
/// Japanese literals are timetable data, not localized UI). Restoring via a
/// language seam would also be Android-unreachable (see the [Platform]
/// reason below), so a TearDown re-pin would regress nothing it could
/// reliably fix.</item>
/// </list>
/// The language change is in-memory only (never Saved to the settings file),
/// so it cannot leak into a *fresh* session.
/// </summary>
[TestFixture]
[Infrastructure.RetryAllTests(2)] // see AppLaunchTests for rationale
[Platform(Exclude = "Linux", Reason = "Android UIAutomator2 prunes invisible (transparent) Button seams that render outside the narrow x_dp∈[0,24] left strip of the accessibility tree, so the #40 language seams (StartHome.TestSetLanguageEnglishButton / TestSetLanguageJapaneseButton) are not findable on Android — empirically falsified across four placements (extra TestSeamHost rows, a standalone second-x-band Button, and a widened-host right-half child). The localization pipeline this fixture guards (resx → LocalizationResourceManager → {loc:Translate}-bound label refresh) is platform-agnostic and verified on iPhone, iPad, macOS and Windows. An Android-reachable seam mechanism is tracked in a follow-up issue. (The Android job is the only UI-test job whose NUnit host is Linux.)")]
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

		// Shared-session recovery (mirrors WebSocketReconnectTests.SetUp): the
		// prior fixture may have left a loaded loader, which puts StartHome in
		// Home mode where the Start-mode Title / ConnectServerButton this test
		// relies on are absent (proven by the iPhone CI page-source:
		// LoaderInfoTitle="Demo data", DisconnectButton present,
		// ConnectServerButton missing). Clearing the loader returns the page to
		// Start mode so both the line-55 IsDisplayed() assert and the test
		// body's ConnectServerButton lookup resolve on every platform.
		_startHomePage.ClearLoaderForTesting();

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

		// Poll for the PropertyChanged("Item[]") refresh to propagate to the
		// bound Button instead of a fixed sleep: the refresh latency varies
		// with simulator load (a fixed 500 ms was observed flaking on the
		// busier iPhone CI runner). Read the caption every 100 ms for up to
		// 5 s and stop as soon as it flips to English; the assertions below
		// still give a precise failure message if it never does.
		string connectText = _startHomePage.ConnectServerButton.Text;
		var deadline = DateTime.UtcNow.AddSeconds(5);
		while (!connectText.Contains("Load from Server") && DateTime.UtcNow < deadline)
		{
			Thread.Sleep(100);
			connectText = _startHomePage.ConnectServerButton.Text;
		}
		Assert.That(connectText, Does.Contain("Load from Server"),
			"After switching the UI language to English, the "
			+ "{loc:Translate}-bound Connect-to-Server button must show the "
			+ $"English resource value. Got='{connectText}'.");
		Assert.That(connectText, Does.Not.Contain("サーバーから読み込み"),
			"The Japanese caption must no longer be shown after switching "
			+ "to English.");
	}
}
