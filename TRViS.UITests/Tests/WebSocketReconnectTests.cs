using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// E2E for #261: when a WebSocket loader's connection drops, Home must stop
/// showing "サーバー接続中" and instead show "サーバー未接続" plus a 再接続 button.
///
/// The disconnected state is reached through the UI_TEST-only
/// <c>StartHome.TestSimulateWebSocketDisconnectButton</c> seam (sets a
/// non-connected WebSocketNetworkSyncService loader + IsServerConnectionLost),
/// so the test needs no real WebSocket server and stays deterministic on CI.
/// </summary>
[TestFixture]
[Infrastructure.RetryAllTests(2)] // see AppLaunchTests for rationale
public class WebSocketReconnectTests : BaseUITest
{
	// Share one Appium session across the fixture (iOS only); see
	// BaseUITest.ShareSessionAcrossTestsInFixture. Mirrors StartHomeTests.
	protected override bool ShareSessionAcrossTestsInFixture => true;

	private StartHomePageObject _startHomePage = null!;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		_startHomePage = new StartHomePageObject(Driver);

		// Cross-fixture shared session: a prior fixture may have left a modal
		// open. Close the SelectFile dialog if it carried over.
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

		// Each test starts from "no loader": clearing the loader resets
		// IsServerConnectionLost=false (AppViewModel.OnLoaderChanged) so the
		// disconnected state from a prior test in this shared session is gone.
		_startHomePage.ClearLoaderForTesting();
		_startHomePage.AcceptPrivacyPolicyIfNeeded();

		Assert.That(_startHomePage.IsDisplayed(), Is.True,
			"StartHomePage should be displayed after recovery.");
	}

	[Test]
	public void WebSocketDisconnect_ShowsServerNotConnectedTitleAndReconnectButton()
	{
		_startHomePage.SimulateWebSocketDisconnectForTesting();

		Assert.That(_startHomePage.IsReconnectButtonVisible(), Is.True,
			"再接続 button must appear once the WebSocket connection is lost.");

		Assert.That(_startHomePage.LoaderInfoTitle.Text, Does.Contain("サーバー未接続"),
			"LoaderInfoCard title must switch from \"サーバー接続中\" to \"サーバー未接続\" "
			+ "so the disconnect is visible (#261).");

		// 開く / 閉じる stay reachable: cached data is still browsable while
		// disconnected (the WS loader's caches survive Dispose).
		Assert.Multiple(() =>
		{
			Assert.That(_startHomePage.OpenButton.Displayed, Is.True);
			Assert.That(_startHomePage.DisconnectButton.Displayed, Is.True);
		});
	}

	/// <summary>
	/// The seam stores no reconnect target, so 再接続 → ReconnectWebSocketAsync
	/// returns false without touching the network. Tapping it must not crash and
	/// must leave the disconnected UI intact (so the user can retry / 閉じる).
	/// </summary>
	[Test]
	public void ReconnectTap_WithoutStoredTarget_KeepsDisconnectedStateAndDoesNotCrash()
	{
		_startHomePage.SimulateWebSocketDisconnectForTesting();
		Assert.That(_startHomePage.IsReconnectButtonVisible(), Is.True,
			"precondition: disconnected state must be shown before tapping 再接続.");

		_startHomePage.ReconnectButton.Click();

		// State preserved after the tap: still disconnected, button still
		// available, and the Home action row is intact — i.e. we did NOT
		// navigate to DTAC or crash. NOTE: StartHome.Title is the Start-mode
		// header element and is intentionally absent from the Home-mode
		// accessibility tree, so IsDisplayed()/Title cannot be the liveness
		// probe here — assert on Home-mode elements instead.
		Assert.That(_startHomePage.IsReconnectButtonVisible(), Is.True,
			"再接続 must remain available when reconnection cannot proceed.");
		Assert.That(_startHomePage.LoaderInfoTitle.Text, Does.Contain("サーバー未接続"));
		Assert.That(_startHomePage.DisconnectButton.Displayed, Is.True,
			"the Home page must still be intact (no crash / no navigation) after the 再接続 tap.");
	}
}
