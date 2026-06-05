using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// Navigation tests require Firebase consent to have been accepted first.
/// Each test accepts Firebase consent at the beginning.
/// </summary>
[TestFixture]
[Infrastructure.RetryAllTests(2)] // see AppLaunchTests for rationale
public class NavigationTests : BaseUITest
{
	// Share one Appium session across all tests in this fixture (iOS only).
	// See BaseUITest.ShareSessionAcrossTestsInFixture for details.
	protected override bool ShareSessionAcrossTestsInFixture => true;

	private AppShellPage _shell = null!;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		// Navigate to StartHome at the top of each test so OpenFlyout starts
		// from a flyout-rooted page. The earlier "OpenFlyout works from any
		// flyout-capable page" assumption breaks on Android in the
		// shared-session run: the previous fixture (HorizontalTimetableTests)
		// lands the app on DTAC's VerticalView tab which has orientation
		// locked to Landscape, and on Android the OpenFlyout probes (DTAC
		// MenuButton click or "Open navigation drawer") don't open the
		// NavigationView under that lock (CI run 25727806170 / 25729263553).
		// NavigateToHome uses the DTAC.TestNavigateHomeButton seam to GoToAsync
		// past the broken flyout path, and the seam's OnDisappearing also
		// unlocks the orientation. From StartHome OpenFlyout works fine on
		// every platform.
		var startHome = new StartHomePageObject(Driver);
		if (!startHome.PollDisplayed(AutomationIds.StartHome.Title, timeoutSeconds: 3))
		{
			new AppShellPage(Driver).NavigateToHome();
			startHome = new StartHomePageObject(Driver);
		}
		startHome.AcceptPrivacyPolicyIfNeeded();
		startHome.ClearLoaderForTesting();

		_shell = new AppShellPage(Driver);
	}

	/// <summary>
	/// TPL is no longer a flyout entry — it is reached via the StartHome
	/// footer "Third Party Licenses" link, which pushes it as a modal with
	/// an in-page close (X) icon. Opens it, asserts it renders, then closes
	/// via the icon and asserts we return to StartHome.
	/// </summary>
	[Test]
	public void Footer_OpenAndCloseThirdPartyLicenses()
	{
		var startHome = new StartHomePageObject(Driver);

		var page = startHome.OpenThirdPartyLicenses();
		Assert.That(page.IsDisplayed(), Is.True,
			"ThirdPartyLicenses modal should be displayed after tapping the footer link.");

		startHome = page.CloseModal();
		Assert.That(startHome.IsDisplayed(), Is.True,
			"StartHomePage should be displayed again after closing the TPL modal.");
	}

	[Test]
	public void Flyout_NavigateToSettings()
	{
		var page = _shell.NavigateToSettings();
		Assert.That(page.IsDisplayed(), Is.True,
			"Settings (EasterEgg) page should be displayed after navigation.");
	}

	[Test]
	public void Flyout_NavigateToDTAC()
	{
		// On Android the DTAC FlyoutItem is removed (MAUI #16927 mitigation):
		// DTAC is a relative route reachable only after a Work is committed.
		// Flyout-based navigation to DTAC no longer exists on this platform.
		if (IsAndroid)
			Assert.Ignore("Android: DTAC flyout item removed (MAUI #16927); DTAC reachable only via relative route after Work commit.");

		var page = _shell.NavigateToDTAC();
		Assert.That(page.IsDisplayed(), Is.True,
			"DTACViewHost page should be displayed after navigation.");
	}

	/// <summary>
	/// Regression for #240: ViewHost is a cached ShellContent (DataTemplate),
	/// so the same instance is reused on every flyout navigation. Earlier code
	/// disposed the presenter on Unloaded, permanently severing its
	/// LocationService.TimeChanged subscription — the second visit then showed
	/// a frozen clock. Asserts the clock actually changes on the second visit.
	///
	/// Reads the time via DTAC.TestTimeSeam (a UI_TEST-only Label that mirrors
	/// the presenter's TimeLabelText with a sentinel prefix). Cannot use the
	/// real DTAC.TimeLabel: iOS doesn't surface MAUI Labels in the
	/// accessibility tree when their text is empty, and the AppBar's TimeLabel
	/// is hidden on narrow phones in portrait by an internal width threshold.
	/// </summary>
	[Test]
	public void DTAC_ReopenAfterNavigateAway_ClockKeepsTicking()
	{
		// This regression tests the *cached* ShellContent model (#240): the same
		// ViewHost instance is reused across visits. On Android (MAUI #16927
		// mitigation), ViewHost is a fresh route-created instance per visit — the
		// cached-instance failure mode does not apply. Android DTAC tear-down
		// coverage is provided by Dtac_TearDownRepro_Tests instead.
		if (IsAndroid)
			Assert.Ignore("Android: ViewHost is route-created (fresh per visit); cached-ShellContent regression (#240) does not apply.");

		var dtac = _shell.NavigateToDTAC();
		Assert.That(dtac.IsDisplayed(), Is.True,
			"DTAC should be visible on the first visit.");

		_shell.NavigateToHome();

		dtac = _shell.NavigateToDTAC();
		Assert.That(dtac.IsDisplayed(), Is.True,
			"DTAC should be visible on the second visit.");

		// LocationService raises TimeChanged once per second (Task.Delay(100)
		// polling that fires only when GetCurrentTimeSeconds() advances).
		// Sleep > 1 s between reads so a live clock must have ticked at least
		// once even if the first read landed right after a tick.
		string firstReading = dtac.ReadTimeViaSeam();
		Thread.Sleep(1500);
		string secondReading = dtac.ReadTimeViaSeam();

		Assert.That(secondReading, Is.Not.EqualTo(firstReading),
			$"Clock must keep updating on the second DTAC visit (#240). " +
			$"First='{firstReading}', Second='{secondReading}'.");
	}

	/// <summary>
	/// Regression for #240 (title half): the same disposed-presenter chain
	/// also unhooked AppViewModel.PropertyChanged, so a SelectedWork change
	/// that happened *while DTAC was hidden* never updated the AppBar title.
	/// Repro: open DTAC empty (no Work) → home → load sample + commit Work →
	/// DTAC. With the bug, AppBar Title stays empty on the second visit.
	///
	/// Reads via DTAC.TestTitleSeam (UI_TEST-only mirror Label) for the same
	/// iOS-accessibility reason as the clock test above.
	/// </summary>
	[Test]
	public void DTAC_ReopenAfterWorkSelected_TitleUpdated()
	{
		// Same rationale as DTAC_ReopenAfterNavigateAway_ClockKeepsTicking: this
		// test exercises the cached ShellContent model (#240). On Android, ViewHost
		// is route-created (fresh per visit) so the presenter-disposal failure mode
		// cannot occur. Skip on Android; see Dtac_TearDownRepro_Tests for Android
		// DTAC coverage.
		if (IsAndroid)
			Assert.Ignore("Android: ViewHost is route-created (fresh per visit); cached-ShellContent regression (#240) does not apply.");

		// Clear any loader/SelectedWork left by a prior test in this
		// fixture's shared Appium session — base SetUp only navigates to
		// StartHome and accepts the privacy policy. Without this, a
		// non-empty AppBar title carried over from an earlier test would
		// let the later `Is.Not.EqualTo(initialTitle)` assertion pass
		// trivially even with the bug present.
		var startHome = new StartHomePageObject(Driver);
		startHome.ClearLoaderForTesting();

		// First DTAC visit with no Work selected — primes the broken
		// codepath by triggering Unloaded → Dispose on the way back.
		var dtac = _shell.NavigateToDTAC();
		Assert.That(dtac.IsDisplayed(), Is.True);

		// Capture the post-clear title baseline. With no SelectedWork the
		// presenter sets TitleText to "", so the seam reads its sentinel
		// prefix only (stripped to "" by the page object). Asserting the
		// later title differs from this baseline catches the bug even on
		// platforms where an empty TitleText still surfaces non-empty
		// fallback text in the seam.
		string initialTitle = dtac.ReadTitleViaSeam();

		startHome = _shell.NavigateToHome();
		// Demo data load + auto-open commits SelectedWork AND navigates to
		// DTAC in one shot. Both side-effects happen *after* the dead
		// presenter from the first visit would have missed them.
		startHome.LoadSample();
		startHome.WaitForElement(AutomationIds.StartHome.WorkGroupList);
		dtac = startHome.AutoOpenForTesting();
		Assert.That(dtac.IsDisplayed(), Is.True,
			"DTAC should be visible after AutoOpenForTesting.");

		// Title set goes through MainThread.BeginInvokeOnMainThread; give
		// the dispatcher a beat to flush before reading.
		Thread.Sleep(500);
		string title = dtac.ReadTitleViaSeam();
		Assert.That(title, Is.Not.Empty,
			$"AppBar Title must reflect the committed Work on the second " +
			$"DTAC visit (#240). Got='{title}'.");
		Assert.That(title, Is.Not.EqualTo(initialTitle),
			$"AppBar Title must change after a Work is selected (#240). " +
			$"Initial='{initialTitle}', After='{title}'.");

		// Restore Start mode for any subsequent test in this fixture's
		// shared session. Mirrors DTACTimetableTests.SetUp's assumption.
		_shell.NavigateToHome().ClearLoaderForTesting();
	}
}
