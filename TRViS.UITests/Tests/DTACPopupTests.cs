using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// Regression coverage for #273: the two remaining TR.Maui.AnchorPopover
/// popups (QuickSwitchPopup, opened from the AppBar title; SelectMarkerPopup,
/// opened from the timetable MarkerButton). AnchorPopover 1.0.0.2 is built
/// against MAUI 9 and on the project's MAUI 10 Windows target its ShowAsync
/// throws MissingMethodException for ElementExtensions.ToPlatform, crashing
/// the app into the fatal "不明なエラー" modal (ui-test-windows on #273; #266
/// retired its own AnchorPopover usage for the identical reason). The popups
/// are now in-page overlays owned by ViewHost, so opening and dismissing them
/// must work on every platform without crashing.
///
/// Open/dismiss run through UI_TEST seams that invoke the exact production
/// show/dismiss path (ShowQuickSwitchPopupAsync / ShowSelectMarkerPopupAsync /
/// DismissAsync) — the real anchors are MAUI custom controls WinUI surfaces as
/// non-control Panes Appium can't reliably tap, and #266 established that
/// real-gesture popover E2E is fragile cross-platform. SelectMarkerPopup's
/// real "Close" button is also exercised so its IPagePopupHost.DismissAsync
/// wiring is covered end to end.
/// </summary>
[TestFixture]
[Infrastructure.RetryAllTests(2)] // see AppLaunchTests for rationale
public class DTACPopupTests : BaseUITest
{
	private StartHomePageObject _startHomePage = null!;

	protected override bool ShareSessionAcrossTestsInFixture => true;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		// Belt-and-suspenders: if a prior test left the modal scrim up it
		// blocks NavigateToHome's flyout. Dismiss before recovering state.
		try { new DTACViewHostPageObject(Driver).TryDismissAnyPopup(); }
		catch { /* fresh session / not on DTAC — nothing to dismiss */ }

		_startHomePage = new StartHomePageObject(Driver);

		// Idempotent reset to StartHome with no loader (mirrors
		// DTACTimetableTests) — no-ops on the first test of the fixture.
		if (!_startHomePage.PollDisplayed(AutomationIds.StartHome.Title, timeoutSeconds: 3))
		{
			new AppShellPage(Driver).NavigateToHome();
			_startHomePage = new StartHomePageObject(Driver);
		}
		_startHomePage.ClearLoaderForTesting();
		_startHomePage.AcceptPrivacyPolicyIfNeeded();
	}

	/// <summary>
	/// Runs before BaseUITest's teardown (NUnit runs derived TearDown first).
	/// The overlay is a full-screen modal scrim; a popup left open by a failed
	/// assertion would block the flyout and wedge every subsequent test in
	/// this shared session (and, since the app process persists across
	/// fixtures, later fixtures too). Best-effort, never throws.
	/// </summary>
	[TearDown]
	public void DismissPopupAfterTest()
	{
		// Wrap the whole thing: if the Appium session died during the test
		// (e.g. a window-close cascade), even constructing the page object or
		// touching Driver can throw — and a throwing TearDown is reported by
		// NUnit as a failure, manufacturing one that wouldn't otherwise exist.
		try { new DTACViewHostPageObject(Driver).TryDismissAnyPopup(); }
		catch { /* driver/session dead — nothing to clean up */ }
	}

	private DTACViewHostPageObject OpenDTAC()
	{
		Assert.That(_startHomePage.IsDisplayed(), Is.True,
			"StartHome should be displayed at fixture entry.");
		_startHomePage.LoadSample();
		_startHomePage.WaitForElement(AutomationIds.StartHome.WorkGroupList);
		var dtac = _startHomePage.AutoOpenForTesting();
		Assert.That(dtac.IsDisplayed(), Is.True,
			"DTAC view should be displayed after AutoOpenForTesting.");
		return dtac;
	}

	/// <summary>
	/// Asserts a popup is shown, and on failure dumps the Appium tree to the
	/// test's stdout (NUnit "Standard Output Messages" in the CI log) so a
	/// "not shown" failure reveals whether the scrim / popup content is
	/// actually in the platform tree — same diagnostic approach the flyout
	/// helper uses.
	/// </summary>
	private static void AssertPopupShown(bool shown, DTACViewHostPageObject dtac, string label, string message)
	{
		if (!shown)
			TestContext.Out.WriteLine(
				$"[diag] {label} not shown — Appium tree follows:\n{dtac.CaptureTreeForDiagnostics()}");
		Assert.That(shown, Is.True, message);
	}

	[Test]
	public void QuickSwitchPopup_OpensAndDismisses_WithoutCrashing()
	{
		var dtac = OpenDTAC();

		// Retry the open seam: Android intermittently drops the Appium Button
		// click so the overlay never opens (run 25984214790 diagnostic:
		// PopupScrim=0, 不明なエラー=0 — no crash, the click just didn't take;
		// the identical path passed on run 25983883205).
		AssertPopupShown(dtac.OpenQuickSwitchPopupReliably(), dtac, "QuickSwitchPopup",
			"QuickSwitchPopup must appear after the open seam fires. On Windows the "
			+ "old AnchorPopover.ShowAsync threw MissingMethodException here and "
			+ "crashed the app into the fatal error modal (#273).");
		Assert.That(dtac.IsDisplayed(), Is.True,
			"DTAC must still be alive (no fatal-error modal) while the popup is open.");

		// Tap a control *inside* the popup (the "Work" tab — a plain
		// TapGestureRecognizer, not a fragile CollectionView row). This proves
		// descendant input routes through the overlay's absorber Border on
		// every platform (the WinUI-3 gesture-routing risk), and that the
		// absorber stops the tap bubbling to the dismiss scrim: the popup must
		// still be shown and the app still alive afterwards.
		dtac.TapQuickSwitchWorkTab();
		AssertPopupShown(dtac.IsQuickSwitchPopupShown(2), dtac, "QuickSwitchPopup (after Work-tab tap)",
			"Tapping the Work tab inside the popup must not dismiss it (the "
			+ "absorber Border must stop inner taps reaching the scrim) and must "
			+ "not crash the app.");
		Assert.That(dtac.IsDisplayed(), Is.True,
			"DTAC must still be alive after interacting inside the popup.");

		// Dismiss via the seam (DismissAsync) — tap-outside on the scrim isn't
		// reliably reproducible cross-platform; the regression is the crash, not
		// the gesture (#266 rationale). Retried for the same Android click-drop
		// reason as the open.
		Assert.That(dtac.DismissQuickSwitchReliably(), Is.True,
			"QuickSwitchPopup must be gone after DismissAsync.");
		Assert.That(dtac.IsDisplayed(), Is.True,
			"DTAC must remain displayed (no crash) after dismissing the popup.");
	}

	[Test]
	public void SelectMarkerPopup_OpensAndDismisses_WithoutCrashing()
	{
		var dtac = OpenDTAC();

		// Retry the open seam (same Android click-drop reason as QuickSwitch).
		AssertPopupShown(dtac.OpenSelectMarkerPopupReliably(), dtac, "SelectMarkerPopup",
			"SelectMarkerPopup must appear after the open seam fires. On Windows the "
			+ "old AnchorPopover.ShowAsync threw MissingMethodException here and "
			+ "crashed the app into the fatal error modal (#273).");
		Assert.That(dtac.IsDisplayed(), Is.True,
			"DTAC must still be alive (no fatal-error modal) while the popup is open.");

		// Dismiss via the popup's own "Close" button — exercises
		// SelectMarkerPopup.OnCloseButtonClicked -> IPagePopupHost.DismissAsync,
		// the real production interaction (retried for Android click-drop).
		Assert.That(dtac.DismissSelectMarkerViaCloseReliably(), Is.True,
			"SelectMarkerPopup must be gone after tapping its Close button.");
		Assert.That(dtac.IsDisplayed(), Is.True,
			"DTAC must remain displayed (no crash) after dismissing the popup.");
	}
}
