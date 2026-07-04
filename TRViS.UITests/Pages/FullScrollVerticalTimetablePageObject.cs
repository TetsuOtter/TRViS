using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using TRViS.UITests.Infrastructure;

namespace TRViS.UITests.Pages;

/// <summary>
/// Page object for the separated full-scroll D-TAC Shell route (#155): an
/// AppBar (with a back button) above the full vertical timetable wrapped in a
/// single outer ScrollView. Reached by tapping
/// <see cref="AutomationIds.DTAC.FullScrollButton"/> (iPhone idiom only).
/// </summary>
public class FullScrollVerticalTimetablePageObject : PageObject
{
	public FullScrollVerticalTimetablePageObject(AppiumDriver driver) : base(driver) { }

	/// <summary>
	/// AppBar back button — unique to this page, so a reliable "are we here?"
	/// signal. The shared timetable scroll container keeps the
	/// DTAC.TimetableScrollView id (same as the embedded variant) and is not a
	/// distinguishing marker.
	/// </summary>
	public AppiumElement BackButton => FindByAutomationId(AutomationIds.FullScroll.BackButton);

	public bool IsDisplayed()
	{
		try
		{
			return BackButton.Displayed;
		}
		catch (NoSuchElementException)
		{
			return false;
		}
	}

	/// <summary>
	/// Taps the AppBar back button so the page pops back to ViewHost (a
	/// flyout-aware Shell root). The full-scroll page is a Shell push, so the
	/// flyout is not reachable from it — getting back is the prerequisite for
	/// the next fixture's NavigateToHome in shared-session runs.
	/// </summary>
	public DTACViewHostPageObject TapBack()
	{
		BackButton.Click();
		return new DTACViewHostPageObject(Driver);
	}
}
