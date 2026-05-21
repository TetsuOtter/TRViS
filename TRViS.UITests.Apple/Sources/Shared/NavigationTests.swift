// NavigationTests.swift
// Mirrors TRViS.UITests/Tests/NavigationTests.cs (5 tests).
//
// Each test uses a fresh per-test app launch (BaseUITestCase.setUp) and
// accepts the privacy policy if needed at the start of every test.
// No shared session is used; the C# shared-session setup recovery blocks
// are dropped — per-test launch makes them unnecessary.

import XCTest

final class NavigationTests: BaseUITestCase {

    // Per-test setUp: after super.setUpWithError() the app is freshly launched.
    // Accept privacy if the banner is visible (first test in a run), then
    // construct the shell page object.
    override func setUpWithError() throws {
        try super.setUpWithError()
        let startHome = StartHomePageObject(app: app, base: self)
        XCTAssertTrue(
            startHome.isDisplayed(),
            "StartHomePage should be displayed on launch (NavigationTests setUp)."
        )
        startHome.acceptPrivacyPolicyIfNeeded()
    }

    // MARK: — Test: Footer_OpenAndCloseThirdPartyLicenses

    /// Mirrors C# NavigationTests.Footer_OpenAndCloseThirdPartyLicenses.
    ///
    /// TPL is reached via the StartHome footer "Third Party Licenses" link
    /// (flyout entry was removed). Opens it, asserts it renders, closes via
    /// the modal X icon, asserts we return to StartHome.
    func testFooter_OpenAndCloseThirdPartyLicenses() throws {
        let startHome = StartHomePageObject(app: app, base: self)

        let tpl = startHome.openThirdPartyLicenses()
        XCTAssertTrue(
            tpl.isDisplayed(),
            "ThirdPartyLicenses modal should be displayed after tapping the footer link."
        )

        let startHomeAgain = tpl.closeModal()
        XCTAssertTrue(
            startHomeAgain.isDisplayed(),
            "StartHomePage should be displayed again after closing the TPL modal."
        )
    }

    // MARK: — Test: Flyout_NavigateToSettings

    /// Mirrors C# NavigationTests.Flyout_NavigateToSettings.
    func testFlyout_NavigateToSettings() throws {
        let shell = AppShellPageObject(app: app, base: self)
        let page = shell.navigateToSettings()
        XCTAssertTrue(
            page.isDisplayed(),
            "Settings (EasterEgg) page should be displayed after navigation."
        )
    }

    // MARK: — Test: Flyout_NavigateToDTAC

    /// Mirrors C# NavigationTests.Flyout_NavigateToDTAC.
    func testFlyout_NavigateToDTAC() throws {
        let shell = AppShellPageObject(app: app, base: self)
        let page = shell.navigateToDTAC()
        XCTAssertTrue(
            page.isDisplayed(),
            "DTACViewHost page should be displayed after navigation."
        )
    }

    // MARK: — Test: DTAC_ReopenAfterNavigateAway_ClockKeepsTicking

    /// Mirrors C# NavigationTests.DTAC_ReopenAfterNavigateAway_ClockKeepsTicking.
    ///
    /// Regression for #240: checks that the clock label keeps updating on the
    /// second DTAC visit. Reads via DTAC.TestTimeSeam so narrow-phone / empty-text
    /// iOS accessibility-tree limitations don't affect the result.
    func testDTAC_ReopenAfterNavigateAway_ClockKeepsTicking() throws {
        let shell = AppShellPageObject(app: app, base: self)

        let dtac = shell.navigateToDTAC()
        XCTAssertTrue(dtac.isDisplayed(), "DTAC should be visible on the first visit.")

        _ = shell.navigateToHome()

        let dtac2 = shell.navigateToDTAC()
        XCTAssertTrue(dtac2.isDisplayed(), "DTAC should be visible on the second visit.")

        let firstReading = dtac2.readTimeViaSeam()
        // LocationService ticks every ~1 s; sleep 1.5 s to guarantee at least
        // one tick even if the first read landed right after a tick.
        Thread.sleep(forTimeInterval: 1.5)
        let secondReading = dtac2.readTimeViaSeam()

        XCTAssertNotEqual(
            secondReading, firstReading,
            "Clock must keep updating on the second DTAC visit (#240). " +
            "First='\(firstReading)', Second='\(secondReading)'."
        )
    }

    // MARK: — Test: DTAC_ReopenAfterWorkSelected_TitleUpdated

    /// Mirrors C# NavigationTests.DTAC_ReopenAfterWorkSelected_TitleUpdated.
    ///
    /// Regression for #240 (title half): asserts AppBar title updates when a Work
    /// is selected after the initial empty DTAC visit that primes the broken codepath.
    func testDTAC_ReopenAfterWorkSelected_TitleUpdated() throws {
        let startHome = StartHomePageObject(app: app, base: self)
        let shell = AppShellPageObject(app: app, base: self)

        // Clear any leftover loader state (belt-and-suspenders on per-test launch,
        // but mirrors the C# fixture's intent explicitly).
        startHome.clearLoaderForTesting()

        // First DTAC visit with no Work selected — primes the broken codepath
        // by triggering Unloaded → Dispose on the way back.
        let dtac = shell.navigateToDTAC()
        XCTAssertTrue(dtac.isDisplayed(), "DTAC should be visible on the first visit.")

        let initialTitle = dtac.readTitleViaSeam()

        _ = shell.navigateToHome()

        // Load demo data and auto-open → DTAC with the first Work committed.
        startHome.loadSample()
        _ = startHome.waitForWorkGroupList(timeout: 30)

        let dtac2 = startHome.autoOpenForTesting()
        XCTAssertTrue(
            dtac2.isDisplayed(),
            "DTAC should be visible after AutoOpenForTesting."
        )

        // Title set goes through MainThread.BeginInvokeOnMainThread; give the
        // dispatcher a beat to flush before reading.
        Thread.sleep(forTimeInterval: 0.5)
        let title = dtac2.readTitleViaSeam()

        XCTAssertFalse(
            title.isEmpty,
            "AppBar Title must reflect the committed Work on the second DTAC visit (#240). " +
            "Got='\(title)'."
        )
        XCTAssertNotEqual(
            title, initialTitle,
            "AppBar Title must change after a Work is selected (#240). " +
            "Initial='\(initialTitle)', After='\(title)'."
        )
    }
}
