// DTACViewHostPageObject.swift
// Page object for the DTAC ViewHost page.
// Ports IsDisplayed(), ReadTitleViaSeam(), and ReadTimeViaSeam() from
// TRViS.UITests/Pages/DTACViewHostPageObject.cs.
//
// Only the methods used by NavigationTests (Phase 2A) are ported here;
// broader DTAC functionality (timetable tabs, scroll, etc.) will be added
// in later phases as needed.

import XCTest

class DTACViewHostPageObject {

    private let app: XCUIApplication
    private let base: BaseUITestCase

    init(app: XCUIApplication, base: BaseUITestCase) {
        self.app = app
        self.base = base
    }

    // MARK: — Display check (ported from C# IsDisplayed)

    /// True when the DTAC page is visible (MenuButton is present).
    /// Uses a 30 s timeout to absorb navigation + initial layout latency.
    func isDisplayed(timeout: TimeInterval = 30) -> Bool {
        guard let el = base.waitForElement(
            id: AutomationIds.DTAC.menuButton, timeout: timeout
        ) else {
            return false
        }
        return el.exists
    }

    // MARK: — Seam readers (ported from C# ReadTitleViaSeam / ReadTimeViaSeam)

    /// Reads the UI_TEST mirror Label that reflects AppBar's current title text.
    /// Re-queries the element each call so the value is not stale.
    /// Strips the sentinel prefix ("T:") before returning.
    func readTitleViaSeam() -> String {
        // Re-query on every call — the label text updates asynchronously.
        guard let el = base.waitForElement(
            id: AutomationIds.DTAC.testTitleSeam, timeout: 10
        ) else {
            return ""
        }
        // XCUIElement.label returns the accessibilityLabel MAUI sets from the Text property.
        let raw = el.label
        return stripSeamPrefix(raw, prefix: AutomationIds.DTAC.testSeamTitlePrefix)
    }

    /// Reads the UI_TEST mirror Label that reflects AppBar's current clock text.
    /// Re-queries the element each call so a live clock's value is not stale.
    /// Strips the sentinel prefix ("C:") before returning.
    func readTimeViaSeam() -> String {
        guard let el = base.waitForElement(
            id: AutomationIds.DTAC.testTimeSeam, timeout: 10
        ) else {
            return ""
        }
        let raw = el.label
        return stripSeamPrefix(raw, prefix: AutomationIds.DTAC.testSeamTimePrefix)
    }

    // MARK: — Private helpers

    private func stripSeamPrefix(_ raw: String, prefix: String) -> String {
        if raw.hasPrefix(prefix) {
            return String(raw.dropFirst(prefix.count))
        }
        return raw
    }
}
