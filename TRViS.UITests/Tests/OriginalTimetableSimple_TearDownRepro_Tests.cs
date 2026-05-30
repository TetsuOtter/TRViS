using OpenQA.Selenium;
using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// Verifies the MAUI #16927 Android blank-screen fix for OriginalTimetable pages.
/// Registers the page as a push route (not a cached ShellContent DataTemplate) on
/// Android. Navigate here, navigate away via GoToAsync, navigate back: must NOT blank.
/// </summary>
[TestFixture]
[Category("Repro")]
public class OriginalTimetableSimple_TearDownRepro_Tests : Infrastructure.BaseUITest
{
	protected override bool ShareSessionAcrossTestsInFixture => true;

	private StartHomePageObject _startHomePage = null!;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		if (!IsAndroid)
			Assert.Ignore("Android-only repro: push-route blank is Android-host-specific.");

		// Recover to StartHome at top of each iteration.
		// After TearDown restarts the app the process is still warming up.
		var startHome = new StartHomePageObject(Driver);
		if (!startHome.PollDisplayed(AutomationIds.StartHome.Title, timeoutSeconds: 10)
			&& !startHome.PollDisplayed(AutomationIds.StartHome.HomeBody, timeoutSeconds: 2))
		{
			new AppShellPage(Driver).NavigateToHome();
			startHome = new StartHomePageObject(Driver);
		}
		startHome.AcceptPrivacyPolicyIfNeeded();

		_startHomePage = startHome;
	}

	[TearDown]
	public override void TearDown()
	{
		base.TearDown();

		if (!IsAndroid)
			return;

		// Restart the Android app so each [Repeat] iteration begins with a
		// clean FragmentManager — same rationale as Dtac_TearDownRepro_Tests.
		try
		{
			Driver.ExecuteScript("mobile: terminateApp",
				new Dictionary<string, object> { { "appId", "dev.t0r.trvis" } });
			Driver.ExecuteScript("mobile: activateApp",
				new Dictionary<string, object> { { "appId", "dev.t0r.trvis" } });
		}
		catch (Exception ex)
		{
			TestContext.Out.WriteLine($"OTSimpleTearDownRepro: app restart failed: {ex.Message}");
		}
	}

	/// <summary>
	/// StartHome → navigate to OriginalTimetableSimplePage → navigate back
	/// (GoToAsync "//StartHomePage" via TestNavigateHomeButton seam) → navigate
	/// to OriginalTimetableSimplePage again → assert NOT blank.
	/// </summary>
	[Test]
	[Repeat(3)]
	public void OTSimplePage_AfterNavigateAway_DoesNotBlank_OnAndroid()
	{
		Assert.That(_startHomePage.IsDisplayed(), Is.True,
			"StartHome should be visible at iteration start.");

		var shell = new AppShellPage(Driver);

		// First navigation — push the OT page.
		var otPage = shell.NavigateToOriginalTimetableSimple();
		Assert.That(otPage.IsDisplayed(), Is.True,
			"OriginalTimetableSimplePage should be visible after first navigation.");

		// Navigate away via TestNavigateHomeButton seam (GoToAsync "//StartHomePage").
		// This is the trigger for MAUI #16927: GoToAsync leaves the Fragment in the
		// FragmentManager back-stack. The fix (push-route instead of ShellContent)
		// ensures the Fragment is properly torn down.
		try
		{
			var home = shell.NavigateToHome();

			TakeScreenshot();
			DumpPageSource();

			// No loader is active when navigating from OT Simple → StartHome, so the
			// page is in Start mode where Title is visible but HomeBody is not. Check
			// Title (Start mode) OR HomeBody (Home mode) to be mode-agnostic.
			Assert.That(
				home.PollDisplayed(AutomationIds.StartHome.Title, timeoutSeconds: 30)
				|| home.PollDisplayed(AutomationIds.StartHome.HomeBody, timeoutSeconds: 2),
				Is.True,
				"StartHome should be visible after navigating away from OTSimplePage.");

			// Second navigation — must NOT show a blank screen.
			var otPage2 = shell.NavigateToOriginalTimetableSimple();

			TakeScreenshot();
			DumpPageSource();

			Assert.That(otPage2.IsDisplayed(), Is.True,
				"OriginalTimetableSimplePage should be visible on second visit (not blank). " +
				"A blank screen here means MAUI #16927 is not fixed for push-route OT pages.");
		}
		catch (WebDriverTimeoutException ex)
		{
			TakeScreenshot();
			DumpPageSource();
			Assert.Fail(
				$"Navigation timed out during OT blank-screen repro test " +
				$"({ex.GetType().Name}: {ex.Message}). " +
				"Possible blank screen or slow rendering on Android.");
		}
	}
}
