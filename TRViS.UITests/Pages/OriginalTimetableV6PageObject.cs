using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using TRViS.UITests.Infrastructure;

namespace TRViS.UITests.Pages;

/// <summary>
/// Page object for OriginalTimetableV6Page ("Bold Editorial"). Masthead +
/// TrainStripe + PastChips + CurrentBlock + UpcomingList. Both tablet
/// (&gt;=600pt) and compact layouts implemented. The page is "rendered" when
/// the Masthead, CurrentBlock, EmptyState, or their compact mirrors are
/// reachable.
/// </summary>
public class OriginalTimetableV6PageObject : PageObject
{
	public OriginalTimetableV6PageObject(AppiumDriver driver) : base(driver) { }

	public AppiumElement Masthead
		=> WaitForElement(AutomationIds.OriginalTimetable.V6.Masthead, TimeSpan.FromSeconds(30));
	public AppiumElement CurrentBlock
		=> WaitForElement(AutomationIds.OriginalTimetable.V6.CurrentBlock, TimeSpan.FromSeconds(30));
	public AppiumElement CurrentStation
		=> WaitForElement(AutomationIds.OriginalTimetable.V6.CurrentBlockStationName, TimeSpan.FromSeconds(30));
	public AppiumElement EmptyState
		=> WaitForElement(AutomationIds.OriginalTimetable.V6.EmptyState, TimeSpan.FromSeconds(30));

	/// <summary>
	/// Waits until V6 has finished its first render. Any of the masthead /
	/// current-block / empty-state anchors (tablet or compact) is sufficient
	/// evidence of paint.
	/// </summary>
	public bool WaitForRendered(double timeoutSeconds = 30)
	{
		var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
		Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
		try
		{
			string[] anchors = new[]
			{
				AutomationIds.OriginalTimetable.V6.Root,
				AutomationIds.OriginalTimetable.V6.Masthead,
				AutomationIds.OriginalTimetable.V6.CurrentBlock,
				AutomationIds.OriginalTimetable.V6.EmptyState,
				AutomationIds.OriginalTimetable.V6.CompactMasthead,
				AutomationIds.OriginalTimetable.V6.CompactCurrentBlock,
				AutomationIds.OriginalTimetable.V6.CompactEmptyState,
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

	/// <summary>True when an EmptyState placeholder is visible (tablet OR compact).</summary>
	public bool IsEmptyStateVisible(double timeoutSeconds = 3)
		=> PollDisplayed(AutomationIds.OriginalTimetable.V6.EmptyState, timeoutSeconds)
			|| PollDisplayed(AutomationIds.OriginalTimetable.V6.CompactEmptyState, timeoutSeconds);

	/// <summary>True when the tablet CurrentBlock is visible (active-train state).</summary>
	public bool IsCurrentBlockVisible(double timeoutSeconds = 3)
		=> PollDisplayed(AutomationIds.OriginalTimetable.V6.CurrentBlock, timeoutSeconds);

	/// <summary>True when the tablet layout grid is in the tree.</summary>
	public bool IsTabletLayoutPresent(double timeoutSeconds = 3)
		=> PollDisplayed(AutomationIds.OriginalTimetable.V6.TabletGrid, timeoutSeconds);

	/// <summary>True when the compact layout grid is in the tree.</summary>
	public bool IsCompactLayoutPresent(double timeoutSeconds = 3)
		=> PollDisplayed(AutomationIds.OriginalTimetable.V6.CompactGrid, timeoutSeconds);

	/// <summary>
	/// Reads the current-block's station-name label. Empty when the label is
	/// not in the tree (e.g. no active train).
	/// </summary>
	public string GetCurrentStation()
	{
		try
		{
			return CurrentStation.Text ?? string.Empty;
		}
		catch (NoSuchElementException)
		{
			return string.Empty;
		}
	}

	/// <summary>True when the CurrentBlock's MarkerBadge is currently visible (tablet layout).</summary>
	public bool IsCurrentBlockMarkerBadgeVisible(double timeoutSeconds = 3)
		=> PollDisplayed(AutomationIds.OriginalTimetable.V6.CurrentBlockMarkerBadge, timeoutSeconds);
}
