using OpenQA.Selenium.Appium.Windows;
using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// Verifies the Connect-to-Server modal (replaces the legacy "Load from Web" popup).
/// The dialog has two visual states: rich-card history list and a new-connection
/// form with the "接続先を保存する" toggle. Tapping a card auto-loads (no shared
/// Entry), so the legacy SelectionChanged↔TextChanged re-entrancy regression
/// no longer applies and is not retested here.
/// </summary>
[TestFixture]
[Infrastructure.RetryAllTests(2)] // see AppLaunchTests for rationale
public class ConnectServerDialogTests : BaseUITest
{
	// Share one Appium session across all tests in this fixture (iOS only).
	// See BaseUITest.ShareSessionAcrossTestsInFixture for details.
	protected override bool ShareSessionAcrossTestsInFixture => true;

	private static string SampleUrlA => StartHomePageObject.SeededHistoryUrls[0];
	private static string SampleUrlB => StartHomePageObject.SeededHistoryUrls[1];

	private StartHomePageObject _startHomePage = null!;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		_startHomePage = new StartHomePageObject(Driver);

		// Shared-session recovery: the dialog is a MAUI modal, so if a
		// prior test left it open we can't open another. Close any
		// stranded ConnectServer dialog before proceeding. Use a fast
		// PollDisplayed instead of dialog.IsDisplayed() — the latter
		// internally waits 30 s for the Title element, which on a
		// dialog-not-open run blocks the [SetUp] for the full timeout.
		var dialog = new ConnectServerDialogPageObject(Driver);
		if (dialog.PollDisplayed(AutomationIds.ConnectServer.Title, timeoutSeconds: 1))
		{
			dialog.Close();
			Thread.Sleep(300);
		}

		// Shared-session recovery (mirrors LanguageSettingsTests.SetUp): an
		// earlier fixture — notably ScreenshotRegressionTests at Order(3),
		// which calls LoadSample() — may have left a loaded loader, putting
		// StartHome in Home mode where the Start-mode Title / ConnectServerButton
		// this fixture relies on are absent from the iOS accessibility tree.
		// StartHomePageObject.Title has a 60 s WaitForElement, so without this
		// the first test's IsDisplayed() Assume blocks for a full minute and
		// then fails (WebDriverTimeoutException is not caught by IsDisplayed()).
		// ScreenshotRegressionTests now clears the loader at its own leak
		// source; this is the defensive net for the failure path.
		if (!_startHomePage.PollDisplayed(AutomationIds.StartHome.Title, timeoutSeconds: 3))
		{
			new AppShellPage(Driver).NavigateToHome();
			_startHomePage = new StartHomePageObject(Driver);
		}
		_startHomePage.ClearLoaderForTesting();

		_startHomePage.AcceptPrivacyPolicyIfNeeded();
	}

	[Test]
	[Platform(Exclude = "Win", Reason = "Windows MAUI never exposes a UIA peer for the new-connection form's Entry when NewConnectionView is the initially-visible sub-view of the modal — even with the XAML IsVisible='False' default removed and PopulateHistory moved to the constructor. The complementary OpenDialog_WithSeededHistory_ShowsHistoryList path passes on Windows, so the dialog itself works; this is an Appium/UIA peer-creation quirk specific to the empty-history initial render.")]
	public void OpenDialog_WithEmptyHistory_ShowsNewConnectionFormDirectly()
	{
		// Clean install: no URLs in history. The dialog should skip the empty-list
		// state and render the new-connection form directly so the user has a
		// single, clear path forward.
		Assume.That(_startHomePage.IsDisplayed(), Is.True);

		// Explicitly clear URL history before opening the dialog. iOS uses
		// noReset:true for FrontBoard stability (AppiumConfig.cs); BaseUITest's
		// per-test preference wipe operates on the simulator filesystem, but
		// the AppViewModel singleton has been observed to retain in-memory
		// history across sessions because the XCUITest driver relaunches the
		// app rather than killing+reinstalling. Clearing in-memory + persisted
		// state via the test seam removes that race for this assertion.
		_startHomePage.ClearUrlHistoryForTesting();

		var dialog = _startHomePage.OpenConnectServerDialog();

		Assert.That(dialog.IsDisplayed(), Is.True, "Dialog should be displayed.");
		Assert.That(dialog.IsNewConnectionFormVisible(), Is.True,
			"With no history, the dialog should default to the new-connection form.");
		Assert.That(dialog.UrlInput.Displayed, Is.True);
		Assert.That(dialog.ConnectButton.Displayed, Is.True);
	}

	/// <summary>
	/// Seeded-history happy path, merged from the prior tests
	/// OpenDialog_WithSeededHistory_ShowsHistoryList /
	/// HistoryCards_AreReachableByPerRowAutomationId /
	/// NewConnectionButton_SwitchesToForm / BackToHistory_ReturnsToList /
	/// Close_ReturnsToStartHomePage. Each of those paid LoadSample-equivalent
	/// setup separately; the merged flow runs the heavy
	/// SeedUrlHistory + OpenConnectServerDialog once, then asserts each
	/// transition with a labelled Assert so a regression points at the
	/// specific sub-step rather than the merged test name.
	/// </summary>
	[Test]
	public void SeededHistory_DialogTransitions_FullFlow()
	{
		Assume.That(_startHomePage.IsDisplayed(), Is.True,
			"StartHome should be displayed before opening the dialog.");

		_startHomePage.SeedUrlHistoryForTesting();
		Thread.Sleep(300);

		// Open: history list state shows because history is non-empty.
		var dialog = _startHomePage.OpenConnectServerDialog();
		Assert.That(dialog.IsDisplayed(), Is.True,
			"ConnectServer dialog should be displayed after OpenConnectServerDialog().");
		Assert.That(dialog.IsHistoryViewVisible(), Is.True,
			"With seeded history, the dialog should default to the history list state.");
		Assert.That(dialog.NewConnectionButton.Displayed, Is.True,
			"'+ 新規接続' button should be visible in the history list state.");

		// Per-row card AutomationIds — regression for the
		// `ConnectServer.HistoryItem.<url>` pattern. Win excluded because
		// MAUI inner Border children are not surfaced via WinUI UIA;
		// guarded inline rather than as a [Platform] attribute on a
		// separate test because the rest of the flow IS valid on Win.
		if (Driver is not WindowsDriver)
		{
			Assert.That(dialog.HistoryItem(SampleUrlA).Displayed, Is.True,
				$"History card for '{SampleUrlA}' should be reachable by AutomationId.");
			Assert.That(dialog.HistoryItem(SampleUrlB).Displayed, Is.True,
				$"History card for '{SampleUrlB}' should be reachable by AutomationId.");
		}

		// "+ 新規接続" → form
		dialog.OpenNewConnectionForm();
		Thread.Sleep(300);
		Assert.That(dialog.IsNewConnectionFormVisible(), Is.True,
			"Tapping '+ 新規接続' should switch to the new-connection form.");
		Assert.That(dialog.BackToHistoryButton.Displayed, Is.True,
			"Back-to-history affordance should be visible when arriving at the form from a non-empty history.");

		// Back → history list
		dialog.GoBackToHistory();
		Thread.Sleep(300);
		Assert.That(dialog.IsHistoryViewVisible(), Is.True,
			"Tapping back should return to the history list.");

		// Close → StartHome
		var back = dialog.Close();
		Thread.Sleep(300);
		Assert.That(back.IsDisplayed(), Is.True,
			"After Close the dialog should dismiss and StartHomePage should be visible again.");
	}
}
