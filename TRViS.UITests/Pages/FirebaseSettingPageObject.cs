using OpenQA.Selenium.Appium;
using TRViS.UITests.Infrastructure;

namespace TRViS.UITests.Pages;

public class FirebaseSettingPageObject : PageObject
{
	public FirebaseSettingPageObject(AppiumDriver driver) : base(driver) { }

	public AppiumElement Title => WaitForElement(AutomationIds.Firebase.Title);
	public AppiumElement AnalyticsSwitch => FindByAutomationId(AutomationIds.Firebase.AnalyticsSwitch);
	public AppiumElement ResetButton => FindByAutomationId(AutomationIds.Firebase.ResetButton);
	public AppiumElement SaveButton => FindByAutomationId(AutomationIds.Firebase.SaveButton);

	public bool IsDisplayed(TimeSpan? timeout = null)
	{
		try
		{
			return WaitForElement(AutomationIds.Firebase.Title, timeout).Displayed;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Clicks the Save button and accepts the DisplayAlert if it appears.
	/// Returns a SelectTrainPageObject after navigation completes.
	/// </summary>
	public SelectTrainPageObject SaveAndAccept()
	{
		// Wait for the page to be fully visible before interacting with buttons.
		_ = Title;
		SaveButton.Click();

		// Accept the "Success!" confirmation dialog.
		// The W3C alert API is not implemented by some drivers (e.g. mac2),
		// so fall back to finding the OK button directly in the accessibility tree.
		try
		{
			Driver.SwitchTo().Alert().Accept();
		}
		catch (OpenQA.Selenium.NoAlertPresentException)
		{
			// Alert already dismissed or did not appear on this platform.
		}
		catch
		{
			// Fallback: driver does not support the W3C alert endpoint (e.g. mac2).
			// On macOS Catalyst, DisplayAlert renders as a native sheet (XCUIElementTypeSheet).
			// Target the "OK" button only within a sheet or alert context so we do not
			// accidentally click a navigation button on the underlying page.
			try
			{
				Driver.FindElement(OpenQA.Selenium.By.XPath(
					"//XCUIElementTypeSheet//XCUIElementTypeButton[@label='OK']" +
					" | //XCUIElementTypeAlert//XCUIElementTypeButton[@label='OK']"
				)).Click();
			}
			catch
			{
				// No sheet/alert dialog found (e.g. DISABLE_FIREBASE builds skip the dialog).
			}
		}

		return new SelectTrainPageObject(Driver);
	}
}
