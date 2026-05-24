using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using TRViS.UITests.Infrastructure;

namespace TRViS.UITests.Pages;

/// <summary>
/// Page object for OriginalTimetableV1Page ("Modern Classic"). Phase 1: tablet
/// layout only (width &gt;= 600pt). On narrower viewports the V1 page renders a
/// CompactPlaceholder Label; tests that need the tablet layout assert that
/// either TabletGrid is visible or the EmptyState/RowsList is reachable, and
/// skip the marker-cycle assertions on phone widths.
/// </summary>
public class OriginalTimetableV1PageObject : PageObject
{
	public OriginalTimetableV1PageObject(AppiumDriver driver) : base(driver) { }

	public AppiumElement Header
		=> WaitForElement(AutomationIds.OriginalTimetable.V1.Header, TimeSpan.FromSeconds(30));
	public AppiumElement HeaderTrainNumber
		=> WaitForElement(AutomationIds.OriginalTimetable.V1.HeaderTrainNumber, TimeSpan.FromSeconds(30));
	public AppiumElement HeaderDestination
		=> FindByAutomationId(AutomationIds.OriginalTimetable.V1.HeaderDestination);
	public AppiumElement EmptyState
		=> WaitForElement(AutomationIds.OriginalTimetable.V1.EmptyState, TimeSpan.FromSeconds(30));
	public AppiumElement RowsList
		=> FindByAutomationId(AutomationIds.OriginalTimetable.V1.RowsList);

	/// <summary>
	/// Waits until V1 has finished its first render. The page is "rendered"
	/// when either the sticky train-info header is on screen (active-train
	/// state) or the empty-state Label is on screen (no-train state). A 30 s
	/// budget absorbs flyout-navigation latency on slow simulators.
	/// </summary>
	public bool WaitForRendered(double timeoutSeconds = 30)
	{
		var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
		Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
		try
		{
			while (DateTime.UtcNow < deadline)
			{
				try
				{
					if (Driver.FindElements(AutomationIdLocator(AutomationIds.OriginalTimetable.V1.Header)).Count > 0)
						return true;
				}
				catch { }
				try
				{
					if (Driver.FindElements(AutomationIdLocator(AutomationIds.OriginalTimetable.V1.EmptyState)).Count > 0)
						return true;
				}
				catch { }
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
	/// Returns the train-number label text from the sticky header. Empty when
	/// no train is active. MAUI Label text surfaces as the element's Text on
	/// Appium drivers.
	/// </summary>
	public string GetTrainNumber()
	{
		try
		{
			return HeaderTrainNumber.Text ?? string.Empty;
		}
		catch (NoSuchElementException)
		{
			return string.Empty;
		}
	}

	/// <summary>
	/// True when the EmptyState Label ("列車を選択してください") is currently visible.
	/// </summary>
	public bool IsEmptyStateVisible(double timeoutSeconds = 3)
		=> PollDisplayed(AutomationIds.OriginalTimetable.V1.EmptyState, timeoutSeconds);

	/// <summary>
	/// True when the sticky header (train number) is currently visible — i.e.
	/// V1 has an ActiveTrain.
	/// </summary>
	public bool IsHeaderVisible(double timeoutSeconds = 3)
		=> PollDisplayed(AutomationIds.OriginalTimetable.V1.HeaderTrainNumber, timeoutSeconds);

	/// <summary>
	/// Existence probe for a specific row by its TimetableRow.Id.
	/// </summary>
	public bool IsRowDisplayed(string rowId, double timeoutSeconds = 3)
		=> PollDisplayed(AutomationIds.OriginalTimetable.V1.RowFor(rowId), timeoutSeconds);

	/// <summary>
	/// True when the marker badge for <paramref name="rowId"/> is visible.
	/// The badge is hidden (HasMarker=false) when Marker == None, so this
	/// returning true is evidence the row has been marked.
	/// </summary>
	public bool IsMarkerBadgeVisible(string rowId, double timeoutSeconds = 3)
		=> PollDisplayed(AutomationIds.OriginalTimetable.V1.MarkerBadgeFor(rowId), timeoutSeconds);

	/// <summary>
	/// Taps the UI_TEST seam button that drives the same OnCycleMarker handler
	/// the SwipeView's Command binding points at, targeting the first normal
	/// (non-section-break) row. Keeps the View→VM marker pipeline covered
	/// without depending on simulated swipe gesture reliability.
	/// </summary>
	public void TapCycleMarkerRow0ForTesting()
		=> FindByAutomationId(AutomationIds.OriginalTimetable.V1.TestCycleMarkerRow0Button).Click();

	/// <summary>Inverse of <see cref="TapCycleMarkerRow0ForTesting"/>.</summary>
	public void TapClearMarkerRow0ForTesting()
		=> FindByAutomationId(AutomationIds.OriginalTimetable.V1.TestClearMarkerRow0Button).Click();
}
