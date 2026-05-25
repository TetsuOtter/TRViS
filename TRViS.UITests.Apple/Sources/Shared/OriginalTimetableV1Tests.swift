// OriginalTimetableV1Tests.swift
// Mirrors TRViS.UITests/Tests/OriginalTimetableV1Tests.cs — Apple platforms.
//
// V1 Phase 1 is tablet-only (width >= 600pt). iPad mini A17 portrait = 744pt
// and so renders the CollectionView layout. iPhone 16 portrait = 393pt and
// renders the compact placeholder — these tests XCTSkip on phone widths.
//
// Per-test cold launch is used (BaseUITestCase.setUpWithError launches fresh).
// V1's MarkersVersion / CurIdxVersion state in the singleton VM is non-trivial
// to reset between tests via in-app seams, so a clean process avoids the cost
// of writing yet more reset plumbing.

import XCTest

final class OriginalTimetableV1Tests: BaseUITestCase {

    private var startHome: StartHomePageObject!
    private var shell: AppShellPageObject!

    /// Tablet breakpoint in points — must match OriginalTimetableV1Page.TabletBreakpoint.
    private let tabletBreakpointPt: CGFloat = 600

    override func setUpWithError() throws {
        try super.setUpWithError()

        // Phase 1 V1 layout is tablet-only. Skip the fixture on phone widths so
        // the iPhone matrix entries don't fail on the compact placeholder
        // (which intentionally has no train header / row list).
        let width = app.windows.firstMatch.frame.size.width
        try XCTSkipIf(
            width > 0 && width < tabletBreakpointPt,
            "Skipping V1 tests: width \(width)pt < tablet breakpoint \(tabletBreakpointPt)pt (Phase 1 is tablet-only)."
        )

        startHome = StartHomePageObject(app: app, base: self)
        shell = AppShellPageObject(app: app, base: self)
        XCTAssertTrue(
            startHome.isDisplayed(),
            "StartHomePage should be displayed after launch (OriginalTimetableV1Tests setUp)."
        )
        startHome.acceptPrivacyPolicyIfNeeded()
    }

    // MARK: — Test: V1Page_OpensFromFlyout_Renders

    /// Navigates to V1 via the Shell flyout (no train committed).
    /// Asserts the page reaches a renderable state — either the empty-state
    /// Label (no ActiveTrain) or the sticky header (some prior session leaked
    /// an ActiveTrain into the singleton VM). Both are acceptable.
    func testV1Page_OpensFromFlyout_Renders() throws {
        let v1 = shell.navigateToOriginalTimetableV1()
        XCTAssertTrue(
            v1.waitForRendered(timeout: 30),
            "V1 page should render (sticky header or empty-state label)."
        )
    }

    // MARK: — Test: V1Page_EmptyState_WhenNoTrainSelected

    /// With no Work/Train committed, V1 must render the empty-state Label so
    /// the user sees a clear "select a train" affordance instead of a blank page.
    func testV1Page_EmptyState_WhenNoTrainSelected() throws {
        let v1 = shell.navigateToOriginalTimetableV1()
        XCTAssertTrue(v1.waitForRendered(timeout: 30))
        XCTAssertTrue(
            v1.isEmptyStateVisible(timeout: 10),
            "EmptyState Label should be visible when no train has been selected."
        )
    }

    // MARK: — Test: V1Page_AfterTrainSelected_ShowsHeaderWithTrainNumber

    /// After committing a Work via autoOpenForTesting (cascades to ActiveTrain),
    /// navigating to V1 should show the sticky header with a non-empty
    /// TrainNumber, not the empty-state.
    func testV1Page_AfterTrainSelected_ShowsHeaderWithTrainNumber() throws {
        startHome.loadSample()
        _ = startHome.waitForWorkGroupList(timeout: 30)
        let dtac = startHome.autoOpenForTesting()
        XCTAssertTrue(dtac.isDisplayed(), "DTAC should reach displayed state after AutoOpen.")

        let v1 = shell.navigateToOriginalTimetableV1()
        XCTAssertTrue(v1.waitForRendered(timeout: 30))
        XCTAssertTrue(
            v1.isHeaderVisible(timeout: 15),
            "V1 sticky header should be visible once a Work has been committed."
        )
        XCTAssertFalse(
            v1.getTrainNumber().isEmpty,
            "Sticky header TrainNumber label should be non-empty after the cascade picks a train."
        )
    }

    // MARK: — Test: V1Page_CycleMarker_AddsAndClearsMarkerBadge

    /// Marker pipeline: with an active train, tapping the UI_TEST CycleMarker
    /// seam (same handler as the SwipeItem Command binding) should make the
    /// first row's MarkerBadge visible. Tapping ClearMarker should hide it
    /// again. Covers the View→VM marker plumbing without depending on
    /// cross-platform SwipeView gesture reliability.
    func testV1Page_CycleMarker_AddsAndClearsMarkerBadge() throws {
        startHome.loadSample()
        _ = startHome.waitForWorkGroupList(timeout: 30)
        _ = startHome.autoOpenForTesting()

        let v1 = shell.navigateToOriginalTimetableV1()
        XCTAssertTrue(v1.waitForRendered(timeout: 30))
        XCTAssertTrue(v1.isHeaderVisible(timeout: 15))

        // Pre-condition: no MarkerBadge visible on any row yet.
        XCTAssertFalse(
            v1.isAnyMarkerBadgeVisible(timeout: 2),
            "Pre-condition failure: a MarkerBadge is already visible before any cycle."
        )

        v1.tapCycleMarkerRow0ForTesting()
        Thread.sleep(forTimeInterval: 0.6)

        XCTAssertTrue(
            v1.isAnyMarkerBadgeVisible(timeout: 8),
            "Cycling the first-row marker should make a MarkerBadge become visible."
        )

        v1.tapClearMarkerRow0ForTesting()
        Thread.sleep(forTimeInterval: 0.6)

        XCTAssertFalse(
            v1.isAnyMarkerBadgeVisible(timeout: 3),
            "Clearing the first-row marker should hide the MarkerBadge."
        )
    }
}
