namespace TRViS.UITests.Pages;

public class OriginalTimetableSimplePageObject : PageObject
{
	public OriginalTimetableSimplePageObject(AppiumDriver driver) : base(driver) { }

	/// <summary>
	/// Returns true when the page's main label is visible.
	/// A blank (white) screen after navigation indicates MAUI #16927 is not fixed.
	/// </summary>
	public bool IsDisplayed()
	{
		try
		{
			return WaitForElement(AutomationIds.OriginalTimetable.Simple.PageLabel, TimeSpan.FromSeconds(10)).Displayed;
		}
		catch
		{
			return false;
		}
	}
}
