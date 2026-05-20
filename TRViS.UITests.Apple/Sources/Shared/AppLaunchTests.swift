// AppLaunchTests.swift
// Mirrors TRViS.UITests/Tests/AppLaunchTests.cs.
//
// Covers both iOS simulator and Mac Catalyst targets from a single source file.
// The runner script (run-ui-tests-apple.sh) ensures a clean-install state before
// the first test run (iOS: simctl uninstall + install; Catalyst: defaults delete +
// container wipe), so the privacy banner is guaranteed to be visible.

import XCTest

final class AppLaunchTests: BaseUITestCase {

    /// Mirrors C# AppLaunchTests.App_Launches_Into_StartHome_With_Privacy_Banner
    ///
    /// On a clean install (runner has wiped NSUserDefaults / the app container),
    /// TRViS navigates directly to StartHomePage in Start mode and shows the
    /// privacy-policy reconfirm banner until the user accepts via the in-page
    /// privacy dialog.
    func testApp_Launches_Into_StartHome_With_Privacy_Banner() throws {
        let startHome = StartHomePageObject(app: app, base: self)

        XCTAssertTrue(
            startHome.isDisplayed(),
            "StartHomePage should be displayed on first launch."
        )

        XCTAssertTrue(
            startHome.isPrivacyReconfirmBannerVisible(),
            "Privacy reconfirm banner should be visible on a fresh install."
        )
    }
}
