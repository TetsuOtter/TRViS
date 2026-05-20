// DTACViewHostPageObject.swift
// Page object for the DTAC ViewHost page.
// Ports IsDisplayed(), ReadTitleViaSeam(), ReadTimeViaSeam() from Phase 2A,
// plus the full timetable tab / button surface from Phase 2C.

import XCTest

class DTACViewHostPageObject {

    private let app: XCUIApplication
    private let base: BaseUITestCase

    init(app: XCUIApplication, base: BaseUITestCase) {
        self.app = app
        self.base = base
    }

    // MARK: — Display check (ported from C# IsDisplayed)

    /// True when the DTAC page is visible (MenuButton is present).
    /// Uses a 30 s timeout to absorb navigation + initial layout latency.
    func isDisplayed(timeout: TimeInterval = 30) -> Bool {
        guard let el = base.waitForElement(
            id: AutomationIds.DTAC.menuButton, timeout: timeout
        ) else {
            return false
        }
        return el.exists
    }

    // MARK: — Seam readers (ported from C# ReadTitleViaSeam / ReadTimeViaSeam)

    /// Reads the UI_TEST mirror Label that reflects AppBar's current title text.
    /// Re-queries the element each call so the value is not stale.
    /// Strips the sentinel prefix ("T:") before returning.
    func readTitleViaSeam() -> String {
        guard let el = base.waitForElement(
            id: AutomationIds.DTAC.testTitleSeam, timeout: 10
        ) else {
            return ""
        }
        let raw = el.label
        return stripSeamPrefix(raw, prefix: AutomationIds.DTAC.testSeamTitlePrefix)
    }

    /// Reads the UI_TEST mirror Label that reflects AppBar's current clock text.
    /// Re-queries the element each call so a live clock's value is not stale.
    /// Strips the sentinel prefix ("C:") before returning.
    func readTimeViaSeam() -> String {
        guard let el = base.waitForElement(
            id: AutomationIds.DTAC.testTimeSeam, timeout: 10
        ) else {
            return ""
        }
        let raw = el.label
        return stripSeamPrefix(raw, prefix: AutomationIds.DTAC.testSeamTimePrefix)
    }

    // MARK: — Timetable tab navigation (Phase 2C)

    /// Taps the 時刻表 tab and waits for TimetableScrollView to appear.
    /// Mirrors C# SwitchToTimetableTab() — 60 s timeout for iPad layout latency.
    @discardableResult
    func switchToTimetableTab() -> DTACViewHostPageObject {
        guard let tab = base.waitForElement(
            id: AutomationIds.DTAC.tabTimetable, timeout: 30
        ) else {
            XCTFail("TabTimetable not found within 30 s")
            return self
        }
        tab.tap()
        Thread.sleep(forTimeInterval: 0.3)
        // Wait for the surrounding ScrollView — VerticalTimetableView may not
        // surface reliably as an accessibility element on iOS.
        guard let _ = base.waitForElement(
            id: AutomationIds.DTAC.timetableScrollView, timeout: 60
        ) else {
            XCTFail("TimetableScrollView not found within 60 s after tab tap")
            return self
        }
        return self
    }

    // MARK: — TimetableScrollView element

    /// The timetable ScrollView (the GPS auto-scroll target).
    /// Use isDisplayed check on the returned element.
    func timetableScrollView() -> XCUIElement? {
        return base.waitForElement(id: AutomationIds.DTAC.timetableScrollView, timeout: 30)
    }

    // MARK: — StartEndRunButton / OpenCloseButton / LocationServiceButton

    /// Returns the first element matching `automationId` using a descendant
    /// query with `firstMatch`. This avoids "Multiple matching elements found"
    /// errors that occur when the accessibility tree exposes the same id through
    /// multiple XCUIElementType hierarchies simultaneously (observed on iOS 26
    /// for custom ContentView subclasses like OpenCloseButton).
    private func firstDescendant(id: String, timeout: TimeInterval = 10) -> XCUIElement? {
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            let el = app.descendants(matching: .any)
                .matching(identifier: id)
                .firstMatch
            if el.exists { return el }
            Thread.sleep(forTimeInterval: 0.3)
        }
        return nil
    }

    /// The 運行開始/運行終了 toggle button.
    func startEndRunButton() -> XCUIElement? {
        return firstDescendant(id: AutomationIds.DTAC.startEndRunButton)
    }

    /// The location-service toggle button.
    func locationServiceButton() -> XCUIElement? {
        return firstDescendant(id: AutomationIds.DTAC.locationServiceButton)
    }

    /// The open/close toggle button (collapses/expands the page header).
    func openCloseButton() -> XCUIElement? {
        return firstDescendant(id: AutomationIds.DTAC.openCloseButton)
    }

    /// Taps the StartEndRunButton.
    @discardableResult
    func tapStartEndRun() -> DTACViewHostPageObject {
        guard let btn = startEndRunButton() else {
            XCTFail("StartEndRunButton not found")
            return self
        }
        btn.tap()
        return self
    }

    /// Taps the OpenCloseButton.
    @discardableResult
    func tapOpenClose() -> DTACViewHostPageObject {
        guard let btn = openCloseButton() else {
            XCTFail("OpenCloseButton not found")
            return self
        }
        btn.tap()
        return self
    }

    // MARK: — NextTrainButton (Phase 2C)

    /// Returns true when the NextTrainButton is both present AND has a non-zero
    /// frame (i.e., it is genuinely laid out for the user to see).
    ///
    /// Mirrors C# IsNextTrainButtonPresent(): swipes up to scroll the timetable
    /// to expose the button before giving up. On iPhone the timetable Grid is
    /// wider than the screen, so XCUITest cascades isHittable=false from the
    /// off-screen parent. We use frame.size rather than isHittable to match the
    /// C# "Size.Width>0 && Height>0" heuristic.
    func isNextTrainButtonPresent(timeout: TimeInterval = 8) -> Bool {
        let deadline = Date().addingTimeInterval(timeout)
        var swipesDone = 0
        let maxSwipes = 4

        while Date() < deadline {
            if let el = base.waitForElement(
                id: AutomationIds.DTAC.nextTrainButton, timeout: 0.5
            ) {
                let frame = el.frame
                if frame.size.width > 0 && frame.size.height > 0 {
                    return true
                }
            }

            if swipesDone < maxSwipes {
                swipeTimetableUp()
                swipesDone += 1
            } else {
                return false
            }
        }
        return false
    }

    // MARK: — HorizontalTimetableButton (Phase 2C)

    /// Polls briefly for the HorizontalTimetableButton. Returns true only when
    /// the element exists and has a non-zero frame within the timeout.
    func isHorizontalTimetableButtonVisible(timeout: TimeInterval = 5) -> Bool {
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            if let el = base.waitForElement(
                id: AutomationIds.DTAC.horizontalTimetableButton, timeout: 0.5
            ) {
                let frame = el.frame
                if frame.size.width > 0 && frame.size.height > 0 {
                    return true
                }
            }
            Thread.sleep(forTimeInterval: 0.1)
        }
        return false
    }

    /// Taps the HorizontalTimetableButton.
    @discardableResult
    func tapHorizontalTimetableButton() -> DTACViewHostPageObject {
        guard let btn = base.waitForElement(
            id: AutomationIds.DTAC.horizontalTimetableButton, timeout: 10
        ) else {
            XCTFail("HorizontalTimetableButton not found")
            return self
        }
        btn.tap()
        return self
    }

    // MARK: — AppBar WebSocket status indicator (#266)

    /// Reads the UI_TEST mirror Label that reflects AppViewModel.ServerConnectionStatus.
    /// Strips the sentinel prefix ("S:") before returning.
    /// Returns "" when the element is not found within 10 s.
    func readConnectionStatusViaSeam() -> String {
        guard let el = base.waitForElement(
            id: AutomationIds.AppBar.connectionStatus, timeout: 10
        ) else { return "" }
        // MAUI Labels surface as AXValue; try label (AXLabel) then value (AXValue).
        let raw: String
        let labelText = el.label
        if !labelText.isEmpty {
            raw = labelText
        } else {
            raw = el.value as? String ?? ""
        }
        return stripSeamPrefix(raw, prefix: AutomationIds.AppBar.connectionStatusPrefix)
    }

    /// Polls the connection-status mirror until it equals `expected` or times out.
    /// Returns true when the expected value is reached; false on timeout.
    func waitForConnectionStatus(_ expected: String, timeout: TimeInterval = 8) -> Bool {
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            let current = readConnectionStatusViaSeam()
            if current == expected { return true }
            Thread.sleep(forTimeInterval: 0.2)
        }
        return false
    }

    /// Taps the DTAC-side seam that sets IsServerReconnecting=false, IsServerConnectionLost=false.
    func tapWsConnectedSeam() {
        base.tapSeam(id: AutomationIds.DTAC.testWsConnectedButton)
    }

    /// Taps the DTAC-side seam that sets IsServerConnectionLost=true.
    func tapWsDisconnectedSeam() {
        base.tapSeam(id: AutomationIds.DTAC.testWsDisconnectedButton)
    }

    /// Taps the DTAC-side seam that sets IsServerReconnecting=true.
    func tapWsReconnectingSeam() {
        base.tapSeam(id: AutomationIds.DTAC.testWsReconnectingButton)
    }

    // MARK: — IsInfoRow seam (Phase 2C)

    /// Taps the UI_TEST seam that flips row 0's IsInfoRow from false to true.
    @discardableResult
    func tapSeedIsInfoRowTransition() -> DTACViewHostPageObject {
        base.tapSeam(id: AutomationIds.DTAC.testSeedIsInfoRowTransitionButton)
        return self
    }

    // MARK: — Timetable row element lookup (Phase 2C)

    /// Returns true when the element for `automationId` exists and has a
    /// non-zero frame within `timeoutSeconds`.
    ///
    /// On iPhone the timetable Grid (width ≈ 740 pt) is wider than the screen
    /// (~390 pt), so XCUITest cascades isHittable=false from the off-screen
    /// parent down to every child. Frame size is the cross-platform-reliable
    /// "user can see it" signal, matching C# IsElementUserVisible().
    func isElementUserVisible(automationId: String, timeout: TimeInterval = 3) -> Bool {
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            let el = app.descendants(matching: .any)
                .matching(identifier: automationId)
                .firstMatch
            if el.exists {
                let frame = el.frame
                if frame.size.width > 0 && frame.size.height > 0 {
                    return true
                }
            }
            Thread.sleep(forTimeInterval: 0.1)
        }
        return false
    }

    // MARK: — Station name scanning (Phase 2C, StationNameDisplayTests)

    /// Reads the stripped text of the StationName label at `rowIndex`, or nil
    /// when the element is absent or has empty text.
    /// Mirrors C# ReadStrippedStationName() and Strip().
    ///
    /// MAUI Label text surfaces as AXValue in the iOS accessibility tree.
    /// XCUIElement.value contains AXValue; XCUIElement.label contains AXLabel
    /// (which MAUI leaves empty for plain Labels). We try both to be robust.
    func readStrippedStationName(rowIndex: Int) -> String? {
        let id = String(format: AutomationIds.DTAC.timetableRowStationNamePattern, rowIndex)
        let query = app.descendants(matching: .any).matching(identifier: id)
        // Iterate all matching elements — there may be multiple wrappers
        // with the same id at different levels of the MAUI/UIKit hierarchy.
        for i in 0..<min(query.count, 5) {
            let el = query.element(boundBy: i)
            guard el.exists else { continue }
            // Try label first (AXLabel), then value (AXValue).
            let labelText = el.label
            if !labelText.isEmpty {
                return stripSpacingChars(labelText)
            }
            if let v = el.value as? String, !v.isEmpty {
                return stripSpacingChars(v)
            }
        }
        return nil
    }

    /// Swipes the timetable upward by one viewport height to reveal lower rows.
    /// Mirrors C# TrySwipeUp() using XCUICoordinate.press(forDuration:thenDragTo:).
    func swipeTimetableUp() {
        let win = app.windows.firstMatch
        let startCoord = win.coordinate(
            withNormalizedOffset: CGVector(dx: 0.5, dy: 0.75)
        )
        let endCoord = win.coordinate(
            withNormalizedOffset: CGVector(dx: 0.5, dy: 0.25)
        )
        startCoord.press(forDuration: 0.05, thenDragTo: endCoord)
        Thread.sleep(forTimeInterval: 0.3)
    }

    // MARK: — Private helpers

    private func stripSeamPrefix(_ raw: String, prefix: String) -> String {
        if raw.hasPrefix(prefix) {
            return String(raw.dropFirst(prefix.count))
        }
        return raw
    }

    /// Strips the spacing characters StationNameConverter inserts between glyphs
    /// (EN SPACE U+2002 for 2/3-char, THIN SPACE U+2009 for 4-char).
    private func stripSpacingChars(_ s: String) -> String {
        return s.filter { $0 != "\u{2002}" && $0 != "\u{2009}" }
    }
}
