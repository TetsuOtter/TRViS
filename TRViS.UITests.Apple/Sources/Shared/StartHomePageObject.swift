// StartHomePageObject.swift
// Page object for the TRViS Start/Home page.
// Ports IsDisplayed(), IsPrivacyReconfirmBannerVisible(), AcceptPrivacyPolicyIfNeeded(),
// LoadSample(), OpenThirdPartyLicenses(), ClearLoaderForTesting(), AutoOpenForTesting(),
// IsWorkGroupChipVisible(), and WaitForWorkGroupList() from
// TRViS.UITests/Pages/StartHomePageObject.cs.

import XCTest

class StartHomePageObject {

    private let app: XCUIApplication
    private let base: BaseUITestCase

    init(app: XCUIApplication, base: BaseUITestCase) {
        self.app = app
        self.base = base
    }

    // MARK: — Core state predicates

    /// True when the StartHome page is displayed.
    ///
    /// Uses `testClearLoaderButton` (a `.button` element always present in the
    /// UI_TEST seam host) as the sentinel rather than `StartHome.Title`.
    /// On iOS 26 simulators, the Shell navigation-bar title Label is not exposed
    /// in the accessibility tree for 60+ s after flyout navigation, whereas the
    /// seam Buttons (added to RootGrid at construction time) surface on the first
    /// type check (`.button`). Uses a 60 s timeout to match the C# implementation.
    func isDisplayed(timeout: TimeInterval = 60) -> Bool {
        return base.waitForElement(id: AutomationIds.StartHome.testClearLoaderButton, timeout: timeout) != nil
    }

    /// True when the privacy-policy reconfirm banner is visible.
    ///
    /// MAUI's Border element does not map to a single predictable XCUIElementType.
    /// Use descendant matching on `identifier` (= accessibilityIdentifier, which
    /// MAUI sets from AutomationId) so we catch the element regardless of type.
    /// Polls for `timeout` seconds to absorb OnAppearing latency on first launch.
    func isPrivacyReconfirmBannerVisible(timeout: TimeInterval = 10) -> Bool {
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            // Use descendant(matching:) to probe all XCUIElementTypes at once.
            let match = app.descendants(matching: .any)
                .matching(identifier: AutomationIds.StartHome.privacyReconfirmBanner)
                .firstMatch
            if match.exists && match.isHittable {
                return true
            }
            // Also check the text label inside the banner — it's reliably surfaced
            // even when the containing Border isn't separately accessible.
            let textMatch = app.descendants(matching: .any)
                .matching(identifier: AutomationIds.StartHome.privacyReconfirmText)
                .firstMatch
            if textMatch.exists {
                return true
            }
            Thread.sleep(forTimeInterval: 0.2)
        }
        return false
    }

    // MARK: — Privacy acceptance

    /// Opens the privacy dialog and taps Save so feature buttons become enabled.
    /// Fast-paths (no-ops) when the privacy reconfirm banner is not on screen,
    /// meaning privacy was already accepted in a prior test in this session.
    /// Per-test-launch means each XCTest invocation is a fresh process, but the
    /// MAUI app persists NSUserDefaults across launches within the same run —
    /// so test 1 accepts, tests 2-N fast-path here.
    func acceptPrivacyPolicyIfNeeded() {
        guard isPrivacyReconfirmBannerVisible(timeout: 5) else { return }

        guard let privacyButton = base.waitForElement(
            id: AutomationIds.StartHome.privacyPolicyButton, timeout: 30
        ) else {
            XCTFail("PrivacyPolicyButton not found within 30 s")
            return
        }
        privacyButton.tap()

        guard let saveButton = base.waitForElement(
            id: AutomationIds.PrivacyDialog.saveButton, timeout: 60
        ) else {
            XCTFail("PrivacyDialog.SaveButton not found within 60 s")
            return
        }
        saveButton.tap()

        // Accept any system alert that some Firebase-enabled builds raise.
        let alert = app.alerts.firstMatch
        if alert.waitForExistence(timeout: 3) {
            let ok = alert.buttons["OK"]
            if ok.exists { ok.tap() }
        }

        // Wait for the dialog to dismiss back to the Start page.
        _ = base.waitForElement(id: AutomationIds.StartHome.testClearLoaderButton, timeout: 15)
        Thread.sleep(forTimeInterval: 0.3)
    }

    // MARK: — Sample data loading

    /// Taps the Load Demo button. After load, the page transitions to Home mode
    /// and the WorkGroup list becomes visible.
    func loadSample() {
        guard let loadDemoButton = base.waitForElement(
            id: AutomationIds.StartHome.loadDemoButton, timeout: 30
        ) else {
            XCTFail("LoadDemoButton not found")
            return
        }
        loadDemoButton.tap()
    }

    /// Waits up to `timeout` for the WorkGroupList to appear (Home mode).
    /// Returns the element if found, nil on timeout.
    @discardableResult
    func waitForWorkGroupList(timeout: TimeInterval = 30) -> XCUIElement? {
        return base.waitForElement(id: AutomationIds.StartHome.workGroupList, timeout: timeout)
    }

    // MARK: — WorkGroupChip visibility

    /// Returns true when the WorkGroupChip is on screen (tentative WG selected).
    /// Polls briefly (1 s) so the caller's assertion is not a race on layout.
    func isWorkGroupChipVisible(timeout: TimeInterval = 1) -> Bool {
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            let match = app.descendants(matching: .any)
                .matching(identifier: AutomationIds.StartHome.workGroupChip)
                .firstMatch
            if match.exists && match.isHittable {
                return true
            }
            Thread.sleep(forTimeInterval: 0.1)
        }
        return false
    }

    // MARK: — Third Party Licenses

    /// Taps the footer "Third Party Licenses" link and returns the page object.
    func openThirdPartyLicenses() -> ThirdPartyLicensesPageObject {
        guard let button = base.waitForElement(
            id: AutomationIds.StartHome.thirdPartyLicensesButton, timeout: 30
        ) else {
            XCTFail("ThirdPartyLicensesButton not found")
            return ThirdPartyLicensesPageObject(app: app, base: base)
        }
        button.tap()
        return ThirdPartyLicensesPageObject(app: app, base: base)
    }

    // MARK: — WorkGroupList visibility

    /// Polls up to `timeout` for the WorkGroupList to become visible (Home mode).
    /// Returns true once visible; false on timeout.
    func isWorkGroupListVisible(timeout: TimeInterval = 10) -> Bool {
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            let el = app.descendants(matching: .any)
                .matching(identifier: AutomationIds.StartHome.workGroupList)
                .firstMatch
            if el.exists { return true }
            Thread.sleep(forTimeInterval: 0.2)
        }
        return false
    }

    // MARK: — ConnectServer/SelectFile dialog openers

    /// Taps ConnectServerButton and returns the dialog page object.
    func openConnectServerDialog() -> ConnectServerDialogPageObject {
        guard let btn = base.waitForElement(
            id: AutomationIds.StartHome.connectServerButton, timeout: 30
        ) else {
            XCTFail("ConnectServerButton not found")
            return ConnectServerDialogPageObject(app: app, base: base)
        }
        btn.tap()
        return ConnectServerDialogPageObject(app: app, base: base)
    }

    /// Taps the TestOpenSelectFileDialog seam button and returns the dialog page object.
    /// Uses the seam button (not the styled SelectFileButton) to avoid dispatch issues.
    func openSelectFileDialog() -> SelectFileDialogPageObject {
        base.tapSeam(id: AutomationIds.StartHome.testOpenSelectFileDialogButton)
        return SelectFileDialogPageObject(app: app, base: base)
    }

    // MARK: — ConnectServer URL-history seams

    /// Seeds two URLs into the in-memory + persisted URL history.
    func seedUrlHistoryForTesting() {
        base.tapSeam(id: AutomationIds.StartHome.testSeedButton)
    }

    /// Clears URL history both in-memory and on disk.
    func clearUrlHistoryForTesting() {
        base.tapSeam(id: AutomationIds.StartHome.testClearHistoryButton)
    }

    // MARK: — SQLite / sample-file seams (SelectFile tests)

    /// Seeds a minimal SQLite fixture into TimetableFileDirectory.
    func seedSqliteForTesting() {
        base.tapSeam(id: AutomationIds.StartHome.testSeedSqliteButton)
    }

    /// Seeds a root JSON + sub-folder fixture into TimetableFileDirectory.
    func seedSampleFilesForTesting() {
        base.tapSeam(id: AutomationIds.StartHome.testSeedSampleFilesButton)
    }

    /// Wipes TimetableFileDirectory and clears any FilePicker override.
    /// Waits 500 ms after the tap to let the mode-switch animation settle.
    func clearSampleFilesForTesting() {
        base.tapSeam(id: AutomationIds.StartHome.testClearSampleFilesButton)
        Thread.sleep(forTimeInterval: 0.5)
    }

    /// Writes a JSON fixture into CacheDirectory and installs a FilePicker
    /// override that returns its path, so the Browse fallback test can run
    /// without driving the OS picker UI.
    func setupBrowseFallbackForTesting() {
        base.tapSeam(id: AutomationIds.StartHome.testSetupBrowseFallbackButton)
    }

    // MARK: — Language seams (LanguageSettings test / ScreenshotRegressionTests)

    /// Switches the UI language to English through the ViewModel path.
    func setLanguageEnglishForTesting() {
        base.tapSeam(id: AutomationIds.StartHome.testSetLanguageEnglishButton)
    }

    /// Switches the UI language to Japanese through the ViewModel path.
    func setLanguageJapaneseForTesting() {
        base.tapSeam(id: AutomationIds.StartHome.testSetLanguageJapaneseButton)
    }

    // MARK: — Clock freeze seams (used by ScreenshotRegressionTests)

    /// Pins AppTimeProvider at 09:41:00 so the DTAC live clock is pixel-stable.
    /// Must be called after sample data is loaded (seam button is only wired up
    /// once a Work is committed and the DTAC view model exists).
    func freezeClockForTesting() {
        base.tapSeam(id: AutomationIds.StartHome.testFreezeClockButton)
    }

    /// Releases the clock pin — restores live time.
    func unfreezeClockForTesting() {
        base.tapSeam(id: AutomationIds.StartHome.testUnfreezeClockButton)
    }

    // MARK: — Theme force seams (used by ScreenshotRegressionTests)

    /// Forces the app-wide theme to Light or Dark for deterministic cross-palette
    /// screenshots. Taps the appropriate seam button based on `dark`.
    func forceThemeForTesting(dark: Bool) {
        let id = dark
            ? AutomationIds.StartHome.testForceDarkThemeButton
            : AutomationIds.StartHome.testForceLightThemeButton
        base.tapSeam(id: id)
    }

    /// Resets the app-wide theme to Unspecified (follow OS) after the screenshot walk.
    func resetThemeForTesting() {
        base.tapSeam(id: AutomationIds.StartHome.testResetThemeButton)
    }

    // MARK: — Privacy Policy dialog (used by ScreenshotRegressionTests)

    /// Taps the footer "Privacy Policy" link and waits for the dialog to appear.
    func openPrivacyPolicyDialog() {
        guard let btn = base.waitForElement(
            id: AutomationIds.StartHome.privacyPolicyButton, timeout: 30
        ) else {
            XCTFail("PrivacyPolicyButton not found")
            return
        }
        btn.tap()
        // Wait for dialog title to confirm it has appeared.
        _ = base.waitForElement(id: AutomationIds.PrivacyDialog.title, timeout: 15)
    }

    /// Closes the Privacy Policy dialog by tapping its CloseButton.
    /// Waits for the seam button to confirm StartHome is visible again.
    func closePrivacyPolicyDialog() {
        guard let btn = base.waitForElement(
            id: AutomationIds.PrivacyDialog.closeButton, timeout: 15
        ) else {
            XCTFail("PrivacyDialog.CloseButton not found")
            return
        }
        btn.tap()
        _ = base.waitForElement(id: AutomationIds.StartHome.testClearLoaderButton, timeout: 10)
    }

    // MARK: — ConnectServerButton caption (LanguageSettings test)

    /// Returns the current label of the ConnectServerButton. Used by
    /// LanguageSettingsTests to verify the {loc:Translate}-bound caption flips
    /// to English after setLanguageEnglishForTesting().
    ///
    /// MAUI Button captions surface as `.label` on XCUITest. Falls back to
    /// `.value` if `.label` is empty (driver quirk on some Xcode versions).
    func connectServerButtonText() -> String {
        guard let btn = base.waitForElement(
            id: AutomationIds.StartHome.connectServerButton, timeout: 30
        ) else { return "" }
        let label = btn.label
        if !label.isEmpty { return label }
        return btn.value as? String ?? ""
    }

    // MARK: — Test seam buttons

    /// Taps the UI_TEST seam that clears the loader (returns to Start mode).
    func clearLoaderForTesting() {
        base.tapSeam(id: AutomationIds.StartHome.testClearLoaderButton)
    }

    /// Taps the UI_TEST seam that auto-selects the first WorkGroup+Work and navigates to DTAC.
    func autoOpenForTesting() -> DTACViewHostPageObject {
        base.tapSeam(id: AutomationIds.StartHome.testAutoOpenButton)
        return DTACViewHostPageObject(app: app, base: base)
    }

    // MARK: — WorkGroup count (DTACTimetableTests)

    /// Counts the number of child elements (WorkGroup rows) in the WorkGroupList.
    /// Mirrors C# CountWorkGroups() — returns descendants count as a proxy for
    /// row count; the exact number may exceed the work-group count on some
    /// platforms due to cell wrappers, so callers use >=2.
    func countWorkGroups() -> Int {
        guard let list = base.waitForElement(
            id: AutomationIds.StartHome.workGroupList, timeout: 10
        ) else {
            return 0
        }
        // Count direct cell/button children that have a non-empty label.
        // This mirrors the C# IReadOnlyList<IWebElement> approach: we care
        // that at least N meaningful rows are present, not internal wrappers.
        let descendants = list.descendants(matching: .any)
        var count = 0
        for i in 0..<descendants.count {
            let el = descendants.element(boundBy: i)
            if el.exists && !el.label.isEmpty {
                count += 1
            }
        }
        return count
    }

    // MARK: — GPS seed seam (DTACTimetableTests)

    /// Taps the UI_TEST seam that injects a fixture GPS coordinate into the
    /// LocationService. No real CoreLocation involved.
    func seedGpsLocationForTesting() {
        base.tapSeam(id: AutomationIds.StartHome.testSeedGpsButton)
    }

    // MARK: — NextTrain seed seam (DTACTimetableTests)

    /// Seeds a train selection with NextTrainId set (linear-train-1) and
    /// navigates to DTAC. Returns the DTAC page object.
    func seedTrainSelectionWithNextTrain() -> DTACViewHostPageObject {
        base.tapSeam(id: AutomationIds.StartHome.testSeedNextTrainSelectionButton)
        return DTACViewHostPageObject(app: app, base: base)
    }

    // MARK: — WebSocket seams (WebSocketReconnectTests / WebSocketStatusIndicatorTests)

    /// Taps the UI_TEST seam that sets a non-connected WebSocketNetworkSyncService
    /// loader and flips IsServerConnectionLost=true, putting Home into the #261
    /// "サーバー未接続 + 再接続" state without a real WebSocket server.
    func simulateWebSocketDisconnectForTesting() {
        base.tapSeam(id: AutomationIds.StartHome.testSimulateWebSocketDisconnectButton)
    }

    /// Taps the UI_TEST seam (#266) that builds a WebSocket-TYPED loader carrying
    /// real sample data, commits the first WG/Work and navigates to DTAC — landing
    /// with the AppBar status indicator in the Connected state.
    /// Returns the DTAC page object.
    func simulateWebSocketConnectedForTesting() -> DTACViewHostPageObject {
        base.tapSeam(id: AutomationIds.StartHome.testSimulateWebSocketConnectedButton)
        return DTACViewHostPageObject(app: app, base: base)
    }

    /// Returns true when the #261 reconnect button is visible (disconnected state).
    func isReconnectButtonVisible(timeout: TimeInterval = 8) -> Bool {
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            let el = app.descendants(matching: .any)
                .matching(identifier: AutomationIds.StartHome.reconnectButton)
                .firstMatch
            if el.exists && el.frame.size.width > 0 && el.frame.size.height > 0 {
                return true
            }
            Thread.sleep(forTimeInterval: 0.2)
        }
        return false
    }

    /// The ReconnectButton element (waits up to 10 s).
    var reconnectButton: XCUIElement? {
        return base.waitForElement(id: AutomationIds.StartHome.reconnectButton, timeout: 10)
    }

    /// The DisconnectButton element.
    var disconnectButton: XCUIElement? {
        return base.waitForElement(id: AutomationIds.StartHome.disconnectButton, timeout: 10)
    }

    /// The OpenButton element.
    var openButton: XCUIElement? {
        return base.waitForElement(id: AutomationIds.StartHome.openButton, timeout: 10)
    }

    /// Reads the LoaderInfoTitle text. MAUI Labels surface as AXValue; tries both
    /// `label` (AXLabel) and `value` (AXValue) so the read is robust.
    func loaderInfoTitleText(timeout: TimeInterval = 10) -> String {
        guard let el = base.waitForElement(
            id: AutomationIds.StartHome.loaderInfoTitle, timeout: timeout
        ) else { return "" }
        let label = el.label
        if !label.isEmpty { return label }
        return el.value as? String ?? ""
    }

    // MARK: — HorizontalTimetable seed seam (HorizontalTimetableTests)

    /// Seeds a Work with HasETrainTimetable=true and a 1×1 PNG payload,
    /// then commits and navigates to DTAC. Returns the DTAC page object.
    func seedHorizontalTimetableAndOpenForTesting() -> DTACViewHostPageObject {
        base.tapSeam(id: AutomationIds.StartHome.testSeedHorizontalTimetableButton)
        return DTACViewHostPageObject(app: app, base: base)
    }

    // MARK: — Constants (mirror C# StartHomePageObject literals)

    /// URLs seeded by seedUrlHistoryForTesting().
    static let seededHistoryUrls: [String] = [
        "https://example.com/timetable-a.json",
        "https://example.com/timetable-b.json",
    ]

    /// Filename written by seedSqliteForTesting().
    static let uiTestSqliteFixtureFileName = "uitest_seed.sqlite"

    /// Fixture file names written by seedSampleFilesForTesting().
    static let seededRootFileName   = "ui-test-root.json"
    static let seededSubFolderName  = "ui-test-folder"
    static let seededNestedFileName = "ui-test-nested.json"
}
