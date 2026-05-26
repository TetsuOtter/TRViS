using OpenQA.Selenium;
using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// TDD step 1 (fix/dtac-teardown-android): reproduce the DTAC tear-down blank
/// on Android. Probe 4b on fix/maui-16927-android-flyoutpage confirmed that
/// DTAC's view-subtree (presenter / VerticalTimetableView / AppBar /
/// ScreenWakeLock / TabHako) corrupts the subsequent Detail-swap in the
/// Android FlyoutPage host. Open question: does the same DTAC tear-down bug
/// surface on `main`, where the navigation primitive is
/// <c>Shell.Current.GoToAsync("//StartHomePage")</c> instead of
/// <c>FlyoutPage.Detail = newNav</c>? If yes, main has a latent same-family
/// bug; if no, only the FlyoutPage branch is affected.
///
/// The test is Android-only because the FlyoutPage / Shell render-coupling
/// bug is host-specific (Apple / Windows hosts render via a different code
/// path). Other platforms hit <c>Assert.Ignore</c> in SetUp.
///
/// Use <c>[Repeat(3)]</c> — NOT <c>[Retry]</c> — so a transient first-iter
/// failure does not mask intermittent reproductions. The
/// <c>AssemblyUITestSetUp</c> global session is reused across iterations;
/// SetUp recovers per-iteration via the same NavigateToHome + ClearLoader
/// pattern <see cref="NavigationTests"/> uses.
/// </summary>
[TestFixture]
[Category("Repro")]
public class Dtac_TearDownRepro_Tests : Infrastructure.BaseUITest
{
	// Share one Appium session across this fixture's iterations. The
	// assembly-level [SetUpFixture] also keeps the session global, but
	// declaring the override mirrors the convention NavigationTests uses
	// when its [SetUp] needs to recover the app state per test/iteration.
	protected override bool ShareSessionAcrossTestsInFixture => true;

	private StartHomePageObject _startHomePage = null!;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		// Android-only repro: the FlyoutPage-host blanking observed on the
		// other branch is a render-path issue specific to the Android host.
		// On main the host is Shell, but the same DTAC tear-down family
		// MAY still corrupt the next render. Other platforms skip.
		if (!IsAndroid)
			Assert.Ignore("Android-only repro: DTAC tear-down blank is Android-host-specific.");

		// Recover to StartHome at the top of each iteration. In a shared
		// session a prior iteration ends on DTAC (or, if the bug reproduces,
		// on a blank page), so probe StartHome first and fall back to the
		// NavigateToHome seam exactly like NavigationTests.SetUp does.
		//
		// Check HomeBody in addition to Title: after GoToAsync("..") pops
		// ViewHost, StartHome lands in Home mode where Title is hidden but
		// HomeBody is visible. Calling NavigateToHome from StartHome (already
		// there) would issue GoToAsync("//StartHomePage") redundantly, which on
		// Android adds a Fragment to the back stack and corrupts it for the
		// next ViewHost push.
		var startHome = new StartHomePageObject(Driver);
		if (!startHome.PollDisplayed(AutomationIds.StartHome.Title, timeoutSeconds: 3)
			&& !startHome.PollDisplayed(AutomationIds.StartHome.HomeBody, timeoutSeconds: 1))
		{
			new AppShellPage(Driver).NavigateToHome();
			startHome = new StartHomePageObject(Driver);
		}
		startHome.AcceptPrivacyPolicyIfNeeded();
		startHome.ClearLoaderForTesting();

		_startHomePage = startHome;
	}

	/// <summary>
	/// Cold StartHome → load sample + commit Work (AutoOpen) → DTAC →
	/// NavigateToHome via the DTAC.TestNavigateHomeButton seam → assert
	/// StartHome is on screen.
	///
	/// If StartHome fails to appear within the NavigateToHome internal
	/// 10 s budget, the seam tap dispatched but the rendered tree did not
	/// converge to StartHome — i.e. the DTAC tear-down blank repro'd on
	/// main as well. Catching the resulting <c>WebDriverTimeoutException</c>
	/// lets us capture screenshot + page-source FROM INSIDE the test (the
	/// TearDown hook also dumps on failure, but in-test artifacts give a
	/// clearer "blank shape" signal for the report).
	/// </summary>
	[Test]
	[Repeat(3)]
	public void DtacToHome_DoesNotBlank_OnAndroid()
	{
		// Cold launch → StartHome.
		Assert.That(_startHomePage.IsDisplayed(), Is.True,
			"StartHome should be visible at iteration start.");

		// Commit a Work via TestAutoOpenButton → DTAC visible. This boots
		// the full DTAC subtree (presenter / VerticalTimetableView / AppBar
		// / ScreenWakeLock / TabHako) — the Probe-4b-implicated chain.
		_startHomePage.LoadSample();
		_startHomePage.WaitForElement(AutomationIds.StartHome.WorkGroupList);
		var dtac = _startHomePage.AutoOpenForTesting();
		Assert.That(dtac.IsDisplayed(), Is.True,
			"DTAC should be visible after AutoOpen.");

		// Now navigate back to StartHome via the TestNavigateHomeButton seam.
		// On Android the seam uses GoToAsync("..") (pop, matching back-button
		// behaviour); on other platforms it uses GoToAsync("//StartHomePage").
		// NavigateToHome internally waits up to 10 s for StartHome.HomeBody
		// to appear. If the tear-down blank reproduces, that wait throws
		// WebDriverTimeoutException — wrap so we can capture artifacts and
		// report a clean Assert.Fail.
		var appShell = new AppShellPage(Driver);
		try
		{
			var home = appShell.NavigateToHome();

			// Capture artifacts BEFORE the final assertion so a regression
			// run has same-iteration evidence even if the assertion below
			// fails.
			TakeScreenshot();
			DumpPageSource();

			Assert.That(home.PollDisplayed(AutomationIds.StartHome.HomeBody, timeoutSeconds: 30), Is.True,
				"StartHome (Home-mode body) should be visible after navigating away from DTAC — " +
				"if this blanks, the DTAC tear-down bug exists on main too.");
		}
		catch (WebDriverTimeoutException ex)
		{
			TakeScreenshot();
			DumpPageSource();
			Assert.Fail(
				"NavigateToHome timed out waiting for StartHome.HomeBody after DTAC tear-down " +
				$"({ex.GetType().Name}: {ex.Message}). Likely DTAC-teardown blank on main.");
		}
	}
}
