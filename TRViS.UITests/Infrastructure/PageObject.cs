using OpenQA.Selenium.Appium;

namespace TRViS.UITests.Infrastructure;

public abstract class PageObject
{
	protected AppiumDriver Driver { get; }

	protected PageObject(AppiumDriver driver)
	{
		Driver = driver;
	}

	protected AppiumElement FindByAutomationId(string automationId)
		=> Driver.FindElement(MobileBy.AccessibilityId(automationId));

	public AppiumElement WaitForElement(string automationId, TimeSpan? timeout = null)
	{
		var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(
			Driver,
			timeout ?? TimeSpan.FromSeconds(30));

		return (AppiumElement)wait.Until(d =>
		{
			try
			{
				var element = d.FindElement(MobileBy.AccessibilityId(automationId));
				return element.Displayed ? element : null!;
			}
			catch (OpenQA.Selenium.NoSuchElementException)
			{
				return null!;
			}
		});
	}
}
