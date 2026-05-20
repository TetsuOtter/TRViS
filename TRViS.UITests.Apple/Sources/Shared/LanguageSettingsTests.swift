// LanguageSettingsTests.swift
// Mirrors TRViS.UITests/Tests/LanguageSettingsTests.cs (1 test).
//
// C# excludes Linux (Android UIAutomator2 seam visibility issue).
// iOS is not excluded — this test is fully supported here.
//
// C# uses a shared session with SetUp recovery (close stray dialog, navigate home,
// clear loader, accept privacy). This class uses per-test cold launch, so all
// recovery is unnecessary.

import XCTest

final class LanguageSettingsTests: BaseUITestCase {

    private var startHome: StartHomePageObject!

    override func setUpWithError() throws {
        try super.setUpWithError()
        startHome = StartHomePageObject(app: app, base: self)
        XCTAssertTrue(
            startHome.isDisplayed(),
            "StartHomePage should be displayed after launch (LanguageSettingsTests setUp)."
        )
        startHome.acceptPrivacyPolicyIfNeeded()
        XCTAssertTrue(
            startHome.isDisplayed(),
            "StartHomePage should be displayed after accepting privacy (LanguageSettingsTests setUp)."
        )
    }

    // MARK: — Test: SwitchToEnglish_UpdatesBoundLabelLive

    /// Mirrors C# LanguageSettingsTests.SwitchToEnglish_UpdatesBoundLabelLive.
    ///
    /// After switching to English, the Start-mode primary ConnectServer button
    /// ({loc:Translate StartHome_ConnectServer}) must show the English resource value,
    /// proving the {loc:Translate} binding refreshed live on PropertyChanged("Item[]").
    func testSwitchToEnglish_UpdatesBoundLabelLive() throws {
        XCTAssertTrue(startHome.isDisplayed())

        startHome.setLanguageEnglishForTesting()

        // Poll for the PropertyChanged("Item[]") refresh to propagate to the
        // bound Button instead of a fixed sleep (latency varies with simulator load).
        // Read the caption every 100 ms for up to 5 s.
        var connectText = startHome.connectServerButtonText()
        let deadline = Date().addingTimeInterval(5)
        while !connectText.contains("Load from Server") && Date() < deadline {
            Thread.sleep(forTimeInterval: 0.1)
            connectText = startHome.connectServerButtonText()
        }

        XCTAssertTrue(
            connectText.contains("Load from Server"),
            "After switching the UI language to English, the " +
            "{loc:Translate}-bound Connect-to-Server button must show the " +
            "English resource value. Got='\(connectText)'."
        )
        XCTAssertFalse(
            connectText.contains("サーバーから読み込み"),
            "The Japanese caption must no longer be shown after switching to English. " +
            "Got='\(connectText)'."
        )
    }
}
