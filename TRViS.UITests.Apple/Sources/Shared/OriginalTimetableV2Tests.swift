// OriginalTimetableV2Tests.swift
// Mirrors TRViS.UITests/Tests/OriginalTimetableV2Tests.cs — Apple platforms.
//
// V2 Phase 3 ships both tablet and compact layouts, so unlike V1 the fixture
// does NOT skip on phone widths — the WaitForRendered probe covers both
// surfaces. The third test is a negative pre-condition (no MarkerBadge before
// any marker action); a real marker-cycle assertion would require a UI_TEST
// seam button that V2 does not yet expose.

import XCTest

final class OriginalTimetableV2Tests: BaseUITestCase {

    private var startHome: StartHomePageObject!
    private var shell: AppShellPageObject!

    override func setUpWithError() throws {
        try super.setUpWithError()
        startHome = StartHomePageObject(app: app, base: self)
        shell = AppShellPageObject(app: app, base: self)
        XCTAssertTrue(
            startHome.isDisplayed(),
            "StartHomePage should be displayed after launch (OriginalTimetableV2Tests setUp)."
        )
        startHome.acceptPrivacyPolicyIfNeeded()
    }

    /// Navigates to V2 via the Shell flyout. Asserts the page reaches a
    /// renderable state — any of tablet/compact header or empty-state.
    func testV2Page_OpensFromFlyout_Renders() throws {
        let v2 = shell.navigateToOriginalTimetableV2()
        XCTAssertTrue(
            v2.waitForRendered(timeout: 30),
            "V2 page should render (tablet/compact header or empty-state)."
        )
    }

    /// With no Work/Train committed, V2 must surface an empty-state Label.
    func testV2Page_EmptyState_WhenNoTrainSelected() throws {
        let v2 = shell.navigateToOriginalTimetableV2()
        XCTAssertTrue(v2.waitForRendered(timeout: 30))
        XCTAssertTrue(
            v2.isEmptyStateVisible(timeout: 10),
            "EmptyState Label should be visible when no train has been selected."
        )
    }

    /// Negative pre-condition: no MarkerBadge visible before any marker
    /// action. Substantive marker-cycle coverage requires a UI_TEST seam.
    func testV2Page_NoMarkerBadgeByDefault() throws {
        startHome.loadSample()
        _ = startHome.waitForWorkGroupList(timeout: 30)
        _ = startHome.autoOpenForTesting()

        let v2 = shell.navigateToOriginalTimetableV2()
        XCTAssertTrue(v2.waitForRendered(timeout: 30))

        Thread.sleep(forTimeInterval: 0.6)
        XCTAssertFalse(
            v2.isAnyMarkerBadgeVisible(timeout: 2),
            "No V2 MarkerBadge should be visible before any marker action is taken."
        )
    }
}
