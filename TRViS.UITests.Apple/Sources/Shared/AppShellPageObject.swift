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
    private func waitForFlyoutItem(id: String, label: String, timeout: TimeInterval = 30) -> XCUIElement? {
        let deadline = Date().addingTimeInterval(timeout)
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

            Thread.sleep(forTimeInterval: 0.3)
        }
        return nil
    }

    // MARK: — Navigation (ported from C# AppShellPage)

    /// Navigates to the DTAC view via the Shell flyout.
    func navigateToDTAC() -> DTACViewHostPageObject {
        openFlyout()
        guard let item = waitForFlyoutItem(
            id: AutomationIds.Shell.Flyout.dtac, label: "D-TAC"
        ) else {
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
    /// flyout Home item, waits up to 60 s for StartHome.Title to appear so
    /// the caller can safely probe Start-mode elements.
    func navigateToHome() -> StartHomePageObject {
        openFlyout()
        guard let item = waitForFlyoutItem(
            id: AutomationIds.Shell.Flyout.startHome, label: "Home"
        ) else {
            XCTFail("Flyout item 'Home' (\(AutomationIds.Shell.Flyout.startHome)) not found")
            return StartHomePageObject(app: app, base: base)
        }
        item.tap()
        // Wait for StartHome page to become accessible. Use a 60 s budget to
        // absorb navigation animation + accessibility-tree repopulation latency
        // on iOS simulators (observed: ~10–20 s after GoToAsync completes).
        _ = base.waitForElement(id: AutomationIds.StartHome.title, timeout: 60)
        return StartHomePageObject(app: app, base: base)
    }
}
