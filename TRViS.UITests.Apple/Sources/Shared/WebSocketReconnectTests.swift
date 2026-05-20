// WebSocketReconnectTests.swift
// Mirrors TRViS.UITests/Tests/WebSocketReconnectTests.cs.
//
// E2E for #261: when a WebSocket loader's connection drops, Home must stop
// showing the "server connected" loader-status title and instead show a
// 再接続 button.
//
// The disconnected state is reached through the UI_TEST-only seam
// TestSimulateWebSocketDisconnectButton (sets a non-connected WS loader +
// IsServerConnectionLost=true), so no real WebSocket server is needed.
//
// Per-test cold launch (BaseUITestCase.setUpWithError) is used here, which
// resets the in-process AppViewModel singleton. The C# fixture's shared-session
// recovery blocks (dialog-close, NavigateToHome, ClearLoader) are dropped —
// they are not needed with per-test launches.

import XCTest

final class WebSocketReconnectTests: BaseUITestCase {

    private var startHome: StartHomePageObject!

    override func setUpWithError() throws {
        try super.setUpWithError()
        startHome = StartHomePageObject(app: app, base: self)
        XCTAssertTrue(
            startHome.isDisplayed(),
            "StartHomePage should be displayed after launch (WebSocketReconnectTests setUp)."
        )
        startHome.acceptPrivacyPolicyIfNeeded()
        XCTAssertTrue(
            startHome.isDisplayed(),
            "StartHomePage should be displayed after accepting privacy."
        )
    }

    // MARK: — Test: WebSocketDisconnect_ShowsServerNotConnectedTitleAndReconnectButton

    /// Mirrors C# WebSocketReconnectTests.WebSocketDisconnect_ShowsServerNotConnectedTitleAndReconnectButton.
    ///
    /// Taps the disconnect seam and verifies:
    ///   1. The 再接続 button appears (primary, language-independent proof).
    ///   2. The LoaderInfoTitle is no longer the "server connected" caption.
    ///   3. The OpenButton and DisconnectButton remain visible (cached data accessible).
    func testWebSocketDisconnect_ShowsServerNotConnectedTitleAndReconnectButton() throws {
        // Load sample data to enter Home mode (required before disconnect seam).
        startHome.loadSample()
        _ = startHome.waitForWorkGroupList(timeout: 30)

        startHome.simulateWebSocketDisconnectForTesting()

        XCTAssertTrue(
            startHome.isReconnectButtonVisible(),
            "再接続 button must appear once the WebSocket connection is lost."
        )

        // Language-agnostic: the loader-status title is resx-resolved, so assert
        // it is NOT the "server connected" caption in either language.
        let title = startHome.loaderInfoTitleText()
        XCTAssertFalse(
            title.contains("サーバー接続中") || title.contains("Server connected"),
            "LoaderInfoCard title must switch away from the \"server connected\" caption once " +
            "the WebSocket connection is lost so the disconnect is visible (#261). Actual: \"\(title)\""
        )

        // 開く / 閉じる stay reachable: cached data is still browsable while disconnected.
        XCTAssertTrue(
            startHome.openButton?.exists == true,
            "OpenButton must still be visible while WebSocket is disconnected."
        )
        XCTAssertTrue(
            startHome.disconnectButton?.exists == true,
            "DisconnectButton must still be visible while WebSocket is disconnected."
        )
    }

    // MARK: — Test: ReconnectTap_WithoutStoredTarget_KeepsDisconnectedStateAndDoesNotCrash

    /// Mirrors C# WebSocketReconnectTests.ReconnectTap_WithoutStoredTarget_KeepsDisconnectedStateAndDoesNotCrash.
    ///
    /// The seam stores no reconnect target, so 再接続 → ReconnectWebSocketAsync
    /// returns false without touching the network. Tapping it must not crash and
    /// must leave the disconnected UI intact.
    func testReconnectTap_WithoutStoredTarget_KeepsDisconnectedStateAndDoesNotCrash() throws {
        // Load sample data to enter Home mode.
        startHome.loadSample()
        _ = startHome.waitForWorkGroupList(timeout: 30)

        startHome.simulateWebSocketDisconnectForTesting()
        XCTAssertTrue(
            startHome.isReconnectButtonVisible(),
            "Precondition: disconnected state must be shown before tapping 再接続."
        )

        guard let reconnect = startHome.reconnectButton else {
            XCTFail("ReconnectButton not found")
            return
        }
        reconnect.tap()

        // State preserved: still disconnected, button still available,
        // and the Home action row is intact (we did NOT navigate away or crash).
        XCTAssertTrue(
            startHome.isReconnectButtonVisible(),
            "再接続 must remain available when reconnection cannot proceed."
        )

        // Language-agnostic (see above): title must still NOT be the connected caption.
        let title = startHome.loaderInfoTitleText()
        XCTAssertFalse(
            title.contains("サーバー接続中") || title.contains("Server connected"),
            "LoaderInfoCard title must remain in the disconnected state after the 再接続 tap. Actual: \"\(title)\""
        )

        XCTAssertTrue(
            startHome.disconnectButton?.exists == true,
            "The Home page must still be intact (no crash / no navigation) after the 再接続 tap."
        )
    }
}
