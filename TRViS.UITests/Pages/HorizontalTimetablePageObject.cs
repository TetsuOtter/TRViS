using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Support.UI;
using TRViS.UITests.Infrastructure;

namespace TRViS.UITests.Pages;

/// <summary>
/// Page object for the horizontal-timetable Shell route. The page is a single
/// AppBar + WebView (wrapped in a Grid that carries the AutomationId).
/// </summary>
public class HorizontalTimetablePageObject : PageObject
{
	public HorizontalTimetablePageObject(AppiumDriver driver) : base(driver) { }

	/// <summary>
	/// Finds the page's WebView. On iOS / macOS the wrapper Grid surfaces the
	/// MAUI AutomationId via the native accessibility identifier, so the
	/// standard AccessibilityId lookup is enough. Android and Windows are
	/// problematic in different ways: UIA2 does not forward AutomationId to
	/// <c>resource-id</c>, and WinUI 3 exposes the Grid as a non-control
	/// Pane that <c>AccessibilityId</c> cannot reach. On both we fall back
	/// to locating the sole native WebView node by its class name —
	/// <c>android.webkit.WebView</c> on Android and
	/// <c>Microsoft.UI.Xaml.Controls.WebView2</c> on Windows.
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

	/// <summary>
	/// Taps the AppBar back button so the page pops back to DTAC. The
	/// HorizontalTimetable page is a Shell pushed page (not a flyout root),
	/// so the Shell flyout is not reachable here; getting back to a
	/// flyout-aware page is the prerequisite for the next fixture's
	/// <c>NavigateToHome</c> to work in shared-session runs. Returns the
	/// caller-friendly DTAC page object that the back navigation lands on.
	/// </summary>
	public DTACViewHostPageObject TapBack()
	{
		FindByAutomationId(AutomationIds.HorizontalTimetable.BackButton).Click();
		return new DTACViewHostPageObject(Driver);
	}
}
