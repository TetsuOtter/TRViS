// OriginalTimetableV1PageObject.swift
// Page object for the V1 (Modern Classic) Original Timetable page.
// Mirrors TRViS.UITests/Pages/OriginalTimetableV1PageObject.cs.

import XCTest

class OriginalTimetableV1PageObject {

    private let app: XCUIApplication
    private let base: BaseUITestCase

    init(app: XCUIApplication, base: BaseUITestCase) {
        self.app = app
        self.base = base
    }

    // MARK: — Rendered state probe

    /// Waits up to `timeout` for V1 to reach a rendered state: either the
    /// sticky train-info header (active-train state) or the empty-state
    /// Label (no-train state). Returns true on first observation, false on
    /// timeout.
    @discardableResult
    func waitForRendered(timeout: TimeInterval = 30) -> Bool {
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            if let _ = base.waitForElement(
                id: AutomationIds.OriginalTimetable.V1.header, timeout: 0.5
            ) { return true }
            if let _ = base.waitForElement(
                id: AutomationIds.OriginalTimetable.V1.emptyState, timeout: 0.5
            ) { return true }
            Thread.sleep(forTimeInterval: 0.2)
        }
        return false
    }

    // MARK: — State predicates

    /// True when the empty-state Label is currently visible in the
    /// accessibility tree (no ActiveTrain).
    func isEmptyStateVisible(timeout: TimeInterval = 5) -> Bool {
        return base.waitForElement(
            id: AutomationIds.OriginalTimetable.V1.emptyState, timeout: timeout
        ) != nil
    }

    /// True when the sticky header (TrainNumber label) is visible.
    func isHeaderVisible(timeout: TimeInterval = 5) -> Bool {
        return base.waitForElement(
            id: AutomationIds.OriginalTimetable.V1.headerTrainNumber, timeout: timeout
        ) != nil
    }

    /// Reads the TrainNumber label text from the sticky header. Returns "" if
    /// the label is not currently in the tree. MAUI Labels surface as AXValue
    /// in XCUITest; we try label (AXLabel) then value (AXValue) for
    /// robustness.
    func getTrainNumber() -> String {
        guard let el = base.waitForElement(
            id: AutomationIds.OriginalTimetable.V1.headerTrainNumber, timeout: 10
        ) else {
            return ""
        }
        let label = el.label
        if !label.isEmpty { return label }
        if let v = el.value as? String { return v }
        return ""
    }

    // MARK: — Per-row probes

    /// True when the row's MarkerBadge AutomationId is in the tree and has a
    /// non-zero frame. The badge is hidden (HasMarker=false) while
    /// MarkerKind=None, so this returning true is evidence the row is marked.
    func isMarkerBadgeVisible(rowId: String, timeout: TimeInterval = 5) -> Bool {
        let id = AutomationIds.OriginalTimetable.V1.markerBadge(rowId)
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            let el = app.descendants(matching: .any)
                .matching(identifier: id).firstMatch
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

    /// True when ANY MarkerBadge (any row) is visible in the tree. Used by
    /// marker-cycle tests where the seam-targeted row's Id is not known at
    /// the test side (the seam picks "first normal row" internally).
    func isAnyMarkerBadgeVisible(timeout: TimeInterval = 5) -> Bool {
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            // NSPredicate ENDSWITH against accessibilityIdentifier.
            let predicate = NSPredicate(format: "identifier ENDSWITH '.MarkerBadge'")
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

    // MARK: — Test seams

    /// Taps the UI_TEST seam button that drives the same OnCycleMarker
    /// handler the SwipeView SwipeItem Command binding points at, targeting
    /// the first normal (non-section-break) row.
    func tapCycleMarkerRow0ForTesting() {
        base.tapSeam(id: AutomationIds.OriginalTimetable.V1.testCycleMarkerRow0Button)
    }

    /// Inverse of `tapCycleMarkerRow0ForTesting()`.
    func tapClearMarkerRow0ForTesting() {
        base.tapSeam(id: AutomationIds.OriginalTimetable.V1.testClearMarkerRow0Button)
    }
}
