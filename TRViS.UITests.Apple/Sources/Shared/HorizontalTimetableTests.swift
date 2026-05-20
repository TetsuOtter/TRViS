// HorizontalTimetableTests.swift
// XCUITest port of TRViS.UITests/Tests/HorizontalTimetableTests.cs (Phase 2C).
//
// Covers: seeding a Work with HasETrainTimetable=true → DTAC → 時刻表 tab →
// HorizontalTimetableButton visible → tap → page displayed → back to DTAC.
//
// Per-test cold launch is used (BaseUITestCase.setUpWithError launches fresh).

import XCTest

final class HorizontalTimetableTests: BaseUITestCase {

    private var startHome: StartHomePageObject!

    override func setUpWithError() throws {
        try super.setUpWithError()
        startHome = StartHomePageObject(app: app, base: self)
        XCTAssertTrue(
            startHome.isDisplayed(),
            "StartHomePage should be displayed after launch (HorizontalTimetableTests setUp)."
        )
        startHome.acceptPrivacyPolicyIfNeeded()
        XCTAssertTrue(
            startHome.isDisplayed(),
            "StartHomePage should be displayed after accepting privacy."
        )
    }

    // MARK: — Test: HorizontalTimetableButton_Visible_NavigatesToHorizontalTimetablePage

    /// Mirrors C# HorizontalTimetableTests.HorizontalTimetableButton_Visible_NavigatesToHorizontalTimetablePage.
    ///
    /// Drives the full path: seed seam swaps in a synthetic Work with
    /// HasETrainTimetable=true + a 1×1 PNG, navigates to DTAC, taps the
    /// 時刻表 tab, and asserts the 横型時刻表 button is visible. We do not
    /// further assert WebView render content because cross-platform readback
    /// of WebView body is unreliable; reaching the page is the observable
    /// contract.
    func testHorizontalTimetableButton_Visible_NavigatesToHorizontalTimetablePage() throws {
        XCTAssertTrue(startHome.isDisplayed())
        startHome.loadSample()
        _ = startHome.waitForWorkGroupList(timeout: 30)

        let dtac = startHome.seedHorizontalTimetableAndOpenForTesting()
        XCTAssertTrue(
            dtac.isDisplayed(),
            "DTAC view should be displayed after seedHorizontalTimetableAndOpenForTesting."
        )
        dtac.switchToTimetableTab()

        XCTAssertTrue(
            dtac.isHorizontalTimetableButtonVisible(timeout: 5),
            "Seeded Work has HasETrainTimetable=true; the 横型時刻表 button should appear."
        )

        dtac.tapHorizontalTimetableButton()

        let htPage = HorizontalTimetablePageObject(app: app, base: self)
        XCTAssertTrue(
            htPage.isDisplayed(timeout: 30),
            "HorizontalTimetablePage's WebView should be in the accessibility tree after tap."
        )

        // Return to DTAC so the app is on a Shell-rooted page.
        // HT is a Shell.GoToAsync pushed page; the flyout is not reachable from it.
        _ = htPage.tapBack()
    }
}
