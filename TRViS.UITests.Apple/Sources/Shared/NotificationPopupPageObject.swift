// NotificationPopupPageObject.swift
// XCUITest page object for the Notification (通告) popup shown when a
// server-pushed Notification is received and judged unread. Injected in
// UI_TEST builds via the trvis://_test/notification deeplink typed into the
// Connect-to-Server dialog. Mirrors TRViS.UITests/Pages/NotificationPopupPageObject.cs.

import XCTest

class NotificationPopupPageObject {

    private let app: XCUIApplication
    private let base: BaseUITestCase

    init(app: XCUIApplication, base: BaseUITestCase) {
        self.app = app
        self.base = base
    }

    func isDisplayed(timeout: TimeInterval = 10) -> Bool {
        return pollDisplayed(id: AutomationIds.Notification.title, timeout: timeout)
    }

    func isImportantBadgeVisible(timeout: TimeInterval = 3) -> Bool {
        return pollDisplayed(id: AutomationIds.Notification.importantBadge, timeout: timeout)
    }

    func isOrderNumberVisible(timeout: TimeInterval = 3) -> Bool {
        return pollDisplayed(id: AutomationIds.Notification.orderNumber, timeout: timeout)
    }

    var titleLabel: XCUIElement? {
        return base.waitForElement(id: AutomationIds.Notification.title, timeout: 10)
    }

    var acknowledgeButton: XCUIElement {
        return app.descendants(matching: .any)
            .matching(identifier: AutomationIds.Notification.acknowledgeButton)
            .firstMatch
    }

    var dismissButton: XCUIElement {
        return app.descendants(matching: .any)
            .matching(identifier: AutomationIds.Notification.dismissButton)
            .firstMatch
    }

    /// Taps 受領 (acknowledge + close). Closes the popup whether or not the
    /// server ack succeeds; only a confirmed send marks the notice read.
    func acknowledge() {
        acknowledgeButton.tap()
    }

    /// Taps 閉じる (close, informational/Id-less notices only).
    func dismiss() {
        dismissButton.tap()
    }

    /// Recovery helper for shared sessions: closes the popup with whichever
    /// control is present. Returns true if a control was tapped.
    @discardableResult
    func dismissAny() -> Bool {
        if pollDisplayed(id: AutomationIds.Notification.acknowledgeButton, timeout: 0.5) {
            acknowledge()
            return true
        }
        if pollDisplayed(id: AutomationIds.Notification.dismissButton, timeout: 0.5) {
            dismiss()
            return true
        }
        return false
    }

    /// Waits up to `timeout` for the popup to be gone (dismissed).
    func waitUntilDismissed(timeout: TimeInterval = 10) -> Bool {
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            if !pollDisplayed(id: AutomationIds.Notification.title, timeout: 0.5) {
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
