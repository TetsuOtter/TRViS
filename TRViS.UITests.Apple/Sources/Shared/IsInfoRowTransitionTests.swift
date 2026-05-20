// IsInfoRowTransitionTests.swift
// XCUITest port of TRViS.UITests/Tests/IsInfoRowTransitionTests.cs (Phase 2C).
//
// Regression for "non-InfoRow components (station name, drive time, etc.)
// remain visible after a WebSocket soft-update changes IsInfoRow from false
// to true on a timetable row."
//
// The C# fixture is [Platform(Exclude = "Linux")] — the Android UIAutomator2
// limitation (Linux NUnit host) does NOT apply here. Apple is fully supported.
//
// Per-test cold launch is used (BaseUITestCase.setUpWithError launches fresh).

import XCTest

final class IsInfoRowTransitionTests: BaseUITestCase {

    private var startHome: StartHomePageObject!

    override func setUpWithError() throws {
        try super.setUpWithError()
        startHome = StartHomePageObject(app: app, base: self)
        XCTAssertTrue(
            startHome.isDisplayed(),
            "StartHomePage should be displayed after launch (IsInfoRowTransitionTests setUp)."
        )
        startHome.acceptPrivacyPolicyIfNeeded()
        XCTAssertTrue(
            startHome.isDisplayed(),
            "StartHomePage should be displayed after accepting privacy."
        )
    }

    // MARK: — Test: InfoRowTransition_FalseToTrue_RemovesStationNameShowsInfoLabel

    /// Mirrors C# IsInfoRowTransitionTests.InfoRowTransition_FalseToTrue_RemovesStationNameShowsInfoLabel.
    ///
    /// Core regression: after the IsInfoRow seam fires, the station-name label
    /// must disappear and the info-row label must appear.
    ///
    /// Visibility is determined by frame size (width>0 && height>0) rather than
    /// isHittable. On iPhone the timetable Grid (~740 pt wide) is wider than the
    /// screen (~390 pt), so XCUITest cascades isHittable=false from the off-screen
    /// parent. Frame size is non-zero in that state — matching the C# heuristic.
    func testInfoRowTransition_FalseToTrue_RemovesStationNameShowsInfoLabel() throws {
        XCTAssertTrue(startHome.isDisplayed())
        startHome.loadSample()
        _ = startHome.waitForWorkGroupList(timeout: 30)

        let dtac = startHome.autoOpenForTesting()
        dtac.switchToTimetableTab()

        // Row 0 in the default sample train is "駅１" (IsInfoRow=false).
        let stationId = String(format: AutomationIds.DTAC.timetableRowStationNamePattern, 0)
        let infoRowId = String(format: AutomationIds.DTAC.timetableRowInfoRowPattern, 0)

        // Pre-condition: station-name label is visible before the transition.
        XCTAssertTrue(
            dtac.isElementUserVisible(automationId: stationId, timeout: 3),
            "Row 0 StationNameLabel must be visible before the IsInfoRow transition."
        )

        // Action: trigger the seam that simulates the WebSocket IsInfoRow edit.
        dtac.tapSeedIsInfoRowTransition()
        Thread.sleep(forTimeInterval: 0.5) // allow the PropertyChanged cascade to settle

        // Post-condition: station-name must be gone.
        // isElementUserVisible polls until timeout; if it returns false, the element
        // either left the tree or collapsed to 0×0 — both are the correct outcome.
        XCTAssertFalse(
            dtac.isElementUserVisible(automationId: stationId, timeout: 3),
            "Row 0 StationNameLabel must NOT be visible after IsInfoRow changed to true " +
            "(non-InfoRow components must be removed from the Grid)."
        )

        // Post-condition: info-row label must be visible.
        XCTAssertTrue(
            dtac.isElementUserVisible(automationId: infoRowId, timeout: 3),
            "Row 0 InfoRowLabel must be visible after IsInfoRow changed to true."
        )
    }
}
