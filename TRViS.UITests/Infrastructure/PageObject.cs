using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace TRViS.UITests.Infrastructure;

public abstract class PageObject
{
	protected AppiumDriver Driver { get; }

	/// <summary>
	/// True when running on Android. On Android, MAUI maps AutomationId to
	/// the view's resource-id (found via By.Id), not to content-description
	/// (found via MobileBy.AccessibilityId).
	/// </summary>
	protected bool IsAndroid { get; }

	/// <summary>
	/// True when running on Windows. WinUI 3 surfaces a MAUI <c>ContentView</c>'s
	/// AutomationId as a non-control Pane element that Appium's AccessibilityId
	/// search does not always reach — callers may need an XPath/Name fallback.
	/// </summary>
	protected bool IsWindows { get; }

	protected PageObject(AppiumDriver driver)
	{
		Driver = driver;
		IsAndroid = driver is AndroidDriver;
		IsWindows = driver is WindowsDriver;
	}

	/// <summary>
	/// Finds an element by MAUI AutomationId. Uses By.Id on Android
	/// (resource-id) and MobileBy.AccessibilityId on all other platforms.
	/// </summary>
	protected AppiumElement FindByAutomationId(string automationId)
		=> (AppiumElement)Driver.FindElement(AutomationIdLocator(automationId));

	/// <summary>
	/// Returns the correct locator strategy for the current platform.
	/// </summary>
	protected By AutomationIdLocator(string automationId)
		=> IsAndroid ? By.Id(automationId) : MobileBy.AccessibilityId(automationId);

	public AppiumElement WaitForElement(string automationId, TimeSpan? timeout = null)
	{
		var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(
			Driver,
			timeout ?? TimeSpan.FromSeconds(30));

		var locator = AutomationIdLocator(automationId);

		// Suppress implicit wait so the polling loop fires every ~500 ms instead
		// of every 10 s. Restoring in finally keeps the session state consistent.
		Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
		try
		{
			return (AppiumElement)wait.Until(d =>
			{
				try
				{
					var element = d.FindElement(locator);
					return element.Displayed ? element : null!;
				}
				catch (NoSuchElementException)
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
