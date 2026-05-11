using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
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

	public AppiumElement WebView => WaitForElement(AutomationIds.HorizontalTimetable.WebView);

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
