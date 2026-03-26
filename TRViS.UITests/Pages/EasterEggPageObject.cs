using OpenQA.Selenium.Appium;
using TRViS.UITests.Infrastructure;

namespace TRViS.UITests.Pages;

public class EasterEggPageObject : PageObject
{
	public EasterEggPageObject(AppiumDriver driver) : base(driver) { }

	public AppiumElement ReloadSavedButton => WaitForElement(AutomationIds.Settings.ReloadSavedButton);
	public AppiumElement SaveButton => FindByAutomationId(AutomationIds.Settings.SaveButton);

	public bool IsDisplayed()
	{
		try
		{
			return ReloadSavedButton.Displayed;
		}
		catch (OpenQA.Selenium.NoSuchElementException)
		{
			return false;
		}
	}
}
