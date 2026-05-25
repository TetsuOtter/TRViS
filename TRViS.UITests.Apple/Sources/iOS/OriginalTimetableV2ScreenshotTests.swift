// OriginalTimetableV2ScreenshotTests.swift
// Visual regression baseline + diff for the V2 (Card Stack) page.
// iPad mini A17 only — capturing the rich tablet layout (744pt > 600pt
// tablet breakpoint). iPhone widths render the compact layout, which has
// its own visual identity not yet baseline-covered (could be added later).
//
// Captures two baselines:
//   1) Empty state (no train selected) — empty-state label is the focus.
//   2) With selected train — sticky header + stacked CollectionView rows.
//
// Both baselines pin clock (09:41:00) and language (ja) for determinism.
// Theme is left at the simulator default (light).
//
// Env vars are injected by run-ui-tests-apple.sh's screenshot path:
//   TRVIS_SCREENSHOT_UPDATE        — "1" → overwrite baselines
//   TRVIS_SCREENSHOT_DEVICE_CLASS  — gating key ("ipad-mini-a17" required)
//   TRVIS_SCREENSHOT_BASELINE_DIR  — absolute path to Screenshots/ directory
//
// iOS only — do NOT compile under macCatalyst.

#if !targetEnvironment(macCatalyst)
import XCTest

class OriginalTimetableV2ScreenshotTests: BaseUITestCase {

    private var startHome: StartHomePageObject!
    private var shell: AppShellPageObject!

    /// Tablet breakpoint in points — must match OriginalTimetableV2Page.TabletBreakpoint.
    private let tabletBreakpointPt: CGFloat = 600

    override func setUpWithError() throws {
        try super.setUpWithError()
        continueAfterFailure = false

        // VRT focuses on the tablet variant — iPhone widths render the compact
        // layout (different visual identity, separate baseline lane).
        let width = app.windows.firstMatch.frame.size.width
        try XCTSkipIf(
            width > 0 && width < tabletBreakpointPt,
            "Skipping V2 VRT: width \(width)pt < tablet breakpoint \(tabletBreakpointPt)pt."
        )

        try XCTSkipIf(
            ScreenshotBaselineHelper.baselineRoot.isEmpty,
            "Skipping V2 VRT: TRVIS_SCREENSHOT_BASELINE_DIR not set."
        )

        startHome = StartHomePageObject(app: app, base: self)
        shell = AppShellPageObject(app: app, base: self)

        if !startHome.isDisplayed(timeout: 5) {
            _ = shell.navigateToHome()
        }
        // Order matters: privacy first, then clearLoader — the privacy banner
        // intercepts taps to the TestClearLoaderButton seam until accepted.
        // See OriginalTimetableV1ScreenshotTests for the full rationale.
        startHome.acceptPrivacyPolicyIfNeeded()
        startHome.clearLoaderForTesting()

        startHome.freezeClockForTesting()
        startHome.setLanguageJapaneseForTesting()
        startHome.forceThemeForTesting(dark: false)
        Thread.sleep(forTimeInterval: 0.8)
    }

    override func tearDownWithError() throws {
        if shell != nil {
            _ = shell.navigateToHome()
        }
        if startHome != nil {
            tryTapSeam(id: AutomationIds.StartHome.testUnfreezeClockButton)
            tryTapSeam(id: AutomationIds.StartHome.testResetThemeButton)
            tryTapSeam(id: AutomationIds.StartHome.testSetLanguageJapaneseButton)
        }
        try super.tearDownWithError()
    }

    private func tryTapSeam(id: String) {
        if let el = waitForElement(id: id, timeout: 2) {
            el.tap()
        }
    }

    // MARK: — Tests

    func testV2Tablet_Empty_Baseline() throws {
        let v2 = shell.navigateToOriginalTimetableV2()
        XCTAssertTrue(
            v2.waitForRendered(timeout: 30),
            "V2 page should render before capturing baseline."
        )
        XCTAssertTrue(
            v2.isEmptyStateVisible(timeout: 10),
            "Empty-state baseline requires the empty-state Label to be visible."
        )
        settle()
        try captureOrCompare(screen: "originalTimetable-v2-tablet-empty")
    }

    func testV2Tablet_WithSelectedTrain_Baseline() throws {
        startHome.loadSample()
        _ = startHome.waitForWorkGroupList(timeout: 30)
        let dtac = startHome.autoOpenForTesting()
        XCTAssertTrue(dtac.isDisplayed(), "DTAC should reach displayed state after AutoOpen.")

        let v2 = shell.navigateToOriginalTimetableV2()
        XCTAssertTrue(v2.waitForRendered(timeout: 30))
        XCTAssertTrue(
            v2.isHeaderVisible(timeout: 15),
            "With-train baseline requires the sticky header to be visible."
        )
        settle()
        Thread.sleep(forTimeInterval: 0.5)
        try captureOrCompare(screen: "originalTimetable-v2-tablet-with-train")
    }

    // MARK: — Capture helper

    private func captureOrCompare(screen: String) throws {
        let shot    = XCUIScreen.main.screenshot()
        let pngData = ScreenshotBaselineHelper.maskNonDeterministicRegions(shot.pngRepresentation)

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

    private func settle() {
        Thread.sleep(forTimeInterval: 0.7)
    }
}
#endif
