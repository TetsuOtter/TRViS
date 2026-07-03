using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

using TRViS.UITests.Infrastructure;

namespace TRViS.UITests.Pages;

/// <summary>
/// Page object for the Notification (通告) popup shown when a server-pushed
/// Notification is received and judged unread. Injected in UI_TEST builds via the
/// <c>trvis://_test/notification</c> deeplink typed into the Connect-to-Server
/// dialog.
/// </summary>
public class NotificationPopupPageObject : PageObject
{
	public NotificationPopupPageObject(AppiumDriver driver) : base(driver) { }

	public AppiumElement TitleLabel => WaitForElement(AutomationIds.Notification.Title);
	public AppiumElement AcknowledgeButton => FindByAutomationId(AutomationIds.Notification.AcknowledgeButton);
	public AppiumElement DismissButton => FindByAutomationId(AutomationIds.Notification.DismissButton);
	public AppiumElement OrderNumberLabel => FindByAutomationId(AutomationIds.Notification.OrderNumber);
	public AppiumElement SenderLabel => FindByAutomationId(AutomationIds.Notification.Sender);
	public AppiumElement ReceiverLabel => FindByAutomationId(AutomationIds.Notification.Receiver);

	public bool IsDisplayed(double timeoutSeconds = 10)
		=> PollDisplayed(AutomationIds.Notification.Title, timeoutSeconds);

	public bool IsImportantBadgeVisible(double timeoutSeconds = 3)
		=> PollDisplayed(AutomationIds.Notification.ImportantBadge, timeoutSeconds);

	public bool IsIconBadgeVisible(double timeoutSeconds = 3)
		=> PollDisplayed(AutomationIds.Notification.IconBadge, timeoutSeconds);

	public bool IsOrderNumberVisible(double timeoutSeconds = 3)
		=> PollDisplayed(AutomationIds.Notification.OrderNumber, timeoutSeconds);

	public bool IsSenderVisible(double timeoutSeconds = 3)
		=> PollDisplayed(AutomationIds.Notification.Sender, timeoutSeconds);

	public bool IsReceiverVisible(double timeoutSeconds = 3)
		=> PollDisplayed(AutomationIds.Notification.Receiver, timeoutSeconds);

	public string ReadTitle() => TitleLabel.Text ?? string.Empty;
	public string ReadOrderNumber() => OrderNumberLabel.Text ?? string.Empty;
	public string ReadSender() => SenderLabel.Text ?? string.Empty;
	public string ReadReceiver() => ReceiverLabel.Text ?? string.Empty;

	/// <summary>Taps 受領 (acknowledge + close). Closes the popup whether or not the
	/// server ack succeeds; only a confirmed send marks the notice read.</summary>
	public void Acknowledge() => AcknowledgeButton.Click();

	/// <summary>Taps 閉じる (close, informational/Id-less notices only).</summary>
	public void Dismiss() => DismissButton.Click();

	/// <summary>
	/// Recovery helper for shared sessions: closes the popup with whichever control
	/// is present. Id-bearing (受領必須) popups expose only 受領 (which always closes);
	/// informational (Id 無し) popups expose only 閉じる. Returns true if a control was
	/// tapped.
	/// </summary>
	public bool DismissAny()
	{
		if (PollDisplayed(AutomationIds.Notification.AcknowledgeButton, timeoutSeconds: 0.5))
		{
			Acknowledge();
			return true;
		}
		if (PollDisplayed(AutomationIds.Notification.DismissButton, timeoutSeconds: 0.5))
		{
			Dismiss();
			return true;
		}
		return false;
	}

	/// <summary>
	/// Waits up to <paramref name="timeoutSeconds"/> for the popup to be gone
	/// (dismissed). Returns true if it disappeared.
	/// </summary>
	public bool WaitUntilDismissed(double timeoutSeconds = 10)
	{
		var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
		while (DateTime.UtcNow < deadline)
		{
			if (!PollDisplayed(AutomationIds.Notification.Title, timeoutSeconds: 0.5))
				return true;
		}
		return false;
	}
}
