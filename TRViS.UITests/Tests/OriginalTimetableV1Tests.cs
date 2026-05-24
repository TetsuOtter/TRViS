using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// E2E tests for the Phase 1 V1 (Modern Classic) Original Timetable page.
/// Coverage:
///  1) Flyout navigation reaches V1 from StartHome.
///  2) Empty-state Label shows when no train has been selected.
///  3) Sticky train-info header surfaces a TrainNumber after a Work has been
///     committed via AutoOpenForTesting (cascades through SelectionManager →
///     AppViewModel.SelectedTrainData → OriginalTimetableViewModel.ActiveTrain).
///  4) Marker-cycle pipeline: tapping the UI_TEST seam button drives the same
///     OnCycleMarker handler the SwipeView SwipeItem Command binds to, and the
///     row's MarkerBadge transitions from hidden → visible. Re-tapping the
///     clear seam flips it back. Covers the View→VM marker plumbing without
///     depending on cross-platform SwipeView gesture reliability.
///
/// The fixture cold-launches a fresh Appium session per test (no shared
/// session) — V1's MarkersVersion / CurIdxVersion state is non-trivial to
/// reset between tests via in-app seams, so a clean process avoids the cost
/// of writing yet more reset plumbing.
/// </summary>
[TestFixture]
[Infrastructure.RetryAllTests(2)] // see AppLaunchTests for rationale
public class OriginalTimetableV1Tests : BaseUITest
{
	private StartHomePageObject _startHomePage = null!;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();
		_startHomePage = new StartHomePageObject(Driver);
		Assert.That(_startHomePage.IsDisplayed(), Is.True,
			"StartHome should be visible after launch.");
		_startHomePage.AcceptPrivacyPolicyIfNeeded();
	}

	/// <summary>
	/// Navigates to V1 via the Shell flyout from StartHome (no train committed).
	/// Asserts the page reaches a renderable state — either the empty-state Label
	/// (no ActiveTrain) or the sticky header (some prior session leaked an
	/// ActiveTrain into the singleton VM). Either path is acceptable; the
	/// assertion guards against a navigation-failed / page-blank state.
	/// </summary>
	[Test]
	public void V1Page_OpensFromFlyout_Renders()
	{
		var v1 = new AppShellPage(Driver).NavigateToOriginalTimetableV1();

		Assert.That(v1.WaitForRendered(timeoutSeconds: 30), Is.True,
			"V1 page should render (either the sticky train-info header or the empty-state label).");
	}

	/// <summary>
	/// With no Work/Train committed, V1 must render the empty-state Label so
	/// the user sees a clear "select a train" affordance instead of a blank page.
	/// </summary>
	[Test]
	public void V1Page_EmptyState_WhenNoTrainSelected()
	{
		var v1 = new AppShellPage(Driver).NavigateToOriginalTimetableV1();
		Assert.That(v1.WaitForRendered(timeoutSeconds: 30), Is.True);

		Assert.That(v1.IsEmptyStateVisible(timeoutSeconds: 10), Is.True,
			"EmptyState Label should be visible when no train has been selected.");
	}

	/// <summary>
	/// After committing a Work via AutoOpenForTesting (cascades to ActiveTrain),
	/// navigating to V1 should show the sticky header with a non-empty
	/// TrainNumber, not the empty-state.
	/// </summary>
	[Test]
	public void V1Page_AfterTrainSelected_ShowsHeaderWithTrainNumber()
	{
		_startHomePage.LoadSample();
		_startHomePage.WaitForElement(AutomationIds.StartHome.WorkGroupList);
		var dtac = _startHomePage.AutoOpenForTesting();
		Assert.That(dtac.IsDisplayed(), Is.True, "DTAC should reach displayed state after AutoOpen.");

		var v1 = new AppShellPage(Driver).NavigateToOriginalTimetableV1();
		Assert.That(v1.WaitForRendered(timeoutSeconds: 30), Is.True);

		Assert.That(v1.IsHeaderVisible(timeoutSeconds: 15), Is.True,
			"V1 sticky header should be visible once a Work has been committed.");
		Assert.That(v1.GetTrainNumber(), Is.Not.Empty,
			"Sticky header TrainNumber label should be non-empty after the cascade picks a train.");
	}

	/// <summary>
	/// Marker pipeline: with an active train, tapping the UI_TEST CycleMarker seam
	/// (same handler as the SwipeItem Command binding) should make the row's
	/// MarkerBadge visible. Tapping ClearMarker should hide it again.
	///
	/// The seam targets the first normal (non-section-break) row, so we only
	/// need to know the badge ID derives from RowAutomationId — we read the
	/// ActiveTrain.Rows[0].Id by inspecting the visible row.
	/// </summary>
	[Test]
	public void V1Page_CycleMarker_AddsAndClearsMarkerBadge()
	{
		_startHomePage.LoadSample();
		_startHomePage.WaitForElement(AutomationIds.StartHome.WorkGroupList);
		_ = _startHomePage.AutoOpenForTesting();

		var v1 = new AppShellPage(Driver).NavigateToOriginalTimetableV1();
		Assert.That(v1.WaitForRendered(timeoutSeconds: 30), Is.True);
		Assert.That(v1.IsHeaderVisible(timeoutSeconds: 15), Is.True);

		// Sample data: first train's first non-info row Id is stable. Sample
		// fixture row layout (TimetableRows[0] etc.) is set by LoaderJson on
		// load — the Id values aren't asserted here directly because the
		// MarkerBadge AutomationId is what the test branches on, and the
		// CycleMarker seam targets "the first normal row" already. We assert
		// "*some* MarkerBadge becomes visible" by probing the seam-targeted
		// row through the same lookup path. Since the seam knows the Id but
		// the test does not, we cycle and then check that *any* MarkerBadge
		// AutomationId becomes visible — using the page's RowsList descendants.

		v1.TapCycleMarkerRow0ForTesting();
		Thread.Sleep(500);

		Assert.That(AnyMarkerBadgeVisibleAfterCycle(v1), Is.True,
			"Cycling the first-row marker should make a MarkerBadge become visible.");

		v1.TapClearMarkerRow0ForTesting();
		Thread.Sleep(500);

		Assert.That(AnyMarkerBadgeVisibleAfterCycle(v1), Is.False,
			"Clearing the first-row marker should hide the MarkerBadge.");
	}

	/// <summary>
	/// Returns true when at least one element in the accessibility tree carries
	/// an AutomationId matching the MarkerBadge pattern (RowPrefix + Id +
	/// ".MarkerBadge"). Cheaper than enumerating Items from the test side, and
	/// resilient to which specific Id the seam chose as "first normal row".
	/// </summary>
	private bool AnyMarkerBadgeVisibleAfterCycle(OriginalTimetableV1PageObject _)
	{
		// Suppress implicit wait — we want a fast scan, not a 10 s poll per probe.
		var prev = TimeSpan.FromSeconds(10);
		try
		{
			Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
			// XPath across both Android (resource-id) and iOS (name) is awkward;
			// the cross-platform path is to scan for descendants whose
			// AccessibilityId equals one of the MarkerBadge IDs we expect. We
			// don't know the Id, so probe descendants by XPath against the
			// accessibility identifier ending in ".MarkerBadge".
			//
			// On Android we use UiSelector resourceIdMatches; iOS XCUITest
			// supports BEGINSWITH/ENDSWITH via NSPredicate but Appium's iOS
			// bridge needs the predicate-string indirection. Easiest cross-driver
			// approach: descendants with @name (iOS) / @resource-id (Android)
			// containing the pattern. Use a substring XPath that works on both.
			var xpath = OpenQA.Selenium.By.XPath(
				"//*[contains(@name, '.MarkerBadge') or contains(@resource-id, '.MarkerBadge')]");
			var elements = Driver.FindElements(xpath);
			foreach (var el in elements)
			{
				try
				{
					if (el.Displayed)
						return true;
				}
				catch { }
			}
			return false;
		}
		finally
		{
			Driver.Manage().Timeouts().ImplicitWait = prev;
		}
	}
}
