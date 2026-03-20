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
		SaveButton.Click();

		// Accept the confirmation dialog
		try
		{
			var alert = Driver.SwitchTo().Alert();
			alert.Accept();
		}
		catch (OpenQA.Selenium.NoAlertPresentException)
		{
			// On some platforms the alert may already be dismissed
		}

		return new SelectTrainPageObject(Driver);
	}
}
