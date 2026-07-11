using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// Verifies the server-pushed Notification (通告) receive → popup → 受領 flow.
/// A Notification is injected through the UI_TEST-only
/// <c>trvis://_test/notification</c> deeplink (typed into the Connect-to-Server
/// dialog, which routes trvis:// URLs through HandleAppLinkUriAsync), so no real
/// WebSocket server is required. The deeplink drives the exact same code path a
/// real server-pushed Notification takes:
/// LocationService.NotificationReceived → NotificationCenter → AppShell popup.
/// </summary>
[TestFixture]
[Infrastructure.RetryAllTests(2)] // see AppLaunchTests for rationale
public class NotificationPopupTests : BaseUITest
{
	protected override bool ShareSessionAcrossTestsInFixture => true;

	private StartHomePageObject _startHomePage = null!;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		_startHomePage = new StartHomePageObject(Driver);

		// Shared-session recovery: dismiss any stranded modal (a prior test may
		// have left a notification popup or the connect dialog open).
		var notif = new NotificationPopupPageObject(Driver);
		if (notif.IsDisplayed(timeoutSeconds: 1))
		{
			// Id-bearing (受領必須) popups only expose 受領; informational ones only
			// 閉じる. DismissAny picks whichever control is present.
			notif.DismissAny();
			Thread.Sleep(300);
		}
		var dialog = new ConnectServerDialogPageObject(Driver);
		if (dialog.PollDisplayed(AutomationIds.ConnectServer.Title, timeoutSeconds: 1))
		{
			dialog.Close();
			Thread.Sleep(300);
		}

		// Ensure StartHome is the active page and start from a clean loader/history
		// state (mirrors ConnectServerDialogTests.SetUp).
		if (!_startHomePage.PollDisplayed(AutomationIds.StartHome.Title, timeoutSeconds: 3))
		{
			new AppShellPage(Driver).NavigateToHome();
			_startHomePage = new StartHomePageObject(Driver);
		}
		_startHomePage.ClearLoaderForTesting();
		_startHomePage.AcceptPrivacyPolicyIfNeeded();
		_startHomePage.ClearUrlHistoryForTesting();
	}

	/// <summary>
	/// Injects an important (Priority=1) notification with a BBCode body and
	/// verifies the popup shows with its title + importance badge, then that
	/// tapping 受領 (acknowledge) dismisses it.
	/// </summary>
	[Test]
	public void Notification_Received_ShowsPopup_AndAcknowledgeDismisses()
	{
		Assume.That(_startHomePage.IsDisplayed(), Is.True,
			"StartHome should be displayed before injecting a notification.");

		// trvis:// URLs typed into the connect dialog route through
		// HandleAppLinkUriAsync, which handles the UI_TEST /_test/notification seam.
		// Body carries BBCode ([b]…[/b]); values are percent-encoded so the query
		// parser decodes them. ASCII only to avoid IME/SendKeys flakiness.
		const string title = "Test Notice";
		// reset=true → clean store each run so RetryAllTests re-displays the same id.
		const string deeplink =
			"trvis://_test/notification?id=n-1&title=Test%20Notice&body=%5Bb%5DImportant%20body%5B%2Fb%5D&priority=1&reset=true";

		var notif = InjectNotification(deeplink);

		Assert.That(notif.IsDisplayed(), Is.True, "Notification popup should be displayed after receipt.");
		Assert.That(notif.ReadTitle(), Is.EqualTo(title), "Popup should show the notification title.");
		Assert.That(notif.IsImportantBadgeVisible(), Is.True,
			"Priority=1 notification should show the importance badge.");

		notif.Acknowledge();
		Assert.That(notif.WaitUntilDismissed(), Is.True,
			"Popup should be dismissed after tapping 受領.");
	}

	/// <summary>
	/// Smoke test for the offline 受領 path: with no server connection (via
	/// <c>fakeack=false</c> the real ack path runs and throws), tapping 受領 must still
	/// close the popup cleanly — the crew is never blocked. Per product decision the
	/// notice is left unacknowledged (nothing sent) and re-display relies on the
	/// server re-delivering unacknowledged notices; that "not marked read" invariant
	/// is not observable via the UI (dedup suppresses a same-Id re-inject), so it is
	/// covered by the LocationService unit test + the online integration round-trip.
	/// </summary>
	[Test]
	public void Notification_AcknowledgeOffline_ClosesCleanly()
	{
		Assume.That(_startHomePage.IsDisplayed(), Is.True);

		// fakeack=false → 受領 goes through the real (disconnected) path and fails to send.
		const string deeplink =
			"trvis://_test/notification?id=n-offline&title=Offline%20Notice&body=body&priority=1&fakeack=false&reset=true";

		var notif = InjectNotification(deeplink);
		Assert.That(notif.IsDisplayed(), Is.True, "Popup should be displayed after receipt.");

		notif.Acknowledge();
		Assert.That(notif.WaitUntilDismissed(), Is.True,
			"Offline 受領 must close the popup (crew is never blocked).");
	}

	/// <summary>
	/// An informational notice without an Id (受領不可) shows the popup with the
	/// 閉じる button (no 受領), and tapping 閉じる dismisses it.
	/// </summary>
	[Test]
	public void Notification_NoId_ShowsPopup_AndCloseDismisses()
	{
		Assume.That(_startHomePage.IsDisplayed(), Is.True);

		// No id → non-acknowledgeable informational notice.
		const string title = "Info Notice";
		const string deeplink =
			"trvis://_test/notification?title=Info%20Notice&body=just%20info&priority=0&reset=true";

		var notif = InjectNotification(deeplink);

		Assert.That(notif.IsDisplayed(), Is.True, "Informational notice should be displayed.");
		Assert.That(notif.ReadTitle(), Is.EqualTo(title));

		notif.Dismiss();
		Assert.That(notif.WaitUntilDismissed(), Is.True,
			"Popup should be dismissed after tapping 閉じる.");
	}

	/// <summary>
	/// A notification carrying 指令番号/指令者/受信者 and a color+text icon badge
	/// shows all of them in the popup.
	/// </summary>
	[Test]
	public void Notification_WithOrderNumberSenderReceiverAndIcon_ShowsAllFields()
	{
		Assume.That(_startHomePage.IsDisplayed(), Is.True);

		const string deeplink =
			"trvis://_test/notification?id=n-meta&title=Meta%20Notice&body=body&priority=0&reset=true"
			+ "&ordernumber=ORD-042&sender=Dispatch&receiver=Crew&icontext=D&iconcolor=13022810";

		var notif = InjectNotification(deeplink);

		Assert.That(notif.IsDisplayed(), Is.True, "Popup should be displayed after receipt.");
		Assert.Multiple(() =>
		{
			Assert.That(notif.IsOrderNumberVisible(), Is.True, "指令番号 should be shown.");
			Assert.That(notif.ReadOrderNumber(), Does.Contain("ORD-042"));
			Assert.That(notif.IsSenderVisible(), Is.True, "指令者 should be shown.");
			Assert.That(notif.ReadSender(), Does.Contain("Dispatch"));
			Assert.That(notif.IsReceiverVisible(), Is.True, "受信者 should be shown.");
			Assert.That(notif.ReadReceiver(), Does.Contain("Crew"));
			Assert.That(notif.IsIconBadgeVisible(), Is.True, "Icon badge should be shown when IconText is set.");
		});

		notif.Acknowledge();
		Assert.That(notif.WaitUntilDismissed(), Is.True);
	}

	/// <summary>
	/// A notification the server marks as already acknowledged
	/// (<c>acknowledged=true</c>) must NOT pop up (it is treated as read).
	/// </summary>
	[Test]
	public void Notification_ServerAcknowledged_DoesNotPopup()
	{
		Assume.That(_startHomePage.IsDisplayed(), Is.True);

		const string deeplink =
			"trvis://_test/notification?id=n-ack&title=Already%20Read&body=body&priority=0&acknowledged=true&reset=true";

		var notif = InjectNotification(deeplink);

		Assert.That(notif.IsDisplayed(timeoutSeconds: 3), Is.False,
			"An already-acknowledged notification should not be displayed.");
	}

	/// <summary>
	/// Opens the connect dialog, types the given trvis:// deeplink into the
	/// new-connection form and taps Load. On success the dialog dismisses and
	/// (for /_test/notification) the injected notification surfaces as a popup.
	/// </summary>
	private NotificationPopupPageObject InjectNotification(string deeplink)
	{
		var dialog = _startHomePage.OpenConnectServerDialog();
		Assert.That(dialog.IsDisplayed(), Is.True, "Connect dialog should open.");

		// Empty history → new-connection form shows directly; if a history list
		// is showing (shared session leaked an entry), switch to the form.
		if (!dialog.IsNewConnectionFormVisible())
			dialog.OpenNewConnectionForm();

		dialog.TypeUrl(deeplink);
		dialog.ConnectButton.Click();

		return new NotificationPopupPageObject(Driver);
	}
}
