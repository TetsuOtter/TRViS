// OriginalTimetableV6PageObject.swift
// Page object for the V6 (Bold Editorial) Original Timetable page.
// Mirrors TRViS.UITests/Pages/OriginalTimetableV6PageObject.cs.

import XCTest

class OriginalTimetableV6PageObject {

    private let app: XCUIApplication
    private let base: BaseUITestCase

    init(app: XCUIApplication, base: BaseUITestCase) {
        self.app = app
        self.base = base
    }

    /// Waits up to `timeout` for V6 to reach a rendered state. Masthead,
    /// CurrentBlock, or EmptyState (tablet or compact mirror) satisfies the
    /// wait.
    @discardableResult
    func waitForRendered(timeout: TimeInterval = 30) -> Bool {
        let anchors = [
            AutomationIds.OriginalTimetable.V6.masthead,
            AutomationIds.OriginalTimetable.V6.currentBlock,
            AutomationIds.OriginalTimetable.V6.emptyState,
            AutomationIds.OriginalTimetable.V6.compactMasthead,
            AutomationIds.OriginalTimetable.V6.compactCurrentBlock,
            AutomationIds.OriginalTimetable.V6.compactEmptyState,
        ]
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            for id in anchors {
                if let _ = base.waitForElement(id: id, timeout: 0.5) {
                    return true
                }
            }
            Thread.sleep(forTimeInterval: 0.2)
        }
        return false
    }

    /// True when any V6 EmptyState Label (tablet or compact) is visible.
    func isEmptyStateVisible(timeout: TimeInterval = 5) -> Bool {
        if base.waitForElement(
            id: AutomationIds.OriginalTimetable.V6.emptyState, timeout: timeout
        ) != nil { return true }
        return base.waitForElement(
            id: AutomationIds.OriginalTimetable.V6.compactEmptyState, timeout: timeout
        ) != nil
    }

    /// True when the tablet CurrentBlock is visible.
    func isCurrentBlockVisible(timeout: TimeInterval = 5) -> Bool {
        return base.waitForElement(
            id: AutomationIds.OriginalTimetable.V6.currentBlock, timeout: timeout
        ) != nil
    }

    /// Reads the CurrentBlock's StationName Label, empty when not in the tree.
    func getCurrentStation() -> String {
        guard let el = base.waitForElement(
            id: AutomationIds.OriginalTimetable.V6.currentBlockStationName, timeout: 10
        ) else { return "" }
        let label = el.label
        if !label.isEmpty { return label }
        if let v = el.value as? String { return v }
        return ""
    }

    /// True when ANY MarkerBadge (V6-prefixed row OR CurrentBlock OR Compact)
    /// is visible. Used by the negative pre-condition test.
    func isAnyMarkerBadgeVisible(timeout: TimeInterval = 5) -> Bool {
        let deadline = Date().addingTimeInterval(timeout)
        let cbId = AutomationIds.OriginalTimetable.V6.currentBlockMarkerBadge
        let cbCompactId = AutomationIds.OriginalTimetable.V6.compactCurrentBlockMarkerBadge
        while Date() < deadline {
            let predicate = NSPredicate(
                format: "(identifier BEGINSWITH 'OriginalTimetable.V6.Row.' OR identifier == %@ OR identifier == %@) AND identifier ENDSWITH '.MarkerBadge'",
                cbId, cbCompactId
            )
            let query = app.descendants(matching: .any).matching(predicate)
            for i in 0..<min(query.count, 10) {
                let el = query.element(boundBy: i)
                if el.exists {
                    let frame = el.frame
                    if frame.size.width > 0 && frame.size.height > 0 {
                        return true
                    }
                }
            }
            Thread.sleep(forTimeInterval: 0.15)
        }
        return false
    }
}
