using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using TRViS.UITests.Infrastructure;

namespace TRViS.UITests.Pages;

/// <summary>
/// Page object for OriginalTimetableV1Page ("Modern Classic"). V1 now ships
/// both a tablet layout (width &gt;= 600pt; AutomationIds under
/// <c>OriginalTimetable.V1.*</c> — Header / EmptyState / RowsList / Row.*)
/// and a real compact layout (narrow viewports; AutomationIds under
/// <c>OriginalTimetable.V1.Compact.*</c> — CompactRoot / CompactHeader /
/// CompactEmptyState / CompactRowsList). Marker chooser popover ids are
/// shared across all variants under <c>OriginalTimetable.MarkerPopover.*</c>.
/// </summary>
public class OriginalTimetableV1PageObject : PageObject
{
	public OriginalTimetableV1PageObject(AppiumDriver driver) : base(driver) { }

	public AppiumElement Header
		=> WaitForElement(AutomationIds.OriginalTimetable.V1.Header, TimeSpan.FromSeconds(30));
	public AppiumElement HeaderTrainNumber
		=> WaitForElement(AutomationIds.OriginalTimetable.V1.HeaderTrainNumber, TimeSpan.FromSeconds(30));
	public AppiumElement EmptyState
		=> WaitForElement(AutomationIds.OriginalTimetable.V1.EmptyState, TimeSpan.FromSeconds(30));
	public AppiumElement RowsList
		=> FindByAutomationId(AutomationIds.OriginalTimetable.V1.RowsList);

	/// <summary>
	/// Waits until V1 has finished its first render. The page is "rendered"
	/// when ANY of the tablet- or compact-layout anchors (Header / EmptyState /
	/// RowsList) resolve. Phone-class viewports (&lt;600pt — e.g. the trvis-test
	/// AVD at 411dp) hide TabletGrid entirely, so probing tablet-only ids
	/// would never match. A 30 s budget absorbs flyout-navigation latency on
	/// slow simulators.
	/// </summary>
	public bool WaitForRendered(double timeoutSeconds = 30)
	{
		var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
		Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
		try
		{
			string[] anchors = new[]
			{
				AutomationIds.OriginalTimetable.V1.Root,
				AutomationIds.OriginalTimetable.V1.Header,
				AutomationIds.OriginalTimetable.V1.EmptyState,
				AutomationIds.OriginalTimetable.V1.RowsList,
				AutomationIds.OriginalTimetable.V1.CompactRoot,
				AutomationIds.OriginalTimetable.V1.CompactHeader,
				AutomationIds.OriginalTimetable.V1.CompactEmptyState,
				AutomationIds.OriginalTimetable.V1.CompactRowsList,
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
	/// Returns the train-number label text from whichever sticky header is
	/// currently in the tree (tablet or compact). Empty when no train is
	/// active. MAUI Label text surfaces as the element's Text on Appium drivers.
	/// </summary>
	public string GetTrainNumber()
	{
		foreach (var id in new[]
		{
			AutomationIds.OriginalTimetable.V1.HeaderTrainNumber,
			AutomationIds.OriginalTimetable.V1.CompactHeaderTrainNumber,
		})
		{
			try
			{
				var els = Driver.FindElements(AutomationIdLocator(id));
				if (els.Count > 0)
					return els[0].Text ?? string.Empty;
			}
			catch { }
		}
		return string.Empty;
	}

	/// <summary>
	/// True when either the tablet or compact EmptyState Label
	/// ("列車を選択してください") is currently visible.
	/// </summary>
	public bool IsEmptyStateVisible(double timeoutSeconds = 3)
		=> PollDisplayed(AutomationIds.OriginalTimetable.V1.EmptyState, timeoutSeconds)
		|| PollDisplayed(AutomationIds.OriginalTimetable.V1.CompactEmptyState, timeoutSeconds);

	/// <summary>
	/// True when either the tablet or compact sticky header (train number) is
	/// currently visible — i.e. V1 has an ActiveTrain.
	/// </summary>
	public bool IsHeaderVisible(double timeoutSeconds = 3)
		=> PollDisplayed(AutomationIds.OriginalTimetable.V1.HeaderTrainNumber, timeoutSeconds)
		|| PollDisplayed(AutomationIds.OriginalTimetable.V1.CompactHeaderTrainNumber, timeoutSeconds);

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
