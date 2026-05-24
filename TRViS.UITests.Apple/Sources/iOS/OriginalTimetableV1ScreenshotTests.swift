// OriginalTimetableV1ScreenshotTests.swift
// Visual regression baseline + diff for the Phase 1 V1 (Modern Classic) page.
// iPad mini A17 only — Phase 1 V1 layout is tablet-only (width >= 600pt) and
// the iPhone matrix entries render the CompactPlaceholder instead.
//
// Captures two baselines:
//   1) Empty state (no train selected) — empty-state label is the focus.
//   2) With selected train — sticky header + CollectionView rows are the focus.
//
// Both baselines pin clock (09:41:00) and language (ja) for determinism. Theme
// is left at the simulator default (light) — V1's AppThemeBinding palette has
// no per-theme determinism quirks worth a second baseline for Phase 1.
//
// Env vars are injected by run-ui-tests-apple.sh's screenshot path:
//   TRVIS_SCREENSHOT_UPDATE        — "1" → overwrite baselines
//   TRVIS_SCREENSHOT_DEVICE_CLASS  — gating key ("ipad-mini-a17" required)
//   TRVIS_SCREENSHOT_BASELINE_DIR  — absolute path to Screenshots/ directory
//
// iOS only — do NOT compile under macCatalyst.

#if !targetEnvironment(macCatalyst)
import XCTest

class OriginalTimetableV1ScreenshotTests: BaseUITestCase {

    private var startHome: StartHomePageObject!
    private var shell: AppShellPageObject!

    /// Tablet breakpoint in points — must match OriginalTimetableV1Page.TabletBreakpoint.
    private let tabletBreakpointPt: CGFloat = 600

    override func setUpWithError() throws {
        try super.setUpWithError()
        continueAfterFailure = false

        // Phase 1 is tablet-only. iPhone widths render the placeholder — skip
        // the VRT entirely there to avoid noisy baselines that don't represent
        // what users see.
        let width = app.windows.firstMatch.frame.size.width
        try XCTSkipIf(
            width > 0 && width < tabletBreakpointPt,
            "Skipping V1 VRT: width \(width)pt < tablet breakpoint \(tabletBreakpointPt)pt (Phase 1 is tablet-only)."
        )

        // Skip when the screenshot harness env var isn't injected (e.g. the
        // non-screenshot CI lane running the full suite).
        try XCTSkipIf(
            ScreenshotBaselineHelper.baselineRoot.isEmpty,
            "Skipping V1 VRT: TRVIS_SCREENSHOT_BASELINE_DIR not set."
        )

        startHome = StartHomePageObject(app: app, base: self)
        shell = AppShellPageObject(app: app, base: self)

        // If a prior test in this session left the app on DTAC / V1 / a modal,
        // get back to StartHome in Start mode.
        if !startHome.isDisplayed(timeout: 5) {
            _ = shell.navigateToHome()
        }
        startHome.clearLoaderForTesting()
        startHome.acceptPrivacyPolicyIfNeeded()

        // Determinism: pin clock + language + light theme. Theme reset happens
        // in tearDown so the simulator's default appearance doesn't drift
        // across runs.
        startHome.freezeClockForTesting()
        startHome.setLanguageJapaneseForTesting()
        startHome.forceThemeForTesting(dark: false)
        Thread.sleep(forTimeInterval: 0.8)
    }

    override func tearDownWithError() throws {
        // Best-effort: reset determinism seams so later fixtures don't inherit
        // pinned-clock / forced-theme / forced-language state. By the time we
        // hit tearDown we're on V1 (or possibly DTAC) — the seam buttons live
        // on StartHome's host Grid, so we need to navigate back first.
        //
        // Each step is wrapped so a failure here doesn't mask the real test
        // result. base.tapSeam(...) would XCTFail when the button isn't found
        // on the current page; we route through the element-existence probe
        // instead and silently skip when absent.
        if shell != nil {
            _ = shell.navigateToHome()
        }
        if let s = startHome {
            tryTapSeam(id: AutomationIds.StartHome.testUnfreezeClockButton)
            tryTapSeam(id: AutomationIds.StartHome.testResetThemeButton)
            tryTapSeam(id: AutomationIds.StartHome.testSetLanguageJapaneseButton)
            _ = s // silence unused warning
        }
        try super.tearDownWithError()
    }

    /// Taps a seam button if it's reachable within a short window. Silently
    /// returns when absent (we may not be on the host page).
    private func tryTapSeam(id: String) {
        if let el = waitForElement(id: id, timeout: 2) {
            el.tap()
        }
    }

    // MARK: — Tests

    /// Captures (or compares) the V1 empty-state screenshot on iPad mini A17.
    /// No train is committed; the empty-state Label is the only thing rendered
    /// in the row-list area.
    func testV1Tablet_Empty_Baseline() throws {
        let v1 = shell.navigateToOriginalTimetableV1()
        XCTAssertTrue(
            v1.waitForRendered(timeout: 30),
            "V1 page should render before capturing baseline."
        )
        XCTAssertTrue(
            v1.isEmptyStateVisible(timeout: 10),
            "Empty-state baseline requires the empty-state Label to be visible."
        )
        // Allow first-paint / status-bar override to settle before capture.
        settle()

        try captureOrCompare(screen: "originalTimetable-v1-tablet-empty")
    }

    /// Captures (or compares) the V1 with-train screenshot on iPad mini A17.
    /// Sample data is loaded, the first Work is auto-opened (cascade picks
    /// SelectedTrainData), and V1 is reached via the flyout — the sticky
    /// header + CollectionView rows are the visual focus.
    func testV1Tablet_WithSelectedTrain_Baseline() throws {
        startHome.loadSample()
        _ = startHome.waitForWorkGroupList(timeout: 30)
        let dtac = startHome.autoOpenForTesting()
        XCTAssertTrue(dtac.isDisplayed(), "DTAC should reach displayed state after AutoOpen.")

        let v1 = shell.navigateToOriginalTimetableV1()
        XCTAssertTrue(v1.waitForRendered(timeout: 30))
        XCTAssertTrue(
            v1.isHeaderVisible(timeout: 15),
            "With-train baseline requires the sticky header to be visible."
        )
        // CollectionView first-paint + layout settle window.
        settle()
        Thread.sleep(forTimeInterval: 0.5)

        try captureOrCompare(screen: "originalTimetable-v1-tablet-with-train")
    }

    // MARK: — Capture helper

    /// Single-screen capture: in update mode overwrites the baseline; in compare
    /// mode diffs and fails the test if the diff exceeds the threshold. Mirrors
    /// the shape of ScreenshotRegressionTests.capture(...), with two differences:
    ///   • Theme/lang segments fixed to "light/ja" (no combo walk for V1 Phase 1).
    ///   • Failures fail the test directly (no accumulated failure list).
    private func captureOrCompare(screen: String) throws {
        let shot    = XCUIScreen.main.screenshot()
        let pngData = ScreenshotBaselineHelper.maskNonDeterministicRegions(shot.pngRepresentation)

        // Always attach so CI artifact viewers can inspect on failure.
        let attachment = XCTAttachment(data: pngData, uniformTypeIdentifier: "public.png")
        attachment.name = "\(screen)-light-ja"
        attachment.lifetime = .keepAlways
        XCTContext.runActivity(named: "Screenshot: \(screen)") { activity in
            activity.add(attachment)
        }

        let url = ScreenshotBaselineHelper.baselineURL(
            theme: "light", lang: "ja", screen: screen
        )

        if ScreenshotBaselineHelper.updateMode {
            let dir = url.deletingLastPathComponent()
            try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
            try pngData.write(to: url)
            print("[ScreenshotBaseline] UPDATED \(url.path)")
            return
        }

        guard FileManager.default.fileExists(atPath: url.path) else {
            XCTFail("[ScreenshotBaseline] Baseline missing: \(url.path)")
            return
        }

        let result = ScreenshotComparer.compare(
            actual: pngData, baselinePath: url, name: screen
        )
        print(result.message)
        if let diffData = result.diffData {
            let diffAttachment = XCTAttachment(data: diffData, uniformTypeIdentifier: "public.png")
            diffAttachment.name = "\(screen)-light-ja-diff"
            diffAttachment.lifetime = .keepAlways
            XCTContext.runActivity(named: "Diff: \(screen)") { activity in
                activity.add(diffAttachment)
            }
        }
        XCTAssertTrue(result.passed, result.message)
    }

    // MARK: — Settle helper

    /// Fixed settle window (~700 ms) — matches the helper in ScreenshotRegressionTests.
    private func settle() {
        Thread.sleep(forTimeInterval: 0.7)
    }
}
#endif
