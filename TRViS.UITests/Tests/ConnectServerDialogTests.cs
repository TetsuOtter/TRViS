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
public class ConnectServerDialogTests : BaseUITest
{
	private static string SampleUrlA => StartHomePageObject.SeededHistoryUrls[0];
	private static string SampleUrlB => StartHomePageObject.SeededHistoryUrls[1];

	private StartHomePageObject _startHomePage = null!;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		_startHomePage = new StartHomePageObject(Driver);
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

		var dialog = _startHomePage.OpenConnectServerDialog();

		Assert.That(dialog.IsDisplayed(), Is.True, "Dialog should be displayed.");
		Assert.That(dialog.IsNewConnectionFormVisible(), Is.True,
			"With no history, the dialog should default to the new-connection form.");
		Assert.That(dialog.UrlInput.Displayed, Is.True);
		Assert.That(dialog.ConnectButton.Displayed, Is.True);
	}

	[Test]
	public void OpenDialog_WithSeededHistory_ShowsHistoryList()
	{
		Assume.That(_startHomePage.IsDisplayed(), Is.True);

		_startHomePage.SeedUrlHistoryForTesting();
		Thread.Sleep(300);

		var dialog = _startHomePage.OpenConnectServerDialog();

		Assert.That(dialog.IsHistoryViewVisible(), Is.True,
			"With seeded history, the dialog should default to the history list.");
		Assert.That(dialog.NewConnectionButton.Displayed, Is.True,
			"'+ 新規接続' should be visible in the history list state.");
	}

	/// <summary>
	/// Each seeded URL renders as a tappable card whose AutomationId is
	/// "ConnectServer.HistoryItem.&lt;url&gt;". Asserts the cards are reachable
	/// (regression coverage for the per-row id pattern; tapping triggers a real
	/// HTTP fetch so we don't follow through to dismissal here).
	/// </summary>
	[Test]
	[Platform(Exclude = "Win", Reason = "MAUI inner Border children are not surfaced via WinUI UIA.")]
	public void HistoryCards_AreReachableByPerRowAutomationId()
	{
		Assume.That(_startHomePage.IsDisplayed(), Is.True);

		_startHomePage.SeedUrlHistoryForTesting();
		Thread.Sleep(300);

		var dialog = _startHomePage.OpenConnectServerDialog();

		Assert.That(dialog.HistoryItem(SampleUrlA).Displayed, Is.True,
			$"Card for '{SampleUrlA}' should be reachable by AutomationId.");
		Assert.That(dialog.HistoryItem(SampleUrlB).Displayed, Is.True,
			$"Card for '{SampleUrlB}' should be reachable by AutomationId.");
	}

	[Test]
	public void NewConnectionButton_SwitchesToForm()
	{
		Assume.That(_startHomePage.IsDisplayed(), Is.True);

		_startHomePage.SeedUrlHistoryForTesting();
		Thread.Sleep(300);

		var dialog = _startHomePage.OpenConnectServerDialog();
		Assert.That(dialog.IsHistoryViewVisible(), Is.True);

		dialog.OpenNewConnectionForm();
		Thread.Sleep(300);

		Assert.That(dialog.IsNewConnectionFormVisible(), Is.True,
			"Tapping '+ 新規接続' should switch to the new-connection form.");
		Assert.That(dialog.BackToHistoryButton.Displayed, Is.True,
			"Back-to-history affordance should be visible when navigated from a non-empty list.");
	}

	[Test]
	public void BackToHistory_ReturnsToList()
	{
		Assume.That(_startHomePage.IsDisplayed(), Is.True);

		_startHomePage.SeedUrlHistoryForTesting();
		Thread.Sleep(300);

		var dialog = _startHomePage.OpenConnectServerDialog();
		dialog.OpenNewConnectionForm();
		Thread.Sleep(300);

		dialog.GoBackToHistory();
		Thread.Sleep(300);

		Assert.That(dialog.IsHistoryViewVisible(), Is.True,
			"Tapping back should return to the history list.");
	}

	[Test]
	public void Close_ReturnsToStartHomePage()
	{
		var dialog = _startHomePage.OpenConnectServerDialog();
		Assert.That(dialog.IsDisplayed(), Is.True);

		var back = dialog.Close();
		Thread.Sleep(300);
		Assert.That(back.IsDisplayed(), Is.True,
			"After Close the StartHomePage should be visible again.");
	}
}
