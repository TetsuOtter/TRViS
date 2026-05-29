using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using TRViS.UITests.Infrastructure;

namespace TRViS.UITests.Pages;

/// <summary>
/// Page object for OriginalTimetableV4Page ("Next Big"). Hero card + MiniList
/// layout. The page is "rendered" when the TrainStripe, Hero, MiniList, or
/// EmptyState anchor is reachable (both layout variants share the strip).
/// </summary>
public class OriginalTimetableV4PageObject : PageObject
{
	public OriginalTimetableV4PageObject(AppiumDriver driver) : base(driver) { }

	public AppiumElement TrainStripe
		=> WaitForElement(AutomationIds.OriginalTimetable.V4.TrainStripe, TimeSpan.FromSeconds(30));
	public AppiumElement TrainNumber
		=> WaitForElement(AutomationIds.OriginalTimetable.V4.TrainStripeTrainNumber, TimeSpan.FromSeconds(30));
	public AppiumElement Hero
		=> FindByAutomationId(AutomationIds.OriginalTimetable.V4.Hero);
	public AppiumElement EmptyState
		=> WaitForElement(AutomationIds.OriginalTimetable.V4.EmptyState, TimeSpan.FromSeconds(30));

	/// <summary>
	/// Waits until V4 has finished its first render. Any of TrainStripe / Hero
	/// / MiniList / EmptyState is sufficient evidence the page reached a paint.
	/// </summary>
	public bool WaitForRendered(double timeoutSeconds = 30)
	{
		var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
		Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
		try
		{
			string[] anchors = new[]
			{
				AutomationIds.OriginalTimetable.V4.Root,
				AutomationIds.OriginalTimetable.V4.TrainStripe,
				AutomationIds.OriginalTimetable.V4.Hero,
				AutomationIds.OriginalTimetable.V4.MiniList,
				AutomationIds.OriginalTimetable.V4.CompactMiniList,
				AutomationIds.OriginalTimetable.V4.EmptyState,
				AutomationIds.OriginalTimetable.V4.CompactEmptyState,
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
		=> PollDisplayed(AutomationIds.OriginalTimetable.V4.EmptyState, timeoutSeconds)
			|| PollDisplayed(AutomationIds.OriginalTimetable.V4.CompactEmptyState, timeoutSeconds);

	/// <summary>True when the persistent TrainStripe header is visible.</summary>
	public bool IsTrainStripeVisible(double timeoutSeconds = 3)
		=> PollDisplayed(AutomationIds.OriginalTimetable.V4.TrainStripe, timeoutSeconds);

	/// <summary>True when the Hero card is rendered (i.e. there's a next-arrival row).</summary>
	public bool IsHeroVisible(double timeoutSeconds = 3)
		=> PollDisplayed(AutomationIds.OriginalTimetable.V4.Hero, timeoutSeconds);

	/// <summary>True when the tablet layout grid is in the tree.</summary>
	public bool IsTabletLayoutPresent(double timeoutSeconds = 3)
		=> PollDisplayed(AutomationIds.OriginalTimetable.V4.TabletGrid, timeoutSeconds);

	/// <summary>True when the compact layout grid is in the tree.</summary>
	public bool IsCompactLayoutPresent(double timeoutSeconds = 3)
		=> PollDisplayed(AutomationIds.OriginalTimetable.V4.CompactGrid, timeoutSeconds);

	/// <summary>
	/// Reads the TrainNumber label from the TrainStripe header. Empty when not
	/// present or when no train is active.
	/// </summary>
	public string GetTrainNumber()
	{
		try
		{
			return TrainNumber.Text ?? string.Empty;
		}
		catch (NoSuchElementException)
		{
			return string.Empty;
		}
	}

	/// <summary>True when the Hero card's MarkerBadge is currently visible.</summary>
	public bool IsHeroMarkerBadgeVisible(double timeoutSeconds = 3)
		=> PollDisplayed(AutomationIds.OriginalTimetable.V4.HeroMarkerBadge, timeoutSeconds);
}
