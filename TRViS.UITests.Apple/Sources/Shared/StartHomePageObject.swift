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

    // MARK: — Core state predicates (ported from C# StartHomePageObject)

    /// True when the StartHome page is displayed (title element is visible).
    /// Uses a 60 s timeout to match the C# implementation, which accounts for
    /// slow post-FirebaseSettings navigation on iOS macos-26 simulators.
    func isDisplayed(timeout: TimeInterval = 60) -> Bool {
        guard let el = base.waitForElement(id: AutomationIds.StartHome.title, timeout: timeout) else {
            return false
        }
        return el.exists
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

    // MARK: — Privacy acceptance (ported from C# AcceptPrivacyPolicyIfNeeded)

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
        _ = base.waitForElement(id: AutomationIds.StartHome.title, timeout: 30)
        Thread.sleep(forTimeInterval: 0.3)
    }

    // MARK: — Sample data loading (ported from C# LoadSample)

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

    // MARK: — WorkGroupChip visibility (ported from C# IsWorkGroupChipVisible)

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

    // MARK: — Third Party Licenses (ported from C# OpenThirdPartyLicenses)

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

    // MARK: — Test seam buttons (ported from C# ClearLoaderForTesting / AutoOpenForTesting)

    /// Taps the UI_TEST seam that clears the loader (returns to Start mode).
    func clearLoaderForTesting() {
        base.tapSeam(id: AutomationIds.StartHome.testClearLoaderButton)
    }

    /// Taps the UI_TEST seam that auto-selects the first WorkGroup+Work and navigates to DTAC.
    func autoOpenForTesting() -> DTACViewHostPageObject {
        base.tapSeam(id: AutomationIds.StartHome.testAutoOpenButton)
        return DTACViewHostPageObject(app: app, base: base)
    }
}
