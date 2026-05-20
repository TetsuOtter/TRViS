// SelectFileDialogPageObject.swift
// XCUITest page object for the Select-File modal dialog.
// Ports only the methods called by SelectFileDialogTests.swift.
//
// Two visual states:
//   File list — rich cards for JSON/SQLite files in the app's documents folder,
//               plus breadcrumb + up-folder card for sub-directory navigation.
//   Empty state — shown when no supported files are present; shows browse button.

import XCTest

class SelectFileDialogPageObject {

    private let app: XCUIApplication
    private let base: BaseUITestCase

    init(app: XCUIApplication, base: BaseUITestCase) {
        self.app = app
        self.base = base
    }

    // MARK: — Displayed predicate

    /// True when the dialog title element is findable and exists.
    func isDisplayed(timeout: TimeInterval = 5) -> Bool {
        guard let el = base.waitForElement(
            id: AutomationIds.SelectFile.title, timeout: timeout
        ) else { return false }
        return el.exists
    }

    // MARK: — State predicates

    /// True when the file-list sub-view is active. Probes FileListHint (a Label)
    /// rather than the FileList ScrollView because ScrollView AutomationId isn't
    /// always surfaced by the accessibility tree — matches the C# rationale.
    func isFileListVisible(timeout: TimeInterval = 5) -> Bool {
        return pollDisplayed(id: AutomationIds.SelectFile.fileListHint, timeout: timeout)
    }

    /// True when the empty-state sub-view is active.
    func isEmptyStateVisible(timeout: TimeInterval = 5) -> Bool {
        return pollDisplayed(id: AutomationIds.SelectFile.emptyMessage, timeout: timeout)
    }

    /// True when the breadcrumb is shown (i.e. drilled into a sub-folder).
    /// Uses a short default timeout so "still at root" assertions are quick.
    func isBreadcrumbVisible(timeout: TimeInterval = 1) -> Bool {
        return pollDisplayed(id: AutomationIds.SelectFile.breadcrumb, timeout: timeout)
    }

    /// True when a sub-folder card with `folderName` is visible.
    func isFolderItemVisible(folderName: String, timeout: TimeInterval = 3) -> Bool {
        return pollDisplayed(
            id: AutomationIds.SelectFile.folderItemPrefix + folderName,
            timeout: timeout
        )
    }

    /// True when a file card with `fileName` is visible.
    func isFileItemVisible(fileName: String, timeout: TimeInterval = 3) -> Bool {
        return pollDisplayed(
            id: AutomationIds.SelectFile.fileItemPrefix + fileName,
            timeout: timeout
        )
    }

    // MARK: — Element accessors

    /// The breadcrumb label showing the current relative path.
    var breadcrumb: XCUIElement {
        return elementOrFail(id: AutomationIds.SelectFile.breadcrumb)
    }

    /// The "上の階層へ" (up-folder) card. Present only when not at the root.
    var upFolderItem: XCUIElement {
        return elementOrFail(id: AutomationIds.SelectFile.upFolderItem)
    }

    /// The "他の場所からファイルを開く" browse button. Always present.
    var browseButton: XCUIElement {
        return elementOrFail(id: AutomationIds.SelectFile.browseButton)
    }

    /// The "保存場所を開く" button. Hidden on Android by design; iOS always shows it.
    var openStorageLocationButton: XCUIElement {
        return elementOrFail(id: AutomationIds.SelectFile.openStorageLocationButton)
    }

    // MARK: — Per-row card accessors

    /// Returns the file card element for `fileName`.
    func fileItem(fileName: String) -> XCUIElement {
        return elementOrFail(id: AutomationIds.SelectFile.fileItemPrefix + fileName)
    }

    /// Returns the folder card element for `folderName`.
    func folderItem(folderName: String) -> XCUIElement {
        return elementOrFail(id: AutomationIds.SelectFile.folderItemPrefix + folderName)
    }

    // MARK: — Actions

    /// Taps a file card. On success the dialog dismisses.
    func tapFileItem(fileName: String) {
        fileItem(fileName: fileName).tap()
    }

    /// Taps a folder card. Drills into the sub-folder, replacing the list with
    /// the folder's contents and showing the breadcrumb.
    func tapFolderItem(folderName: String) {
        folderItem(folderName: folderName).tap()
    }

    /// Taps the up-folder card to navigate back to the parent directory.
    func tapUpFolder() {
        upFolderItem.tap()
    }

    /// Taps the browse button (invokes the OS FilePicker override or real picker).
    func tapBrowse() {
        browseButton.tap()
    }

    /// Closes the dialog and returns a StartHomePageObject.
    @discardableResult
    func close() -> StartHomePageObject {
        let closeBtn = elementOrFail(id: AutomationIds.SelectFile.closeButton)
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
        return app.descendants(matching: .any).matching(identifier: id).firstMatch
    }
}
