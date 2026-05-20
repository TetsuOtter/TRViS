// WebSocketStatusIndicatorTests.swift
// XCUITest port of TRViS.UITests/Tests/WebSocketStatusIndicatorTests.cs (Phase 2D).
//
// E2E for #266: the shared AppBar shows a WebSocket connection-status indicator
// (green dot = connected, red dot = disconnected, spinner = connecting/reconnecting,
// hidden for non-WebSocket loaders).
//
// The AppBar is only shown on DTAC, so the test gets there via a UI_TEST seam
// that builds a WebSocket-TYPED loader carrying real sample data (no server),
// then drives the state through DTAC-side seams that mutate the singleton
// AppViewModel's connection flags. The indicator's state is asserted through
// the invisible AppBar.ConnectionStatus mirror Label ("S:" prefix + enum name).
//
// Per-test cold launch (BaseUITestCase.setUpWithError) is used, which resets
// the in-process AppViewModel singleton. The C# fixture's shared-session recovery
// blocks are dropped — unnecessary with per-test launches.

import XCTest

final class WebSocketStatusIndicatorTests: BaseUITestCase {

    private var startHome: StartHomePageObject!

    override func setUpWithError() throws {
        try super.setUpWithError()
        startHome = StartHomePageObject(app: app, base: self)
        XCTAssertTrue(
            startHome.isDisplayed(),
            "StartHomePage should be displayed after launch (WebSocketStatusIndicatorTests setUp)."
        )
        startHome.acceptPrivacyPolicyIfNeeded()
        XCTAssertTrue(
            startHome.isDisplayed(),
            "StartHomePage should be displayed after accepting privacy."
        )
    }

    // MARK: — Test: AppBar_WebSocketStatus_TransitionsThroughConnectedDisconnectedReconnecting

    /// Mirrors C# WebSocketStatusIndicatorTests.AppBar_WebSocketStatus_TransitionsThroughConnectedDisconnectedReconnecting.
    ///
    /// Drives the AppBar connection-status indicator through all four states via
    /// DTAC-side seams and asserts the mirror label reflects each transition:
    ///   Connected → Disconnected → Connecting (reconnecting) → Connected again.
    func testAppBar_WebSocketStatus_TransitionsThroughConnectedDisconnectedReconnecting() throws {
        // Navigate to DTAC with a WebSocket-TYPED loader via the #266 seam.
        let dtac = startHome.simulateWebSocketConnectedForTesting()
        XCTAssertTrue(
            dtac.isDisplayed(),
            "A WebSocket-typed sample loader + committed selection should land on DTAC."
        )

        // A live WS loader (not lost, not reconnecting) → green dot.
        let actualConnected = dtac.readConnectionStatusViaSeam()
        XCTAssertTrue(
            dtac.waitForConnectionStatus("Connected"),
            "AppBar indicator must be Connected when a live WebSocket loader is active. " +
            "Actual: \"\(actualConnected)\""
        )

        // Connection drops → red dot.
        dtac.tapWsDisconnectedSeam()
        let actualDisconnected = dtac.readConnectionStatusViaSeam()
        XCTAssertTrue(
            dtac.waitForConnectionStatus("Disconnected"),
            "AppBar indicator must switch to Disconnected when the connection is lost. " +
            "Actual: \"\(actualDisconnected)\""
        )

        // Auto-reconnect starts → spinner (takes priority over the lost flag).
        dtac.tapWsReconnectingSeam()
        let actualConnecting = dtac.readConnectionStatusViaSeam()
        XCTAssertTrue(
            dtac.waitForConnectionStatus("Connecting"),
            "AppBar indicator must switch to Connecting (spinner) while reconnecting. " +
            "Actual: \"\(actualConnecting)\""
        )

        // Reconnect succeeds → back to green (also clears the #261 lost flag).
        dtac.tapWsConnectedSeam()
        let actualReconnected = dtac.readConnectionStatusViaSeam()
        XCTAssertTrue(
            dtac.waitForConnectionStatus("Connected"),
            "AppBar indicator must return to Connected after a successful reconnect. " +
            "Actual: \"\(actualReconnected)\""
        )
    }
}
