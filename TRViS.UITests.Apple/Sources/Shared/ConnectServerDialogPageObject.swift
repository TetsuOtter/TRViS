// ConnectServerDialogPageObject.swift
// XCUITest page object for the Connect-to-Server modal dialog.
// Ports only the methods called by ConnectServerDialogTests.swift.
//
// Two visual states:
//   History list — rich cards keyed by URL, plus a "新規接続" button.
//   New-connection form — URL Entry, save-connection Switch, Connect button.

import XCTest

class ConnectServerDialogPageObject {

    private let app: XCUIApplication
    private let base: BaseUITestCase

    init(app: XCUIApplication, base: BaseUITestCase) {
        self.app = app
        self.base = base
    }

    // MARK: — Displayed predicate

    /// True when the dialog title element is findable and exists.
    /// Uses a short 5 s wait to match the C# PollDisplayed(timeoutSeconds:1) intent —
    /// if the dialog isn't open this returns quickly rather than blocking for 30 s.
    func isDisplayed(timeout: TimeInterval = 5) -> Bool {
        guard let el = base.waitForElement(
            id: AutomationIds.ConnectServer.title, timeout: timeout
        ) else { return false }
        return el.exists
    }

    // MARK: — State predicates

    /// True when the history list sub-view is the active view.
    /// Polls up to `timeout` to absorb the modal-open animation race.
    func isHistoryViewVisible(timeout: TimeInterval = 5) -> Bool {
        return pollDisplayed(id: AutomationIds.ConnectServer.historyList, timeout: timeout)
    }

    /// True when the new-connection form sub-view is the active view.
    /// Polls for the URL Entry's presence, which is the canonical indicator.
    func isNewConnectionFormVisible(timeout: TimeInterval = 5) -> Bool {
        return pollDisplayed(id: AutomationIds.ConnectServer.urlInput, timeout: timeout)
    }

    // MARK: — Element accessors

    /// The "新規接続" button shown in the history list state.
    var newConnectionButton: XCUIElement {
        return elementOrFail(id: AutomationIds.ConnectServer.newConnectionButton)
    }

    /// The "← 戻る" button shown when arriving at the new-connection form from history.
    var backToHistoryButton: XCUIElement {
        return elementOrFail(id: AutomationIds.ConnectServer.backToHistoryButton)
    }

    /// The URL text field in the new-connection form.
    var urlInput: XCUIElement {
        return elementOrFail(id: AutomationIds.ConnectServer.urlInput)
    }

    /// The Connect button in the new-connection form.
    var connectButton: XCUIElement {
        return elementOrFail(id: AutomationIds.ConnectServer.connectButton)
    }

    // MARK: — Per-row history card

    /// Returns the history card element for `url`.
    /// The whole card is tappable — selecting it triggers a load using the URL.
    func historyItem(url: String) -> XCUIElement {
        let id = AutomationIds.ConnectServer.historyItemPrefix + url
        return elementOrFail(id: id)
    }

    // MARK: — Actions

    /// Switches from the history list to the new-connection form.
    func openNewConnectionForm() {
        newConnectionButton.tap()
    }

    /// Returns from the new-connection form to the history list.
    func goBackToHistory() {
        backToHistoryButton.tap()
    }

    /// Closes the dialog and returns a StartHomePageObject.
    @discardableResult
    func close() -> StartHomePageObject {
        let closeBtn = elementOrFail(id: AutomationIds.ConnectServer.closeButton)
        closeBtn.tap()
        return StartHomePageObject(app: app, base: base)
    }

    // MARK: — Private helpers

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

    private func elementOrFail(id: String, timeout: TimeInterval = 30) -> XCUIElement {
        if let el = base.waitForElement(id: id, timeout: timeout) {
            return el
        }
        XCTFail("Element '\(id)' not found within \(timeout)s")
        // Return a dummy element so the caller can continue (XCTFail marks the test failed)
        return app.descendants(matching: .any).matching(identifier: id).firstMatch
    }
}
