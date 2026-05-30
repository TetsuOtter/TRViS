// BaseUITestCase.swift
// XCTestCase base class for TRViS XCUITest suite.
//
// Design notes (from brief + advisor):
// - setUp() runs INSIDE the simulator runner; it cannot exec xcrun simctl,
//   defaults delete, or any host-shell operations. The runner script handles
//   cold-launch reset (uninstall/install for iOS, defaults-delete + container
//   wipe for Catalyst) before any test target runs.
// - Between-test reset uses the existing UI_TEST seam buttons wired into the
//   MAUI app (TestClear*, TestSeed*). Swift code taps them via XCUITest.
// - The `app` property targets dev.t0r.trvis by bundle ID, not the host app.

import XCTest

class BaseUITestCase: XCTestCase {

    // The MAUI app under test. Resolved by bundle ID so the test bundle drives
    // the pre-installed MAUI app regardless of which host the runner attached to.
    var app: XCUIApplication!

    // Cold-launch budget. MAUI runtime initialisation can take 30+ s on a fresh
    // simulator or a Catalyst sandbox on a busy CI runner.
    // Increased to 300s to accommodate CI runner load spikes that can block
    // the main thread during heavy MAUI initialization.
    let launchTimeout: TimeInterval = 300

    override func setUpWithError() throws {
        try super.setUpWithError()
        continueAfterFailure = false
        app = XCUIApplication(bundleIdentifier: "dev.t0r.trvis")
        app.launch()
    }

    override func tearDownWithError() throws {
        app.terminate()
        try super.tearDownWithError()
    }

    // MARK: — Element helpers

    /// Wait for an element identified by its AutomationId (accessibilityIdentifier)
    /// to appear in the accessibility tree. Probes multiple XCUIElementTypes because
    /// MAUI's mapping of control types to XCUIElementType is not always predictable.
    ///
    /// Returns the first matching element found within `timeout`, or nil on timeout.
    func waitForElement(
        id: String,
        timeout: TimeInterval = 180,
        types: [XCUIElement.ElementType] = [.button, .staticText, .image, .other, .any]
    ) -> XCUIElement? {
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            for type in types {
                let collection: XCUIElementQuery
                switch type {
                case .button:      collection = app.buttons
                case .staticText:  collection = app.staticTexts
                case .image:       collection = app.images
                case .other:       collection = app.otherElements
                default:
                    // For .any use descendant matching — covers all types
                    let el = app.descendants(matching: .any)
                        .matching(identifier: id).firstMatch
                    if el.exists { return el }
                    continue
                }
                let el = collection[id]
                if el.exists { return el }
            }
            Thread.sleep(forTimeInterval: 0.3)
        }
        return nil
    }

    /// Tap an AutomationId-identified element (UI_TEST seam button). Waits up to
    /// `timeout` for the element to appear before tapping. No-ops if not found.
    func tapSeam(id: String, timeout: TimeInterval = 10) {
        guard let el = waitForElement(id: id, timeout: timeout) else {
            XCTFail("Seam button '\(id)' not found within \(timeout)s")
            return
        }
        el.tap()
    }
}
