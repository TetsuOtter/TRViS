using OpenQA.Selenium.Appium;

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
}
