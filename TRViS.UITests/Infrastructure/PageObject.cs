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

		// Suppress implicit wait so the polling loop fires every ~500 ms instead
		// of every 10 s. Restoring in finally keeps the session state consistent.
		Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
		try
		{
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
		finally
		{
			Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
		}
	}
}
