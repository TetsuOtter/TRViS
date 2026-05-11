using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Support.UI;
using TRViS.UITests.Infrastructure;

namespace TRViS.UITests.Pages;

/// <summary>
/// Page object for the horizontal-timetable Shell route. The page is a single
/// AppBar + WebView; the WebView surfaces under a stable AutomationId on every
/// platform (the WebView itself, not its rendered HTML).
/// </summary>
public class HorizontalTimetablePageObject : PageObject
{
	public HorizontalTimetablePageObject(AppiumDriver driver) : base(driver) { }

	/// <summary>
	/// Finds the page's WebView. iOS/Windows map MAUI's AutomationId onto the
	/// native WebView control's accessibility identifier, so the standard
	/// AccessibilityId lookup works. On Android the MAUI WebView handler does
	/// not forward AutomationId to <c>resource-id</c>, so <c>By.Id</c> never
	/// hits — we fall back to locating the sole <c>android.webkit.WebView</c>
	/// node on the page.
	/// </summary>
	public AppiumElement WebView => IsAndroid
		? WaitForAndroidWebView()
		: WaitForElement(AutomationIds.HorizontalTimetable.WebView);

	private AppiumElement WaitForAndroidWebView()
	{
		var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(30));
		var locator = By.ClassName("android.webkit.WebView");
		Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
		try
		{
			return (AppiumElement)wait.Until(d =>
			{
				try
				{
					var el = d.FindElement(locator);
					return el.Displayed ? el : null!;
				}
				catch (NoSuchElementException) { return null!; }
			});
		}
		finally
		{
			Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
		}
	}

	public bool IsDisplayed()
	{
		try
		{
			return WebView.Displayed;
		}
		catch (NoSuchElementException)
		{
			return false;
		}
	}
}
