using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// E2E tests for the V4 (Next Big) Original Timetable page.
/// V4 has no UI_TEST seam button for OnCycleMarker (out of scope), so the
/// third test reduces to a negative pre-condition: no MarkerBadge visible
/// before any marker action is taken.
/// </summary>
[TestFixture]
[Infrastructure.RetryAllTests(2)]
public class OriginalTimetableV4Tests : BaseUITest
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
	/// Navigates to V4 via the Shell flyout (no train committed). Asserts the
	/// page reaches a renderable state.
	/// </summary>
	[Test]
	public void V4Page_OpensFromFlyout_Renders()
	{
		var v4 = new AppShellPage(Driver).NavigateToOriginalTimetableV4();
		Assert.That(v4.WaitForRendered(timeoutSeconds: 30), Is.True,
			"V4 page should render (TrainStripe/Hero/MiniList/EmptyState).");
	}

	/// <summary>
	/// With no Work/Train committed, V4 must surface an EmptyState Label so
	/// the user sees a clear "select a train" affordance.
	/// </summary>
	[Test]
	public void V4Page_EmptyState_WhenNoTrainSelected()
	{
		var v4 = new AppShellPage(Driver).NavigateToOriginalTimetableV4();
		Assert.That(v4.WaitForRendered(timeoutSeconds: 30), Is.True);
		Assert.That(v4.IsEmptyStateVisible(timeoutSeconds: 10), Is.True,
			"EmptyState Label should be visible when no train has been selected.");
	}

	/// <summary>
	/// Negative pre-condition: no MarkerBadge (Hero or MiniList row) visible
	/// before any marker action. Substantive marker-cycle coverage requires a
	/// UI_TEST seam (not yet implemented for V4).
	/// </summary>
	[Test]
	public void V4Page_NoMarkerBadgeByDefault()
	{
		_startHomePage.LoadSample();
		_startHomePage.WaitForElement(AutomationIds.StartHome.WorkGroupList);
		_ = _startHomePage.AutoOpenForTesting();

		var v4 = new AppShellPage(Driver).NavigateToOriginalTimetableV4();
		Assert.That(v4.WaitForRendered(timeoutSeconds: 30), Is.True);

		Thread.Sleep(500);
		Assert.That(AnyMarkerBadgeVisible(), Is.False,
			"No V4 MarkerBadge should be visible before any marker action is taken.");
	}

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
