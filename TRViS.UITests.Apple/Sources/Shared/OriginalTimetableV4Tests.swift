// OriginalTimetableV4Tests.swift
// Mirrors TRViS.UITests/Tests/OriginalTimetableV4Tests.cs — Apple platforms.

import XCTest

final class OriginalTimetableV4Tests: BaseUITestCase {

    private var startHome: StartHomePageObject!
    private var shell: AppShellPageObject!

    override func setUpWithError() throws {
        try super.setUpWithError()
        startHome = StartHomePageObject(app: app, base: self)
        shell = AppShellPageObject(app: app, base: self)
        XCTAssertTrue(
            startHome.isDisplayed(),
            "StartHomePage should be displayed after launch (OriginalTimetableV4Tests setUp)."
        )
        startHome.acceptPrivacyPolicyIfNeeded()
    }

    func testV4Page_OpensFromFlyout_Renders() throws {
        let v4 = shell.navigateToOriginalTimetableV4()
        XCTAssertTrue(
            v4.waitForRendered(timeout: 30),
            "V4 page should render (TrainStripe / Hero / MiniList / EmptyState)."
        )
    }

    func testV4Page_EmptyState_WhenNoTrainSelected() throws {
        let v4 = shell.navigateToOriginalTimetableV4()
        XCTAssertTrue(v4.waitForRendered(timeout: 30))
        XCTAssertTrue(
            v4.isEmptyStateVisible(timeout: 10),
            "EmptyState Label should be visible when no train has been selected."
        )
    }

    func testV4Page_NoMarkerBadgeByDefault() throws {
        startHome.loadSample()
        _ = startHome.waitForWorkGroupList(timeout: 30)
        _ = startHome.autoOpenForTesting()

        let v4 = shell.navigateToOriginalTimetableV4()
        XCTAssertTrue(v4.waitForRendered(timeout: 30))

        Thread.sleep(forTimeInterval: 0.6)
        XCTAssertFalse(
            v4.isAnyMarkerBadgeVisible(timeout: 2),
            "No V4 MarkerBadge (Hero or row) should be visible before any marker action is taken."
        )
    }
}
