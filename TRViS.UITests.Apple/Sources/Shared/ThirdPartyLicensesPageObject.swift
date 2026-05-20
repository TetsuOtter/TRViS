// ThirdPartyLicensesPageObject.swift
// Page object for the Third Party Licenses modal page.
// Ports IsDisplayed() and CloseModal() from
// TRViS.UITests/Pages/ThirdPartyLicensesPageObject.cs.

import XCTest

class ThirdPartyLicensesPageObject {

    private let app: XCUIApplication
    private let base: BaseUITestCase

    init(app: XCUIApplication, base: BaseUITestCase) {
        self.app = app
        self.base = base
    }

    // MARK: — Display check (ported from C# IsDisplayed)

    /// True when the Third Party Licenses page is visible (LicenseList is present).
    func isDisplayed(timeout: TimeInterval = 30) -> Bool {
        guard let el = base.waitForElement(
            id: AutomationIds.ThirdParty.licenseList, timeout: timeout
        ) else {
            return false
        }
        return el.exists
    }

    // MARK: — Modal close (ported from C# CloseModal)

    /// Taps the modal close (X) icon and waits until the license list is gone,
    /// returning to StartHomePage.
    func closeModal() -> StartHomePageObject {
        guard let closeButton = base.waitForElement(
            id: AutomationIds.ThirdParty.modalCloseButton, timeout: 30
        ) else {
            XCTFail("ThirdParty.ModalCloseButton not found within 30 s")
            return StartHomePageObject(app: app, base: base)
        }
        closeButton.tap()

        // Wait for the license list to disappear before returning.
        let deadline = Date().addingTimeInterval(10)
        while Date() < deadline {
            let match = app.descendants(matching: .any)
                .matching(identifier: AutomationIds.ThirdParty.licenseList)
                .firstMatch
            if !match.exists { break }
            Thread.sleep(forTimeInterval: 0.2)
        }

        return StartHomePageObject(app: app, base: base)
    }
}
