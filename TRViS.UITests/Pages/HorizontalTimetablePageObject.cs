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
	/// Finds the page's WebView. iOS/macOS map MAUI's AutomationId onto the
	/// native WebView control's accessibility identifier, so the standard
	/// AccessibilityId lookup works. Android and Windows do not forward the
	/// MAUI WebView's AutomationId to UIA / resource-id (the WinUI handler
	/// leaves <c>Microsoft.UI.Xaml.Controls.WebView2.AutomationId</c> blank,
	/// the UIA2 handler does not surface <c>resource-id</c> at all), so we
	/// fall back to locating the sole WebView node by its platform class
	/// name. The page hosts a single WebView, so this is unambiguous.
	/// </summary>
	public AppiumElement WebView => (IsAndroid || IsWindows)
		? WaitForWebViewByClassName()
		: WaitForElement(AutomationIds.HorizontalTimetable.WebView);

	private AppiumElement WaitForWebViewByClassName()
	{
		// On Windows we use XPath because Selenium .NET's By.ClassName maps to
		// the "css selector" strategy, which WinAppDriver rejects with
		// InvalidSelectorException. The Appium UIA2 (Android) server tolerates
		// it, so By.ClassName still works there.
		var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(30));
		var locator = IsWindows
			? By.XPath("//*[@ClassName='Microsoft.UI.Xaml.Controls.WebView2']")
			: By.ClassName("android.webkit.WebView");
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
