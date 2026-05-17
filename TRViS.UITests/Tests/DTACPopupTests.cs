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

	[Test]
	public void QuickSwitchPopup_OpensAndDismisses_WithoutCrashing()
	{
		var dtac = OpenDTAC();

		dtac.OpenQuickSwitchPopupViaSeam();
		Assert.That(dtac.IsQuickSwitchPopupShown(), Is.True,
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
		Assert.That(dtac.IsQuickSwitchPopupShown(2), Is.True,
			"Tapping the Work tab inside the popup must not dismiss it (the "
			+ "absorber Border must stop inner taps reaching the scrim) and must "
			+ "not crash the app.");
		Assert.That(dtac.IsDisplayed(), Is.True,
			"DTAC must still be alive after interacting inside the popup.");

		// Dismiss via the seam (DismissAsync) — tap-outside on the scrim isn't
		// reliably reproducible cross-platform; the regression is the crash, not
		// the gesture (#266 rationale).
		dtac.DismissPopupViaSeam();
		Assert.That(dtac.IsQuickSwitchPopupGone(), Is.True,
			"QuickSwitchPopup must be gone after DismissAsync.");
		Assert.That(dtac.IsDisplayed(), Is.True,
			"DTAC must remain displayed (no crash) after dismissing the popup.");
	}

	[Test]
	public void SelectMarkerPopup_OpensAndDismisses_WithoutCrashing()
	{
		var dtac = OpenDTAC();

		dtac.OpenSelectMarkerPopupViaSeam();
		Assert.That(dtac.IsSelectMarkerPopupShown(), Is.True,
			"SelectMarkerPopup must appear after the open seam fires. On Windows the "
			+ "old AnchorPopover.ShowAsync threw MissingMethodException here and "
			+ "crashed the app into the fatal error modal (#273).");
		Assert.That(dtac.IsDisplayed(), Is.True,
			"DTAC must still be alive (no fatal-error modal) while the popup is open.");

		// Dismiss via the popup's own "Close" button — exercises
		// SelectMarkerPopup.OnCloseButtonClicked -> IPagePopupHost.DismissAsync,
		// the real production interaction.
		dtac.TapSelectMarkerPopupClose();
		Assert.That(dtac.IsSelectMarkerPopupGone(), Is.True,
			"SelectMarkerPopup must be gone after tapping its Close button.");
		Assert.That(dtac.IsDisplayed(), Is.True,
			"DTAC must remain displayed (no crash) after dismissing the popup.");
	}
}
