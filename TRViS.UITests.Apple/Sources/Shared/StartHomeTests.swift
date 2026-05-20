// StartHomeTests.swift
// XCUITest port of TRViS.UITests/Tests/StartHomeTests.cs (2 tests).
//
// Each test uses a fresh per-test app launch (BaseUITestCase.setUp).
// The C# fixture's shared-session recovery blocks (dialog close, loader clear,
// flyout navigate-home) are dropped — per-test launch makes them unnecessary.

import XCTest

final class StartHomeTests: BaseUITestCase {

    // Per-test setUp: accept privacy policy if the banner is still showing
    // (first launch in a run shows the banner; subsequent launches in the same
    // run will have it already accepted in NSUserDefaults).
    override func setUpWithError() throws {
        try super.setUpWithError()
        let startHome = StartHomePageObject(app: app, base: self)
        XCTAssertTrue(
            startHome.isDisplayed(),
            "StartHomePage should be displayed after launch (StartHomeTests setUp)."
        )
        startHome.acceptPrivacyPolicyIfNeeded()
        XCTAssertTrue(
            startHome.isDisplayed(),
            "StartHomePage should be displayed after accepting privacy."
        )
    }

    // MARK: — Test: LoadSample_PopulatesWorkGroupList

    /// Mirrors C# StartHomeTests.LoadSample_PopulatesWorkGroupList.
    ///
    /// After tapping Load Demo the page transitions to Home mode and the
    /// WorkGroupList becomes visible and non-empty.
    func testLoadSample_PopulatesWorkGroupList() throws {
        let startHome = StartHomePageObject(app: app, base: self)

        XCTAssertTrue(startHome.isDisplayed(), "StartHomePage should be displayed.")

        startHome.loadSample()

        // After demo load, Home mode shows the WorkGroupList.
        guard let workGroupList = startHome.waitForWorkGroupList(timeout: 30) else {
            XCTFail("WorkGroupList should be visible after loading sample data.")
            return
        }
        XCTAssertTrue(
            workGroupList.exists,
            "WorkGroupList should be visible after loading sample data."
        )

        // Verify the list has at least one child element.
        // Use descendants(matching:.any) to probe child cells regardless of type.
        let childCount = workGroupList.descendants(matching: .any).count
        XCTAssertGreaterThan(
            childCount, 0,
            "WorkGroupList should have items after loading the sample database."
        )
    }

    // MARK: — Test: LoadSample_DoesNotAutoSelectWorkGroup

    /// Mirrors C# StartHomeTests.LoadSample_DoesNotAutoSelectWorkGroup.
    ///
    /// Regression: after a fresh load no WorkGroup should be auto-picked.
    /// WorkGroupChip stays hidden until the user taps a row; if the
    /// auto-cascade in TimetableSelectionManager.OnLoaderChanged ever
    /// resurfaces, WorkGroupChip appears immediately and this test fails.
    func testLoadSample_DoesNotAutoSelectWorkGroup() throws {
        let startHome = StartHomePageObject(app: app, base: self)

        XCTAssertTrue(startHome.isDisplayed())

        startHome.loadSample()
        _ = startHome.waitForWorkGroupList(timeout: 30)

        XCTAssertFalse(
            startHome.isWorkGroupChipVisible(),
            "WorkGroupChip should NOT be visible right after load — no tentative selection has been made."
        )
    }
}
