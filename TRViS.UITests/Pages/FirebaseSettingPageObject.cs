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

	public bool IsDisplayed()
	{
		try
		{
			return Title.Displayed;
		}
		catch (OpenQA.Selenium.NoSuchElementException)
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
			// Fallback: driver does not support the alert endpoint (e.g. mac2).
			// Look for an "OK" button rendered as a native sheet button.
			try
			{
				Driver.FindElement(MobileBy.AccessibilityId("OK")).Click();
			}
			catch
			{
				// No dismissable alert found; continue.
			}
		}

		return new SelectTrainPageObject(Driver);
	}
}
