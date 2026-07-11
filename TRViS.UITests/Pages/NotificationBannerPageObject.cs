using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Interactions;

namespace TRViS.UITests.Pages;

/// <summary>
/// Page object for the small non-modal Notification (通告) banner overlaid on the
/// DTAC ViewHost. Unlike <see cref="NotificationPopupPageObject"/> (a modal pushed
/// globally by AppShell), this banner is owned by the DTAC page itself, so it is
/// only reachable while a caller is on <see cref="DTACViewHostPageObject"/>.
/// Covers both the 受領必須 initial compact display (受領 button shown) and the
/// acknowledged 区間連動 redisplay (受領 button hidden).
/// </summary>
public class NotificationBannerPageObject : PageObject
{
	public NotificationBannerPageObject(AppiumDriver driver) : base(driver) { }

	public AppiumElement SummaryLabel => WaitForElement(AutomationIds.Notification.Banner.Summary);
	public AppiumElement AcknowledgeButton => FindByAutomationId(AutomationIds.Notification.Banner.AcknowledgeButton);
	public AppiumElement Chevron => FindByAutomationId(AutomationIds.Notification.Banner.Chevron);

	public bool IsShown(double timeoutSeconds = 10)
		=> PollDisplayed(AutomationIds.Notification.Banner.Root, timeoutSeconds);

	public bool IsAcknowledgeButtonVisible(double timeoutSeconds = 3)
		=> PollDisplayed(AutomationIds.Notification.Banner.AcknowledgeButton, timeoutSeconds);

	public string ReadSummary() => SummaryLabel.Text ?? string.Empty;

	/// <summary>Taps the banner (not the 受領 button) to expand into the large popup.</summary>
	public NotificationPopupPageObject TapToExpand()
	{
		// Tap the summary label rather than the Border root: the root's
		// AutomationId is on the Border, but its TapGestureRecognizer covers the
		// whole surface, so tapping any non-button child inside it dispatches
		// OnBannerTapped the same way.
		SummaryLabel.Click();
		return new NotificationPopupPageObject(Driver);
	}

	/// <summary>Taps 受領 (acknowledge) on the banner itself, without expanding.</summary>
	public void Acknowledge() => AcknowledgeButton.Click();

	/// <summary>
	/// Waits up to <paramref name="timeoutSeconds"/> for the banner to be gone
	/// (dismissed or replaced). Returns true if it disappeared.
	/// </summary>
	public bool WaitUntilDismissed(double timeoutSeconds = 10)
	{
		var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
		while (DateTime.UtcNow < deadline)
		{
			if (!PollDisplayed(AutomationIds.Notification.Banner.Root, timeoutSeconds: 0.5))
				return true;
		}
		return false;
	}

	/// <summary>
	/// Screen Y-coordinate (px) of the banner's top edge. Used to assert the
	/// docked position (bottom by default, top after an up-swipe) — see
	/// ViewHost.ApplyNotificationBannerDockPosition.
	/// </summary>
	public int GetTopY() => SummaryLabel.Location.Y;

	/// <summary>
	/// Best-effort upward swipe over the banner to dock it at the top of the
	/// screen (ViewHost.OnNotificationBannerSwipedUp). No-op on Windows, which
	/// doesn't accept W3C touch pointer input for MAUI ContentViews here
	/// (mirrors DTACViewHostPageObject.TrySwipeUp).
	/// </summary>
	public void SwipeUp() => Swipe(up: true);

	/// <summary>
	/// Best-effort downward swipe over the banner to dock it at the bottom of
	/// the screen (ViewHost.OnNotificationBannerSwipedDown). Same Windows
	/// no-op caveat as <see cref="SwipeUp"/>.
	/// </summary>
	public void SwipeDown() => Swipe(up: false);

	private void Swipe(bool up)
	{
		if (IsWindows)
			return;

		var element = SummaryLabel;
		var location = element.Location;
		var size = element.Size;
		int x = location.X + (size.Width / 2);
		int centerY = location.Y + (size.Height / 2);
		int startY = up ? centerY + 40 : centerY - 40;
		int endY = up ? centerY - 120 : centerY + 120;

		var touch = new PointerInputDevice(PointerKind.Touch, "finger");
		var seq = new ActionSequence(touch);
		seq.AddAction(touch.CreatePointerMove(CoordinateOrigin.Viewport, x, startY, TimeSpan.Zero));
		seq.AddAction(touch.CreatePointerDown(MouseButton.Left));
		seq.AddAction(touch.CreatePointerMove(CoordinateOrigin.Viewport, x, endY, TimeSpan.FromMilliseconds(400)));
		seq.AddAction(touch.CreatePointerUp(MouseButton.Left));
		Driver.PerformActions(new List<ActionSequence> { seq });
		Thread.Sleep(300);
	}
}
