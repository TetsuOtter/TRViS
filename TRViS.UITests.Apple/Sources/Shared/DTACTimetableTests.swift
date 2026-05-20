// DTACTimetableTests.swift
// Mirrors TRViS.UITests/Tests/DTACTimetableTests.cs.
//
// Covers: demo-data happy path (load → open → timetable tab → StartEndRun →
// OpenClose), NextTrainButton present when NextTrainId set, GPS deeplink
// survives the timetable render.
//
// Not covered: NextTrainButton_Hidden_WhenSelectedTrainHasNoNextTrainId — the
// C# source contains an explicit Assert.Ignore for iOS/Mac (lines 187-202 of
// DTACTimetableTests.cs): "Apple's accessibility tree surfaces unparented
// elements with AutomationId as visible on some OS versions — we cannot
// reliably distinguish hidden from displayed". Skipped here with XCTSkip.
//
// Per-test cold launch is used (BaseUITestCase.setUpWithError launches fresh).
// The C# shared-session SetUp recovery (NavigateToHome + ClearLoader when not
// on StartHome) is dropped — per-test launch makes it unnecessary.

import XCTest

final class DTACTimetableTests: BaseUITestCase {

    private var startHome: StartHomePageObject!

    override func setUpWithError() throws {
        try super.setUpWithError()
        startHome = StartHomePageObject(app: app, base: self)
        XCTAssertTrue(
            startHome.isDisplayed(),
            "StartHomePage should be displayed after launch (DTACTimetableTests setUp)."
        )
        startHome.acceptPrivacyPolicyIfNeeded()
        XCTAssertTrue(
            startHome.isDisplayed(),
            "StartHomePage should be displayed after accepting privacy."
        )
    }

    // MARK: — Helper

    private func loadSampleAndOpenDTAC() -> DTACViewHostPageObject {
        startHome.loadSample()
        _ = startHome.waitForWorkGroupList(timeout: 30)
        return startHome.autoOpenForTesting()
    }

    // MARK: — Test: DemoData_LoadOpenAndExerciseTimetable_HappyPath

    /// Mirrors C# DTACTimetableTests.DemoData_LoadOpenAndExerciseTimetable_HappyPath.
    ///
    /// Combined happy path: load sample → auto-open → timetable tab visible →
    /// StartEndRun toggle → OpenClose toggle. Replaces the five separate
    /// per-step tests the C# fixture used to run (merged to avoid paying the
    /// cold-launch cost five times).
    func testDemoData_LoadOpenAndExerciseTimetable_HappyPath() throws {
        XCTAssertTrue(startHome.isDisplayed(), "StartHome should be displayed at fixture entry.")

        // Step 1: LoadSample populates WorkGroup list.
        startHome.loadSample()
        guard let _ = startHome.waitForWorkGroupList(timeout: 30) else {
            XCTFail("WorkGroupList should appear after LoadSample.")
            return
        }
        let count = startHome.countWorkGroups()
        XCTAssertGreaterThanOrEqual(
            count, 2,
            "LoadSample should produce at least 2 work-group rows."
        )

        // Step 2: AutoOpenForTesting commits selection + navigates to DTAC.
        let dtac = startHome.autoOpenForTesting()
        XCTAssertTrue(
            dtac.isDisplayed(),
            "DTAC view should be displayed after AutoOpenForTesting."
        )

        // Step 3: Switch to timetable tab.
        dtac.switchToTimetableTab()
        guard let scrollView = dtac.timetableScrollView() else {
            XCTFail("TimetableScrollView should be visible after switching to the 時刻表 tab.")
            return
        }
        XCTAssertTrue(
            scrollView.exists,
            "TimetableScrollView should be visible after switching to the 時刻表 tab " +
            "(also the GPS auto-scroll target)."
        )

        // Step 4: 運行開始 toggle round-trip.
        guard let startEndBtn = dtac.startEndRunButton() else {
            XCTFail("StartEndRunButton should be visible in the timetable tab.")
            return
        }
        XCTAssertTrue(startEndBtn.exists, "StartEndRunButton should be visible.")

        dtac.tapStartEndRun()
        Thread.sleep(forTimeInterval: 0.4)

        // LocationServiceButton must remain in the tree after toggling on.
        guard let locBtn = dtac.locationServiceButton() else {
            XCTFail("LocationServiceButton should remain in the tree after toggling 運行開始 on.")
            return
        }
        XCTAssertTrue(
            locBtn.exists,
            "LocationServiceButton should remain in the tree after toggling 運行開始 on."
        )

        dtac.tapStartEndRun()
        Thread.sleep(forTimeInterval: 0.4)
        guard let startEndBtn2 = dtac.startEndRunButton() else {
            XCTFail("StartEndRunButton should still be visible after toggling 運行開始 off.")
            return
        }
        XCTAssertTrue(
            startEndBtn2.exists,
            "StartEndRunButton should still be visible after toggling 運行開始 off (repeated taps OK)."
        )

        // Step 5: OpenClose toggle round-trip.
        guard let openCloseBtn = dtac.openCloseButton() else {
            XCTFail("OpenCloseButton should be visible in the timetable tab.")
            return
        }
        XCTAssertTrue(openCloseBtn.exists, "OpenCloseButton should be visible.")
        let initialText = openCloseBtn.label

        dtac.tapOpenClose()
        Thread.sleep(forTimeInterval: 0.4)
        // Re-query to get fresh label after the tap.
        let afterFirstTap = dtac.openCloseButton()?.label ?? ""
        XCTAssertNotEqual(
            afterFirstTap, initialText,
            "OpenCloseButton text should differ after the first tap (open/closed icons swap)."
        )

        dtac.tapOpenClose()
        Thread.sleep(forTimeInterval: 0.4)
        let afterSecondTap = dtac.openCloseButton()?.label ?? ""
        XCTAssertEqual(
            afterSecondTap, initialText,
            "Second tap should return OpenCloseButton to its original state."
        )
    }

    // MARK: — Test: NextTrainButton_Present_WhenSelectedTrainHasNextTrainId

    /// Mirrors C# DTACTimetableTests.NextTrainButton_Present_WhenSelectedTrainHasNextTrainId.
    ///
    /// Regression for #225: the NextTrainButton must appear in the accessibility
    /// tree when the selected train has a non-empty NextTrainId.
    func testNextTrainButton_Present_WhenSelectedTrainHasNextTrainId() throws {
        XCTAssertTrue(startHome.isDisplayed())
        startHome.loadSample()
        _ = startHome.waitForWorkGroupList(timeout: 30)

        // Cascade to linear-train-1 (NextTrainId = "linear-train-2") via seam.
        let dtac = startHome.seedTrainSelectionWithNextTrain()
        dtac.switchToTimetableTab()

        XCTAssertTrue(
            dtac.isNextTrainButtonPresent(timeout: 8),
            "NextTrainButton must be reachable when SelectedTrainData.NextTrainId is non-empty " +
            "(scroll-to-bottom retries are built into isNextTrainButtonPresent)."
        )
    }

    // MARK: — Test: NextTrainButton_Hidden_WhenSelectedTrainHasNoNextTrainId (SKIPPED)

    /// Explicitly skipped on Apple platforms.
    ///
    /// The C# source (DTACTimetableTests.cs lines 187-202) contains an
    /// Assert.Ignore for iOS and Mac Catalyst: "Apple's accessibility tree
    /// surfaces unparented elements with AutomationId as visible on some OS
    /// versions — we cannot reliably distinguish hidden from displayed."
    /// Android and Windows coverage is sufficient for this assertion.
    func testNextTrainButton_Hidden_WhenSelectedTrainHasNoNextTrainId() throws {
        throw XCTSkip(
            "Skipped on Apple (iOS/macOS): XCUITest surfaces unparented elements with " +
            "AutomationId as visible on some OS versions; cannot reliably distinguish " +
            "hidden from displayed. See C# DTACTimetableTests.cs lines 187-202. " +
            "Android and Windows coverage is sufficient for this assertion."
        )
    }

    // MARK: — Test: GpsLocation_DeeplinkReachesLocationService

    /// Mirrors C# DTACTimetableTests.GpsLocation_DeeplinkReachesLocationService.
    ///
    /// Verifies the GPS auto-scroll pipeline: a fake GPS coord injected via the
    /// test seam force-enables LocationService without CoreLocation permissions.
    /// The strict assertion is "app stays responsive and timetable remains
    /// renderable" after the GPS event.
    func testGpsLocation_DeeplinkReachesLocationService() throws {
        XCTAssertTrue(startHome.isDisplayed())
        startHome.loadSample()
        _ = startHome.waitForWorkGroupList(timeout: 30)

        // Push a fixture GPS coord through the DEBUG-only seed button.
        startHome.seedGpsLocationForTesting()
        Thread.sleep(forTimeInterval: 0.5)

        let dtac = startHome.autoOpenForTesting()
        XCTAssertTrue(dtac.isDisplayed())
        dtac.switchToTimetableTab()
        guard let scrollView = dtac.timetableScrollView() else {
            XCTFail("Timetable container must remain renderable after the GPS event was injected.")
            return
        }
        XCTAssertTrue(
            scrollView.exists,
            "Timetable container must remain renderable after the GPS event was injected."
        )
    }
}
