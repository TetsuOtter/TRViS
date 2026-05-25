// OriginalTimetableV2PageObject.swift
// Page object for the V2 (Card Stack) Original Timetable page.
// Mirrors TRViS.UITests/Pages/OriginalTimetableV2PageObject.cs.

import XCTest

class OriginalTimetableV2PageObject {

    private let app: XCUIApplication
    private let base: BaseUITestCase

    init(app: XCUIApplication, base: BaseUITestCase) {
        self.app = app
        self.base = base
    }

    // MARK: — Rendered state probe

    /// Waits up to `timeout` for V2 to reach a rendered state. Any of the
    /// tablet/compact header or empty-state anchors satisfies the wait.
    @discardableResult
    func waitForRendered(timeout: TimeInterval = 30) -> Bool {
        let anchors = [
            AutomationIds.OriginalTimetable.V2.header,
            AutomationIds.OriginalTimetable.V2.emptyState,
            AutomationIds.OriginalTimetable.V2.compactHeader,
            AutomationIds.OriginalTimetable.V2.compactEmptyState,
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

    // MARK: — State predicates

    /// True when any V2 empty-state Label (tablet or compact) is in the tree.
    func isEmptyStateVisible(timeout: TimeInterval = 5) -> Bool {
        if base.waitForElement(
            id: AutomationIds.OriginalTimetable.V2.emptyState, timeout: timeout
        ) != nil { return true }
        return base.waitForElement(
            id: AutomationIds.OriginalTimetable.V2.compactEmptyState, timeout: timeout
        ) != nil
    }

    /// True when the tablet sticky header is visible.
    func isHeaderVisible(timeout: TimeInterval = 5) -> Bool {
        return base.waitForElement(
            id: AutomationIds.OriginalTimetable.V2.header, timeout: timeout
        ) != nil
    }

    /// True when ANY MarkerBadge (any row, V2-prefixed) is visible. Used by
    /// the negative pre-condition test.
    func isAnyMarkerBadgeVisible(timeout: TimeInterval = 5) -> Bool {
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            let predicate = NSPredicate(
                format: "identifier BEGINSWITH 'OriginalTimetable.V2.Row.' AND identifier ENDSWITH '.MarkerBadge'"
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
