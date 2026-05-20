// EasterEggPageObject.swift
// Page object for the Settings (EasterEgg) page.
// Ports IsDisplayed() from TRViS.UITests/Pages/EasterEggPageObject.cs.

import XCTest

class EasterEggPageObject {

    private let app: XCUIApplication
    private let base: BaseUITestCase

    init(app: XCUIApplication, base: BaseUITestCase) {
        self.app = app
        self.base = base
    }

    // MARK: — Display check

    /// True when the Settings/EasterEgg page is visible (ReloadSavedButton is present).
    func isDisplayed(timeout: TimeInterval = 30) -> Bool {
        guard let el = base.waitForElement(
            id: AutomationIds.Settings.reloadSavedButton, timeout: timeout
        ) else {
            return false
        }
        return el.exists
    }
}
