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

        // Fast path: one broad query first to avoid hammering multiple snapshots.
        let any = app.descendants(matching: .any).matching(identifier: id).firstMatch
        if any.waitForExistence(timeout: min(timeout, 2.0)) {
            return any
        }

        while Date() < deadline {
            for type in types {
                let el: XCUIElement
                switch type {
                case .button:
                    el = app.buttons.matching(identifier: id).firstMatch
                case .staticText:
                    el = app.staticTexts.matching(identifier: id).firstMatch
                case .image:
                    el = app.images.matching(identifier: id).firstMatch
                case .other:
                    el = app.otherElements.matching(identifier: id).firstMatch
                default:
                    el = app.descendants(matching: .any).matching(identifier: id).firstMatch
                }

                let remaining = deadline.timeIntervalSinceNow
                if remaining <= 0 {
                    return nil
                }
                if el.waitForExistence(timeout: min(0.5, remaining)) {
                    return el
                }
            }

            RunLoop.current.run(until: Date().addingTimeInterval(0.05))
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
