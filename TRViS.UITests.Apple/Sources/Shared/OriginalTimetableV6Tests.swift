// OriginalTimetableV6Tests.swift
// Mirrors TRViS.UITests/Tests/OriginalTimetableV6Tests.cs — Apple platforms.

import XCTest

final class OriginalTimetableV6Tests: BaseUITestCase {

    private var startHome: StartHomePageObject!
    private var shell: AppShellPageObject!

    override func setUpWithError() throws {
        try super.setUpWithError()
        startHome = StartHomePageObject(app: app, base: self)
        shell = AppShellPageObject(app: app, base: self)
        XCTAssertTrue(
            startHome.isDisplayed(),
            "StartHomePage should be displayed after launch (OriginalTimetableV6Tests setUp)."
        )
        startHome.acceptPrivacyPolicyIfNeeded()
    }

    func testV6Page_OpensFromFlyout_Renders() throws {
        let v6 = shell.navigateToOriginalTimetableV6()
        XCTAssertTrue(
            v6.waitForRendered(timeout: 30),
            "V6 page should render (Masthead / CurrentBlock / EmptyState)."
        )
    }

    func testV6Page_EmptyState_WhenNoTrainSelected() throws {
        let v6 = shell.navigateToOriginalTimetableV6()
        XCTAssertTrue(v6.waitForRendered(timeout: 30))
        XCTAssertTrue(
            v6.isEmptyStateVisible(timeout: 10),
            "EmptyState Label should be visible when no train has been selected."
        )
    }

    func testV6Page_NoMarkerBadgeByDefault() throws {
        startHome.loadSample()
        _ = startHome.waitForWorkGroupList(timeout: 30)
        _ = startHome.autoOpenForTesting()

        let v6 = shell.navigateToOriginalTimetableV6()
        XCTAssertTrue(v6.waitForRendered(timeout: 30))

        Thread.sleep(forTimeInterval: 0.6)
        XCTAssertFalse(
            v6.isAnyMarkerBadgeVisible(timeout: 2),
            "No V6 MarkerBadge (CurrentBlock or row) should be visible before any marker action is taken."
        )
    }
}
