// AppShellPageObject.swift
// Page object for the TRViS App Shell (navigation flyout).
// Ports NavigateToDTAC(), NavigateToSettings(), NavigateToHome() from
// TRViS.UITests/Pages/AppShellPage.cs — Apple-platform only.
//
// Windows/Android paths are dropped; the Swift target never runs on those platforms.
// Catalyst vs iOS differences are gated on #if targetEnvironment(macCatalyst).

import XCTest

class AppShellPageObject {

    private let app: XCUIApplication
    private let base: BaseUITestCase

    init(app: XCUIApplication, base: BaseUITestCase) {
        self.app = app
        self.base = base
    }

    // MARK: — Flyout open helper

    /// Opens the Shell flyout.
    ///
    /// On Mac Catalyst the flyout toggle is the first button in the navigation bar.
    /// On iOS the flyout is opened by dragging from the left edge toward the centre
    /// using XCUICoordinate.press(forDuration:thenDragTo:), which translates to
    /// the same drag gesture the user would perform.
    private func openFlyout() {
        #if targetEnvironment(macCatalyst)
        // Catalyst: tap the hamburger button in the navigation bar.
        let navBar = app.navigationBars.firstMatch
        if navBar.exists {
            let hamburger = navBar.buttons.element(boundBy: 0)
            if hamburger.exists {
                hamburger.tap()
                return
            }
        }
        #else
        // iOS: drag from left edge (x=5) to screen centre.
        // XCUICoordinate.press(forDuration:thenDragTo:) generates a real swipe gesture
        // that the MAUI Shell SwipeGestureRecognizer picks up to open the flyout.
        let startCoord = app.windows.firstMatch.coordinate(
            withNormalizedOffset: CGVector(dx: 0.01, dy: 0.5)
        )
        let endCoord = app.windows.firstMatch.coordinate(
            withNormalizedOffset: CGVector(dx: 0.5, dy: 0.5)
        )
        startCoord.press(forDuration: 0.05, thenDragTo: endCoord)
        #endif
        Thread.sleep(forTimeInterval: 0.4)
    }

    /// Waits up to 30 s for a flyout item identified by AutomationId to appear.
    /// Falls back to a label-text search so the Shell flyout items are found
    /// regardless of the exact XCUIElementType MAUI maps them to.
    ///
    /// Every 5 s, if no flyout item from the known set is visible, the flyout
    /// drag is retried.  This recovers from the gesture being swallowed by a
    /// competing gesture recogniser on the underlying page (observed on iPad in
    /// dark/ja after Settings navigation).
    private func waitForFlyoutItem(id: String, label: String, timeout: TimeInterval = 30) -> XCUIElement? {
        let deadline = Date().addingTimeInterval(timeout)
        let retryInterval: TimeInterval = 5.0
        var lastDragAt = Date()

        while Date() < deadline {
            // Primary: AccessibilityIdentifier lookup
            if let el = base.waitForElement(id: id, timeout: 0.5) {
                return el
            }
            // Fallback: visible label text (all element types)
            let byLabel = app.descendants(matching: .any).matching(NSPredicate(
                format: "label == %@", label
            )).firstMatch
            if byLabel.exists { return byLabel }

            // Retry the flyout drag every 5 s if the flyout is not open.
            // Guard: if any known flyout item is already visible, the flyout IS
            // open — don't re-drag (that would close it).
            if Date().timeIntervalSince(lastDragAt) >= retryInterval {
                let knownFlyoutIds = [
                    AutomationIds.Shell.Flyout.startHome,
                    AutomationIds.Shell.Flyout.settings,
                    AutomationIds.Shell.Flyout.dtac,
                ]
                let flyoutIsOpen = knownFlyoutIds.contains(where: { fid in
                    app.descendants(matching: .any)
                        .matching(identifier: fid).firstMatch.exists
                })
                if !flyoutIsOpen {
                    openFlyout()
                    lastDragAt = Date()
                }
            }

            Thread.sleep(forTimeInterval: 0.3)
        }
        return nil
    }

    // MARK: — Diagnostics

    /// Attaches a full-screen screenshot to the current XCTest activity for
    /// post-mortem inspection when a flyout item is not found after the timeout.
    private func attachDiagnosticScreenshot(name: String) {
        let shot = XCUIScreen.main.screenshot()
        let attachment = XCTAttachment(screenshot: shot)
        attachment.name = "DIAGNOSTIC-\(name)"
        attachment.lifetime = .keepAlways
        XCTContext.runActivity(named: "Diagnostic screenshot: \(name)") { activity in
            activity.add(attachment)
        }
    }

    // MARK: — Navigation

    /// Navigates to the DTAC view via the Shell flyout.
    func navigateToDTAC() -> DTACViewHostPageObject {
        openFlyout()
        guard let item = waitForFlyoutItem(
            id: AutomationIds.Shell.Flyout.dtac, label: "D-TAC"
        ) else {
            attachDiagnosticScreenshot(name: "navigateToDTAC-flyout-not-found")
            XCTFail("Flyout item 'D-TAC' (\(AutomationIds.Shell.Flyout.dtac)) not found")
            return DTACViewHostPageObject(app: app, base: base)
        }
        item.tap()
        return DTACViewHostPageObject(app: app, base: base)
    }

    /// Navigates to the Settings (EasterEgg) page via the Shell flyout.
    func navigateToSettings() -> EasterEggPageObject {
        openFlyout()
        guard let item = waitForFlyoutItem(
            id: AutomationIds.Shell.Flyout.settings, label: "Settings"
        ) else {
            attachDiagnosticScreenshot(name: "navigateToSettings-flyout-not-found")
            XCTFail("Flyout item 'Settings' (\(AutomationIds.Shell.Flyout.settings)) not found")
            return EasterEggPageObject(app: app, base: base)
        }
        item.tap()
        return EasterEggPageObject(app: app, base: base)
    }

    /// Navigates back to StartHome via the Shell flyout.
    ///
    /// Uses the flyout drag on iOS (Apple platforms don't have Android's
    /// orientation-lock / DrawerLayout reliability issues that motivated the
    /// seam-first approach in the C# Appium driver).  After tapping the
    /// flyout Home item, waits for the seam button (`testClearLoaderButton`)
    /// to confirm StartHome is accessible.
    ///
    /// `StartHome.Title` (the Shell navigation-bar Label) is intentionally NOT
    /// used as the sentinel: on iOS 26 simulators it fails to surface in the
    /// accessibility tree for 60+ s after flyout navigation, causing each call
    /// to exhaust its full timeout.  The seam Buttons (added to RootGrid at
    /// page-construction time) appear on the first type check (`.button`) and
    /// are a reliable page-presence indicator.
    func navigateToHome() -> StartHomePageObject {
        openFlyout()
        guard let item = waitForFlyoutItem(
            id: AutomationIds.Shell.Flyout.startHome, label: "Home"
        ) else {
            attachDiagnosticScreenshot(name: "navigateToHome-flyout-not-found")
            XCTFail("Flyout item 'Home' (\(AutomationIds.Shell.Flyout.startHome)) not found")
            return StartHomePageObject(app: app, base: base)
        }
        item.tap()
        _ = base.waitForElement(id: AutomationIds.StartHome.testClearLoaderButton, timeout: 30)
        return StartHomePageObject(app: app, base: base)
    }

    /// Navigates to the V1 (Modern Classic) Original Timetable page via the
    /// Shell flyout. Used by OriginalTimetableV1Tests and the V1 screenshot
    /// regression test on iPad mini A17 (744pt portrait — exceeds the 600pt
    /// tablet breakpoint so V1 renders its CollectionView layout).
    func navigateToOriginalTimetableV1() -> OriginalTimetableV1PageObject {
        openFlyout()
        guard let item = waitForFlyoutItem(
            id: AutomationIds.Shell.Flyout.originalTimetableV1,
            label: "ダイヤ表 (V1)"
        ) else {
            attachDiagnosticScreenshot(name: "navigateToV1-flyout-not-found")
            XCTFail("Flyout item 'ダイヤ表 (V1)' (\(AutomationIds.Shell.Flyout.originalTimetableV1)) not found")
            return OriginalTimetableV1PageObject(app: app, base: base)
        }
        item.tap()
        return OriginalTimetableV1PageObject(app: app, base: base)
    }

    /// Navigates to the V2 (Card Stack) Original Timetable page via the Shell
    /// flyout. iPad mini A17 portrait (744pt) renders the tablet stacked-card
    /// layout; iPhone widths render the compact CollectionView.
    func navigateToOriginalTimetableV2() -> OriginalTimetableV2PageObject {
        openFlyout()
        guard let item = waitForFlyoutItem(
            id: AutomationIds.Shell.Flyout.originalTimetableV2,
            label: "ダイヤ表 (V2)"
        ) else {
            attachDiagnosticScreenshot(name: "navigateToV2-flyout-not-found")
            XCTFail("Flyout item 'ダイヤ表 (V2)' (\(AutomationIds.Shell.Flyout.originalTimetableV2)) not found")
            return OriginalTimetableV2PageObject(app: app, base: base)
        }
        item.tap()
        return OriginalTimetableV2PageObject(app: app, base: base)
    }

    /// Navigates to the V4 (Next Big) Original Timetable page via the Shell flyout.
    func navigateToOriginalTimetableV4() -> OriginalTimetableV4PageObject {
        openFlyout()
        guard let item = waitForFlyoutItem(
            id: AutomationIds.Shell.Flyout.originalTimetableV4,
            label: "ダイヤ表 (V4)"
        ) else {
            attachDiagnosticScreenshot(name: "navigateToV4-flyout-not-found")
            XCTFail("Flyout item 'ダイヤ表 (V4)' (\(AutomationIds.Shell.Flyout.originalTimetableV4)) not found")
            return OriginalTimetableV4PageObject(app: app, base: base)
        }
        item.tap()
        return OriginalTimetableV4PageObject(app: app, base: base)
    }

    /// Navigates to the V6 (Bold Editorial) Original Timetable page via the Shell flyout.
    func navigateToOriginalTimetableV6() -> OriginalTimetableV6PageObject {
        openFlyout()
        guard let item = waitForFlyoutItem(
            id: AutomationIds.Shell.Flyout.originalTimetableV6,
            label: "ダイヤ表 (V6)"
        ) else {
            attachDiagnosticScreenshot(name: "navigateToV6-flyout-not-found")
            XCTFail("Flyout item 'ダイヤ表 (V6)' (\(AutomationIds.Shell.Flyout.originalTimetableV6)) not found")
            return OriginalTimetableV6PageObject(app: app, base: base)
        }
        item.tap()
        return OriginalTimetableV6PageObject(app: app, base: base)
    }
}
