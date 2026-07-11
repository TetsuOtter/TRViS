// NotificationBannerPageObject.swift
// XCUITest page object for the small non-modal Notification (通告) banner
// overlaid on the DTAC ViewHost. Unlike NotificationPopupPageObject (a modal
// pushed globally by AppShell), this banner is owned by the DTAC page itself,
// so it is only reachable while on DTACViewHostPageObject. Mirrors
// TRViS.UITests/Pages/NotificationBannerPageObject.cs.

import XCTest

class NotificationBannerPageObject {

    private let app: XCUIApplication
    private let base: BaseUITestCase

    init(app: XCUIApplication, base: BaseUITestCase) {
        self.app = app
        self.base = base
    }

    var summaryLabel: XCUIElement? {
        return base.waitForElement(id: AutomationIds.Notification.Banner.summary, timeout: 10)
    }

    var acknowledgeButton: XCUIElement {
        return app.descendants(matching: .any)
            .matching(identifier: AutomationIds.Notification.Banner.acknowledgeButton)
            .firstMatch
    }

    func isShown(timeout: TimeInterval = 10) -> Bool {
        return pollDisplayed(id: AutomationIds.Notification.Banner.root, timeout: timeout)
    }

    func isAcknowledgeButtonVisible(timeout: TimeInterval = 3) -> Bool {
        return pollDisplayed(id: AutomationIds.Notification.Banner.acknowledgeButton, timeout: timeout)
    }

    /// Taps the banner (not the 受領 button) to expand into the large popup.
    func tapToExpand() -> NotificationPopupPageObject {
        // Tap the summary label rather than the Border root: the root's
        // AutomationId is on the Border, but its TapGestureRecognizer covers
        // the whole surface, so tapping any non-button child inside it
        // dispatches OnBannerTapped the same way.
        summaryLabel?.tap()
        return NotificationPopupPageObject(app: app, base: base)
    }

    /// Taps 受領 (acknowledge) on the banner itself, without expanding.
    func acknowledge() {
        acknowledgeButton.tap()
    }

    /// Waits up to `timeout` for the banner to be gone (dismissed or replaced).
    func waitUntilDismissed(timeout: TimeInterval = 10) -> Bool {
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            if !pollDisplayed(id: AutomationIds.Notification.Banner.root, timeout: 0.5) {
                return true
            }
        }
        return false
    }

    private func pollDisplayed(id: String, timeout: TimeInterval) -> Bool {
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            let el = app.descendants(matching: .any)
                .matching(identifier: id)
                .firstMatch
            if el.exists { return true }
            Thread.sleep(forTimeInterval: 0.15)
        }
        return false
    }
}
