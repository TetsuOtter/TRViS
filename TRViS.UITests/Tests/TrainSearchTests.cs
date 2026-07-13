using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// E2E for the train-search feature (Issue #197): over a WebSocket connection whose
/// server advertises the <c>TrainSearch</c> feature, the crew types a train number in
/// the QuickSwitchPopup Search tab (auto-searched, debounced — no search button), picks
/// a candidate, confirms, and the app switches to that train's WHOLE duty (WorkGroup/Work),
/// changing the header's 行路番号 and leaving the ハコ tab visible (there is no "return to
/// previous train" affordance — the switch is permanent, like any other duty switch).
///
/// A UI_TEST seam builds a WebSocket-TYPED loader that advertises TrainSearch and serves
/// a canned dataset (train "9999" → TrainId "uitest-searched-9999", belonging to a
/// DIFFERENT WorkGroup/Work than the initially committed duty), so the whole flow runs
/// with no real server and genuinely proves a cross-duty switch. Mirrors
/// <see cref="WebSocketStatusIndicatorTests"/>.
/// </summary>
[TestFixture]
[Platform(Exclude = "Win", Reason = "Blocked by a PRE-EXISTING Windows crash unrelated to train search: opening the QuickSwitch popover (ViewHost.TitleLabel_Tapped -> TR.Maui.AnchorPopover.ShowAsync) throws an unhandled MissingMethodException 'ElementExtensions.ToPlatform(IElement, IMauiContext)' inside AnchorPopover's WinUI rendering, so MAUI shows 'The app will exit' and the popover never opens. Verified via the ui-test-windows failure artifact: the page source contained NO popover elements and the screenshot showed the crash dialog (IsSearchTabPresent() is false because the app crashed, not because the tab is hidden). Confirmed pre-existing, not a regression: the ShowAsync call site and the whole assembly-binding environment (global.json workload pin, TRViS.csproj, TR.Maui.AnchorPopover version) are identical to main -- main crashes the same way; this is simply the first UI test to open QuickSwitch on Windows. The AnchorPopover/MAUI ABI fix is a separate dependency/platform concern tracked outside this train-search PR. This Appium suite now runs on Android in CI (Apple platforms moved to XCUITest under TRViS.UITests.Apple/, which has no train-search coverage yet); the protocol/logic is separately verified by TrainSearchIntegrationTests.")]
[Infrastructure.RetryAllTests(2)] // see AppLaunchTests for rationale
public class TrainSearchTests : BaseUITest
{
	private const string SearchedTrainId = "uitest-searched-9999";

	// Sample data (TRViS/Resources/Raw/sample_data.json): the seam commits the first
	// WorkGroup/Work ("Work1-1") and the canned search result belongs to the second
	// WorkGroup's first Work ("線形連結リスト") — a genuinely different duty.
	private const string CommittedWorkName = "Work1-1";
	private const string SearchedWorkName = "線形連結リスト";

	protected override bool ShareSessionAcrossTestsInFixture => true;

	private StartHomePageObject _startHomePage = null!;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		_startHomePage = new StartHomePageObject(Driver);

		// A prior fixture may have left the SelectFile dialog open.
		var dialog = new SelectFileDialogPageObject(Driver);
		if (dialog.PollDisplayed(AutomationIds.SelectFile.Title, timeoutSeconds: 1))
		{
			dialog.Close();
			Thread.Sleep(300);
		}

		// A prior test may have left the app on DTAC.
		if (!_startHomePage.PollDisplayed(AutomationIds.StartHome.Title, timeoutSeconds: 3))
		{
			new AppShellPage(Driver).NavigateToHome();
			_startHomePage = new StartHomePageObject(Driver);
		}

		_startHomePage.ClearLoaderForTesting();
		_startHomePage.AcceptPrivacyPolicyIfNeeded();

		Assert.That(_startHomePage.IsDisplayed(), Is.True,
			"StartHomePage should be displayed after recovery.");
	}

	[Test]
	public void TrainSearch_SearchSelectConfirm_SwitchesWholeDutyAndHeader()
	{
		_startHomePage.SimulateWebSocketSearchForTesting();

		var dtac = new DTACViewHostPageObject(Driver);
		Assert.That(dtac.IsDisplayed(), Is.True,
			"A WebSocket-typed loader with train search + committed selection should land on DTAC.");
		Assert.That(dtac.ReadTitleViaSeam(), Is.EqualTo(CommittedWorkName),
			"The header should show the initially committed duty's Work name.");

		// Open QuickSwitch; the Search tab is present because the server advertises TrainSearch.
		dtac.OpenQuickSwitch();
		Assert.That(dtac.IsSearchTabPresent(), Is.True,
			"The QuickSwitch Search tab must be shown when the server advertises the TrainSearch feature.");

		// Type the train number; search runs automatically (debounced) with no search button.
		dtac.TapSearchTab();
		dtac.EnterTrainNumber("9999");
		Assert.That(dtac.WaitForSearchResult(SearchedTrainId, timeoutSeconds: 8), Is.True,
			"The search result for train 9999 should appear in the list after the debounce.");

		// Select the candidate and confirm.
		dtac.TapSearchResult(SearchedTrainId);
		dtac.AcceptConfirmDialog();

		// The whole duty switched: DTAC stays up, the ハコ tab remains visible (no special
		// "displaying a searched train" mode), and the header now shows the searched duty.
		Assert.That(dtac.IsDisplayed(), Is.True,
			"DTAC should still be displayed after confirming the searched train.");
		Assert.That(dtac.IsHakoTabPresent(timeoutSeconds: 3), Is.True,
			"The ハコ tab must remain visible — switching duty via search is a normal duty switch.");
		Assert.That(dtac.ReadTitleViaSeam(), Is.EqualTo(SearchedWorkName),
			"The header's 行路番号 (Work name) must switch to the searched train's duty.");

		// There is no "return to previous train" affordance any more.
		dtac.OpenQuickSwitch();
		Assert.That(dtac.IsSearchTabPresent(), Is.True);

		// Leave QuickSwitch closed: this fixture shares its Appium session with
		// later fixtures, and an open popover breaks their SetUp recovery (see
		// DTACViewHostPageObject.CloseQuickSwitch).
		dtac.CloseQuickSwitch();
		Assert.That(dtac.IsSearchTabPresent(timeoutSeconds: 2), Is.False,
			"QuickSwitch must be closed at the end of the test so it doesn't leak into the next fixture's shared session.");
	}

	// Real entry (both the software numeric keypad and Keyboard.Numeric OS keyboard,
	// see DTACViewHostPageObject.EnterTrainNumber) only ever produces digits, so unlike
	// the WebSocket test's canned "9999" this test must search for a train number that
	// actually exists in sample_data.json and is reachable by typing digits alone.
	// sample_data.json's real train numbers are all kanji-prefixed ("試単9092"), so the
	// query "9092" is never a Prefix match — Contains mode is required. The matched
	// train ("試単9092", TrainId "1-1-2") belongs to the SAME WorkGroup/Work as the
	// initially committed duty, so — unlike the WebSocket test — this does not prove a
	// cross-duty switch; it proves the local search + select + confirm flow itself works
	// end-to-end against a JSON-backed (non-WebSocket) loader.
	private const string LocalSearchedTrainId = "1-1-2";

	[Test]
	public void TrainSearch_LocalJsonLoader_SearchSelectConfirm()
	{
		// No WebSocket involved: SimulateLocalSearchForTesting loads sample_data.json
		// through the same JSON-backed ILoader a user gets by opening a .json file.
		// Train search must work here too (the fix this test guards), not just when
		// connected to a server.
		_startHomePage.SimulateLocalSearchForTesting();

		var dtac = new DTACViewHostPageObject(Driver);
		Assert.That(dtac.IsDisplayed(), Is.True,
			"A JSON-backed loader with a committed selection should land on DTAC.");
		Assert.That(dtac.ReadTitleViaSeam(), Is.EqualTo(CommittedWorkName),
			"The header should show the initially committed duty's Work name.");

		// The Search tab must be visible for a local (non-WebSocket) loader too.
		dtac.OpenQuickSwitch();
		Assert.That(dtac.IsSearchTabPresent(), Is.True,
			"The QuickSwitch Search tab must be shown for a loaded JSON/SQLite file, not only a WebSocket connection.");

		dtac.TapSearchTab();
		dtac.TapMatchModeContains();
		dtac.EnterTrainNumber("9092");
		Assert.That(dtac.WaitForSearchResult(LocalSearchedTrainId, timeoutSeconds: 8), Is.True,
			"Searching sample_data.json's own train number should find it locally, with no server involved.");

		dtac.TapSearchResult(LocalSearchedTrainId);
		dtac.AcceptConfirmDialog();

		Assert.That(dtac.IsDisplayed(), Is.True,
			"DTAC should still be displayed after confirming the searched train.");
		Assert.That(dtac.IsHakoTabPresent(timeoutSeconds: 3), Is.True,
			"The ハコ tab must remain visible after switching to the searched train.");
		Assert.That(dtac.ReadTitleViaSeam(), Is.EqualTo(CommittedWorkName),
			"The searched train belongs to the same duty, so the header's 行路番号 is unchanged.");

		// Leave QuickSwitch closed: this fixture shares its Appium session with
		// later fixtures, and an open popover breaks their SetUp recovery.
		dtac.OpenQuickSwitch();
		dtac.CloseQuickSwitch();
		Assert.That(dtac.IsSearchTabPresent(timeoutSeconds: 2), Is.False,
			"QuickSwitch must be closed at the end of the test so it doesn't leak into the next fixture's shared session.");
	}
}
