using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// E2E tests for the V2 (Card Stack) Original Timetable page.
/// Coverage mirrors OriginalTimetableV1Tests but without a marker-cycle test —
/// V2 has no UI_TEST seam button for OnCycleMarker (adding one was out of
/// scope for this fixture), so the third test reduces to a negative
/// pre-condition assertion: the MarkerBadge surface should be empty by default.
/// </summary>
[TestFixture]
[Infrastructure.RetryAllTests(2)]
public class OriginalTimetableV2Tests : BaseUITest
{
	private StartHomePageObject _startHomePage = null!;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();
		var startHome = new StartHomePageObject(Driver);
		if (!startHome.PollDisplayed(AutomationIds.StartHome.Title, timeoutSeconds: 3))
		{
			new AppShellPage(Driver).NavigateToHome();
			startHome = new StartHomePageObject(Driver);
		}
		startHome.AcceptPrivacyPolicyIfNeeded();
		startHome.ClearLoaderForTesting();
		_startHomePage = startHome;
	}

	/// <summary>
	/// Navigates to V2 via the Shell flyout (no train committed). Asserts the
	/// page reaches a renderable state (empty-state or active-train header).
	/// </summary>
	[Test]
	[Platform(Exclude = "Win", Reason = "Windows keyboard navigation to V2 FlyoutItem via NavigateViaKeyboard times out waiting for WaitForRendered. Works on Android/Catalyst. Needs Windows-specific navigation fix.")]
	public void V2Page_OpensFromFlyout_Renders()
	{
		var v2 = new AppShellPage(Driver).NavigateToOriginalTimetableV2();
		Assert.That(v2.WaitForRendered(timeoutSeconds: 30), Is.True,
			"V2 page should render (any of tablet/compact header or empty-state).");
	}

	/// <summary>
	/// With no Work/Train committed, V2 must surface an empty-state Label so
	/// the user sees a clear "select a train" affordance.
	/// </summary>
	[Test]
	[Platform(Exclude = "Win", Reason = "Windows keyboard navigation to V2 FlyoutItem times out. Works on Android/Catalyst.")]
	public void V2Page_EmptyState_WhenNoTrainSelected()
	{
		var v2 = new AppShellPage(Driver).NavigateToOriginalTimetableV2();
		Assert.That(v2.WaitForRendered(timeoutSeconds: 30), Is.True);
		Assert.That(v2.IsEmptyStateVisible(timeoutSeconds: 10), Is.True,
			"EmptyState Label should be visible when no train has been selected.");
	}

	/// <summary>
	/// Negative pre-condition: with no marker action taken, no MarkerBadge
	/// should be visible anywhere in the V2 row surface. Acts as a smoke check
	/// that nothing has spuriously marked rows on page-open. Substantive
	/// marker-cycle coverage requires a UI_TEST seam (not yet implemented for V2).
	/// </summary>
	[Test]
	[Platform(Exclude = "Win", Reason = "Windows keyboard navigation to V2 FlyoutItem times out. Works on Android/Catalyst.")]
	public void V2Page_NoMarkerBadgeByDefault()
	{
		_startHomePage.LoadSample();
		_startHomePage.WaitForElement(AutomationIds.StartHome.WorkGroupList);
		_startHomePage.CommitFirstWork();

		var v2 = new AppShellPage(Driver).NavigateToOriginalTimetableV2();
		Assert.That(v2.WaitForRendered(timeoutSeconds: 30), Is.True);

		Thread.Sleep(500);
		Assert.That(AnyMarkerBadgeVisible(), Is.False,
			"No V2 MarkerBadge should be visible before any marker action is taken.");
	}

	/// <summary>
	/// True when ANY element in the tree carries an AutomationId ending in
	/// ".MarkerBadge". MarkerBadges are hidden (HasMarker=false) until a row
	/// is marked, so existence here is evidence of a marked row.
	/// </summary>
	private bool AnyMarkerBadgeVisible()
	{
		var prev = TimeSpan.FromSeconds(10);
		try
		{
			Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
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
