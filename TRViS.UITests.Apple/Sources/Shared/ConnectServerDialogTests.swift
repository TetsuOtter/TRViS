// ConnectServerDialogTests.swift
// Mirrors TRViS.UITests/Tests/ConnectServerDialogTests.cs (2 tests).
//
// C# has ShareSessionAcrossTestsInFixture=true (shared Appium session) plus
// SetUp recovery blocks (close stray dialog, clear loader, accept privacy).
// This class uses per-test cold launch (BaseUITestCase.setUpWithError), which
// makes all session-recovery logic unnecessary.
//
// The C# Platform(Exclude="Win") test is included — we're on iOS only here
// and the Win exclusion is irrelevant.

import XCTest

final class ConnectServerDialogTests: BaseUITestCase {

    private var startHome: StartHomePageObject!

    override func setUpWithError() throws {
        try super.setUpWithError()
        startHome = StartHomePageObject(app: app, base: self)
        XCTAssertTrue(
            startHome.isDisplayed(),
            "StartHomePage should be displayed after launch (ConnectServerDialogTests setUp)."
        )
        startHome.acceptPrivacyPolicyIfNeeded()
        XCTAssertTrue(
            startHome.isDisplayed(),
            "StartHomePage should be displayed after accepting privacy (ConnectServerDialogTests setUp)."
        )
    }

    // MARK: — Test: OpenDialog_WithEmptyHistory_ShowsNewConnectionFormDirectly

    /// Mirrors C# ConnectServerDialogTests.OpenDialog_WithEmptyHistory_ShowsNewConnectionFormDirectly.
    ///
    /// On a clean install (no URL history), the dialog should skip the empty-list
    /// state and show the new-connection form directly.
    ///
    /// Even with per-test cold launch, the NSUserDefaults container is warm within
    /// a single xcodebuild-test run (the run-ui-tests-apple.sh script only wipes
    /// the container before the run starts). Earlier tests in the same run might
    /// have seeded history, so we call clearUrlHistoryForTesting() explicitly —
    /// mirroring the C# comment about noReset:true in-memory retention.
    func testOpenDialog_WithEmptyHistory_ShowsNewConnectionFormDirectly() throws {
        XCTAssertTrue(startHome.isDisplayed())

        // Wipe any persisted URL history left by a prior test in this run.
        startHome.clearUrlHistoryForTesting()
        Thread.sleep(forTimeInterval: 0.2)

        let dialog = startHome.openConnectServerDialog()

        XCTAssertTrue(
            dialog.isDisplayed(),
            "Dialog should be displayed."
        )
        XCTAssertTrue(
            dialog.isNewConnectionFormVisible(),
            "With no history, the dialog should default to the new-connection form."
        )
        XCTAssertTrue(
            dialog.urlInput.exists,
            "URL input should be visible in the new-connection form."
        )
        XCTAssertTrue(
            dialog.connectButton.exists,
            "Connect button should be visible in the new-connection form."
        )
    }

    // MARK: — Test: SeededHistory_DialogTransitions_FullFlow

    /// Mirrors C# ConnectServerDialogTests.SeededHistory_DialogTransitions_FullFlow.
    ///
    /// Seeds URL history, opens the dialog, verifies the history-list state,
    /// transitions to the new-connection form, goes back, and closes.
    func testSeededHistory_DialogTransitions_FullFlow() throws {
        XCTAssertTrue(
            startHome.isDisplayed(),
            "StartHome should be displayed before opening the dialog."
        )

        startHome.seedUrlHistoryForTesting()
        Thread.sleep(forTimeInterval: 0.3)

        // Open: history list state shows because history is non-empty.
        let dialog = startHome.openConnectServerDialog()
        XCTAssertTrue(
            dialog.isDisplayed(),
            "ConnectServer dialog should be displayed after openConnectServerDialog()."
        )
        XCTAssertTrue(
            dialog.isHistoryViewVisible(),
            "With seeded history, the dialog should default to the history list state."
        )
        XCTAssertTrue(
            dialog.newConnectionButton.exists,
            "'+ 新規接続' button should be visible in the history list state."
        )

        // Per-row card AutomationIds — regression for ConnectServer.HistoryItem.<url> pattern.
        let urlA = StartHomePageObject.seededHistoryUrls[0]
        let urlB = StartHomePageObject.seededHistoryUrls[1]
        XCTAssertTrue(
            dialog.historyItem(url: urlA).exists,
            "History card for '\(urlA)' should be reachable by AutomationId."
        )
        XCTAssertTrue(
            dialog.historyItem(url: urlB).exists,
            "History card for '\(urlB)' should be reachable by AutomationId."
        )

        // "+ 新規接続" → form
        dialog.openNewConnectionForm()
        Thread.sleep(forTimeInterval: 0.3)
        XCTAssertTrue(
            dialog.isNewConnectionFormVisible(),
            "Tapping '+ 新規接続' should switch to the new-connection form."
        )
        XCTAssertTrue(
            dialog.backToHistoryButton.exists,
            "Back-to-history affordance should be visible when arriving at the form from non-empty history."
        )

        // Back → history list
        dialog.goBackToHistory()
        Thread.sleep(forTimeInterval: 0.3)
        XCTAssertTrue(
            dialog.isHistoryViewVisible(),
            "Tapping back should return to the history list."
        )

        // Close → StartHome
        let back = dialog.close()
        Thread.sleep(forTimeInterval: 0.3)
        XCTAssertTrue(
            back.isDisplayed(),
            "After Close the dialog should dismiss and StartHomePage should be visible again."
        )
    }
}
