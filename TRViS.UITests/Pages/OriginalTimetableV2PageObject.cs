using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using TRViS.UITests.Infrastructure;

namespace TRViS.UITests.Pages;

/// <summary>
/// Page object for OriginalTimetableV2Page ("Card Stack"). Tablet layout
/// (width &gt;= 600pt) and compact layout are both implemented; the page is
/// "rendered" when EITHER the tablet sticky header, the compact header, or
/// either layout's empty-state Label is on screen.
/// </summary>
public class OriginalTimetableV2PageObject : PageObject
{
	public OriginalTimetableV2PageObject(AppiumDriver driver) : base(driver) { }

	public AppiumElement Header
		=> WaitForElement(AutomationIds.OriginalTimetable.V2.Header, TimeSpan.FromSeconds(30));
	public AppiumElement EmptyState
		=> WaitForElement(AutomationIds.OriginalTimetable.V2.EmptyState, TimeSpan.FromSeconds(30));
	public AppiumElement RowsList
		=> FindByAutomationId(AutomationIds.OriginalTimetable.V2.RowsList);

	/// <summary>
	/// Waits until V2 has finished its first render. The page is "rendered"
	/// when any of its tablet/compact header or empty-state anchors are
	/// reachable in the accessibility tree.
	/// </summary>
	public bool WaitForRendered(double timeoutSeconds = 30)
	{
		var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
		Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
		try
		{
			string[] anchors = new[]
			{
				AutomationIds.OriginalTimetable.V2.Header,
				AutomationIds.OriginalTimetable.V2.EmptyState,
				AutomationIds.OriginalTimetable.V2.CompactHeader,
				AutomationIds.OriginalTimetable.V2.CompactEmptyState,
			};
			while (DateTime.UtcNow < deadline)
			{
				foreach (var id in anchors)
				{
					try
					{
						if (Driver.FindElements(AutomationIdLocator(id)).Count > 0)
							return true;
					}
					catch { }
				}
				Thread.Sleep(200);
			}
			return false;
		}
		finally
		{
			Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
		}
	}

	/// <summary>
	/// True when an empty-state Label (tablet OR compact) is currently visible.
	/// </summary>
	public bool IsEmptyStateVisible(double timeoutSeconds = 3)
		=> PollDisplayed(AutomationIds.OriginalTimetable.V2.EmptyState, timeoutSeconds)
			|| PollDisplayed(AutomationIds.OriginalTimetable.V2.CompactEmptyState, timeoutSeconds);

	/// <summary>
	/// True when the tablet sticky header is currently visible — i.e. V2 is on
	/// the tablet layout AND has an ActiveTrain.
	/// </summary>
	public bool IsHeaderVisible(double timeoutSeconds = 3)
		=> PollDisplayed(AutomationIds.OriginalTimetable.V2.Header, timeoutSeconds);

	/// <summary>True when the tablet layout grid is in the tree.</summary>
	public bool IsTabletLayoutPresent(double timeoutSeconds = 3)
		=> PollDisplayed(AutomationIds.OriginalTimetable.V2.TabletGrid, timeoutSeconds);

	/// <summary>True when the compact layout grid is in the tree.</summary>
	public bool IsCompactLayoutPresent(double timeoutSeconds = 3)
		=> PollDisplayed(AutomationIds.OriginalTimetable.V2.CompactGrid, timeoutSeconds);

	/// <summary>
	/// True when the marker badge for <paramref name="rowId"/> is visible.
	/// </summary>
	public bool IsMarkerBadgeVisible(string rowId, double timeoutSeconds = 3)
		=> PollDisplayed(AutomationIds.OriginalTimetable.V2.MarkerBadgeFor(rowId), timeoutSeconds);
}
