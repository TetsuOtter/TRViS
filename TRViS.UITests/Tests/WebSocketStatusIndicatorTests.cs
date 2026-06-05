using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// E2E for #266: the shared AppBar shows a WebSocket connection-status
/// indicator (green dot = connected, red dot = disconnected, spinner =
/// connecting/reconnecting, hidden for non-WebSocket loaders).
///
/// The AppBar is only shown on DTAC, so the test gets there via a UI_TEST
/// seam that builds a WebSocket-TYPED loader carrying real sample data
/// (no server), then drives the state through DTAC-side seams that mutate
/// the singleton AppViewModel's connection flags. The indicator's state is
/// asserted through the invisible <c>AppBar.ConnectionStatus</c> mirror
/// Label (Ellipse/ActivityIndicator don't reliably surface on iOS).
/// </summary>
[TestFixture]
[Infrastructure.RetryAllTests(2)] // see AppLaunchTests for rationale
public class WebSocketStatusIndicatorTests : BaseUITest
{
	// Share one Appium session across the fixture; mirrors WebSocketReconnectTests.
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

		// A prior test in this shared session may have left the app on DTAC.
		if (!_startHomePage.PollDisplayed(AutomationIds.StartHome.Title, timeoutSeconds: 3))
		{
			new AppShellPage(Driver).NavigateToHome();
			_startHomePage = new StartHomePageObject(Driver);
		}

		// Clearing the loader resets IsServerConnectionLost/IsServerReconnecting
		// (AppViewModel.OnLoaderChanged) so a prior test's state is gone.
		_startHomePage.ClearLoaderForTesting();
		_startHomePage.AcceptPrivacyPolicyIfNeeded();

		Assert.That(_startHomePage.IsDisplayed(), Is.True,
			"StartHomePage should be displayed after recovery.");
	}

	[Test]
	public void AppBar_WebSocketStatus_TransitionsThroughConnectedDisconnectedReconnecting()
	{
		_startHomePage.SimulateWebSocketConnectedForTesting();

		var dtac = new DTACViewHostPageObject(Driver);
		Assert.That(dtac.IsDisplayed(), Is.True,
			"A WebSocket-typed sample loader + committed selection should land on DTAC.");

		// A live WS loader (not lost, not reconnecting) -> green dot.
		Assert.That(dtac.WaitForConnectionStatus("Connected"), Is.True,
			"AppBar indicator must be Connected when a live WebSocket loader is active. "
			+ $"Actual: \"{dtac.ReadConnectionStatusViaSeam()}\".");

		// Connection drops -> red dot.
		dtac.TapWsDisconnectedSeam();
		Assert.That(dtac.WaitForConnectionStatus("Disconnected"), Is.True,
			"AppBar indicator must switch to Disconnected when the connection is lost. "
			+ $"Actual: \"{dtac.ReadConnectionStatusViaSeam()}\".");

		// Auto-reconnect starts -> spinner (takes priority over the lost flag).
		dtac.TapWsReconnectingSeam();
		Assert.That(dtac.WaitForConnectionStatus("Connecting"), Is.True,
			"AppBar indicator must switch to Connecting (spinner) while reconnecting. "
			+ $"Actual: \"{dtac.ReadConnectionStatusViaSeam()}\".");

		// Reconnect succeeds -> back to green (also clears the #261 lost flag).
		dtac.TapWsConnectedSeam();
		Assert.That(dtac.WaitForConnectionStatus("Connected"), Is.True,
			"AppBar indicator must return to Connected after a successful reconnect. "
			+ $"Actual: \"{dtac.ReadConnectionStatusViaSeam()}\".");
	}
}
