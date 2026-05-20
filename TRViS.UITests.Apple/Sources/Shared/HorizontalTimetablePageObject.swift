// HorizontalTimetablePageObject.swift
// Page object for the Horizontal Timetable page.
// Ports IsDisplayed() and TapBack() from
// TRViS.UITests/Pages/HorizontalTimetablePageObject.cs — Apple platform only.
//
// On iOS / macOS the wrapper Grid around the WebView carries
// AutomationId="HorizontalTimetable.WebView" (see HorizontalTimetablePage.cs
// comments), so a plain AccessibilityIdentifier lookup is sufficient.
// The Android/Windows class-name fallbacks in the C# port are not needed here.

import XCTest

class HorizontalTimetablePageObject {

    private let app: XCUIApplication
    private let base: BaseUITestCase

    init(app: XCUIApplication, base: BaseUITestCase) {
        self.app = app
        self.base = base
    }

    // MARK: — Display check (ported from C# IsDisplayed)

    /// True when the HorizontalTimetable page's WebView wrapper Grid is in the
    /// accessibility tree and has a non-zero frame (i.e., it is laid out).
    func isDisplayed(timeout: TimeInterval = 30) -> Bool {
        guard let el = base.waitForElement(
            id: AutomationIds.HorizontalTimetable.webView, timeout: timeout
        ) else {
            return false
        }
        return el.exists && el.frame.size.width > 0 && el.frame.size.height > 0
    }

    // MARK: — Navigation (ported from C# TapBack)

    /// Taps the AppBar back button to pop back to DTAC.
    /// HorizontalTimetable is a Shell.GoToAsync pushed page (not a flyout root),
    /// so the Shell flyout is not reachable from here. Returning to DTAC
    /// ensures the next fixture's NavigateToHome via the flyout can work.
    @discardableResult
    func tapBack() -> DTACViewHostPageObject {
        guard let btn = base.waitForElement(
            id: AutomationIds.HorizontalTimetable.backButton, timeout: 15
        ) else {
            XCTFail("HorizontalTimetable.BackButton not found")
            return DTACViewHostPageObject(app: app, base: base)
        }
        btn.tap()
        return DTACViewHostPageObject(app: app, base: base)
    }
}
