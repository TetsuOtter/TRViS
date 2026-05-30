using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// E2E tests for the V6 (Bold Editorial) Original Timetable page.
/// V6 has no UI_TEST seam button for OnCycleMarker (out of scope), so the
/// third test reduces to a negative pre-condition: no MarkerBadge visible
/// before any marker action is taken.
/// </summary>
[TestFixture]
[Infrastructure.RetryAllTests(2)]
public class OriginalTimetableV6Tests : BaseUITest
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
	/// Navigates to V6 via the Shell flyout (no train committed). Asserts the
	/// page reaches a renderable state (Masthead / CurrentBlock / EmptyState
	/// on either tablet or compact layout).
	/// </summary>
	[Test]
	public void V6Page_OpensFromFlyout_Renders()
	{
		var v6 = new AppShellPage(Driver).NavigateToOriginalTimetableV6();
		Assert.That(v6.WaitForRendered(timeoutSeconds: 30), Is.True,
			"V6 page should render (Masthead/CurrentBlock/EmptyState).");
	}

	/// <summary>
	/// With no Work/Train committed, V6 must surface an EmptyState Label so
	/// the user sees a clear "select a train" affordance.
	/// </summary>
	[Test]
	public void V6Page_EmptyState_WhenNoTrainSelected()
	{
		var v6 = new AppShellPage(Driver).NavigateToOriginalTimetableV6();
		Assert.That(v6.WaitForRendered(timeoutSeconds: 30), Is.True);
		Assert.That(v6.IsEmptyStateVisible(timeoutSeconds: 10), Is.True,
			"EmptyState Label should be visible when no train has been selected.");
	}

	/// <summary>
	/// Negative pre-condition: no MarkerBadge (CurrentBlock or UpcomingList)
	/// visible before any marker action. Substantive marker-cycle coverage
	/// requires a UI_TEST seam (not yet implemented for V6).
	/// </summary>
	[Test]
	public void V6Page_NoMarkerBadgeByDefault()
	{
		_startHomePage.LoadSample();
		_startHomePage.WaitForElement(AutomationIds.StartHome.WorkGroupList);
		_startHomePage.CommitFirstWork();

		var v6 = new AppShellPage(Driver).NavigateToOriginalTimetableV6();
		Assert.That(v6.WaitForRendered(timeoutSeconds: 30), Is.True);

		Thread.Sleep(500);
		Assert.That(AnyMarkerBadgeVisible(), Is.False,
			"No V6 MarkerBadge should be visible before any marker action is taken.");
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
