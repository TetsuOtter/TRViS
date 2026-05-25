// OriginalTimetableV4PageObject.swift
// Page object for the V4 (Next Big) Original Timetable page.
// Mirrors TRViS.UITests/Pages/OriginalTimetableV4PageObject.cs.

import XCTest

class OriginalTimetableV4PageObject {

    private let app: XCUIApplication
    private let base: BaseUITestCase

    init(app: XCUIApplication, base: BaseUITestCase) {
        self.app = app
        self.base = base
    }

    /// Waits up to `timeout` for V4 to reach a rendered state. TrainStripe,
    /// Hero, MiniList, CompactMiniList, or EmptyState satisfies the wait.
    @discardableResult
    func waitForRendered(timeout: TimeInterval = 30) -> Bool {
        let anchors = [
            AutomationIds.OriginalTimetable.V4.trainStripe,
            AutomationIds.OriginalTimetable.V4.hero,
            AutomationIds.OriginalTimetable.V4.miniList,
            AutomationIds.OriginalTimetable.V4.compactMiniList,
            AutomationIds.OriginalTimetable.V4.emptyState,
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

    func isEmptyStateVisible(timeout: TimeInterval = 5) -> Bool {
        return base.waitForElement(
            id: AutomationIds.OriginalTimetable.V4.emptyState, timeout: timeout
        ) != nil
    }

    /// True when the persistent TrainStripe header is visible.
    func isTrainStripeVisible(timeout: TimeInterval = 5) -> Bool {
        return base.waitForElement(
            id: AutomationIds.OriginalTimetable.V4.trainStripe, timeout: timeout
        ) != nil
    }

    /// True when the Hero card is rendered (i.e. a next-arrival row exists).
    func isHeroVisible(timeout: TimeInterval = 5) -> Bool {
        return base.waitForElement(
            id: AutomationIds.OriginalTimetable.V4.hero, timeout: timeout
        ) != nil
    }

    /// Reads the TrainStripe TrainNumber Label, empty when not in the tree.
    func getTrainNumber() -> String {
        guard let el = base.waitForElement(
            id: AutomationIds.OriginalTimetable.V4.trainStripeTrainNumber, timeout: 10
        ) else { return "" }
        let label = el.label
        if !label.isEmpty { return label }
        if let v = el.value as? String { return v }
        return ""
    }

    /// True when the Hero card's MarkerBadge has a non-zero frame.
    func isHeroMarkerBadgeVisible(timeout: TimeInterval = 5) -> Bool {
        let id = AutomationIds.OriginalTimetable.V4.heroMarkerBadge
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            let el = app.descendants(matching: .any).matching(identifier: id).firstMatch
            if el.exists {
                let frame = el.frame
                if frame.size.width > 0 && frame.size.height > 0 {
                    return true
                }
            }
            Thread.sleep(forTimeInterval: 0.15)
        }
        return false
    }

    /// True when ANY MarkerBadge (V4-prefixed row or Hero) is visible.
    func isAnyMarkerBadgeVisible(timeout: TimeInterval = 5) -> Bool {
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            // V4 has Hero.MarkerBadge AND Row.<id>.MarkerBadge.
            let predicate = NSPredicate(
                format: "(identifier BEGINSWITH 'OriginalTimetable.V4.Row.' OR identifier == %@) AND identifier ENDSWITH '.MarkerBadge'",
                AutomationIds.OriginalTimetable.V4.heroMarkerBadge
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
