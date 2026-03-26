using OpenQA.Selenium.Appium;
using TRViS.UITests.Infrastructure;

namespace TRViS.UITests.Pages;

public class DTACViewHostPageObject : PageObject
{
	public DTACViewHostPageObject(AppiumDriver driver) : base(driver) { }

	public AppiumElement MenuButton => WaitForElement(AutomationIds.DTAC.MenuButton);
	public AppiumElement TimeLabel => FindByAutomationId(AutomationIds.DTAC.TimeLabel);
	public AppiumElement TitleLabel => FindByAutomationId(AutomationIds.DTAC.TitleLabel);
	public AppiumElement TabHako => FindByAutomationId(AutomationIds.DTAC.TabHako);
	public AppiumElement TabTimetable => FindByAutomationId(AutomationIds.DTAC.TabTimetable);
	public AppiumElement TabWorkAffix => FindByAutomationId(AutomationIds.DTAC.TabWorkAffix);

	public bool IsDisplayed()
	{
		try
		{
			return MenuButton.Displayed;
		}
		catch (OpenQA.Selenium.NoSuchElementException)
		{
			return false;
		}
	}
}
