// OriginalTimetableV6ScreenshotTests.swift
// Visual regression baseline + diff for the V6 (Bold Editorial) page.
// iPad mini A17 only — captures the rich tablet masthead + CurrentBlock layout.

#if !targetEnvironment(macCatalyst)
import XCTest

class OriginalTimetableV6ScreenshotTests: BaseUITestCase {

    private var startHome: StartHomePageObject!
    private var shell: AppShellPageObject!

    private let tabletBreakpointPt: CGFloat = 600

    override func setUpWithError() throws {
        try super.setUpWithError()
        continueAfterFailure = false

        let width = app.windows.firstMatch.frame.size.width
        try XCTSkipIf(
            width > 0 && width < tabletBreakpointPt,
            "Skipping V6 VRT: width \(width)pt < tablet breakpoint \(tabletBreakpointPt)pt."
        )

        try XCTSkipIf(
            ScreenshotBaselineHelper.baselineRoot.isEmpty,
            "Skipping V6 VRT: TRVIS_SCREENSHOT_BASELINE_DIR not set."
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

    func testV6Tablet_Empty_Baseline() throws {
        let v6 = shell.navigateToOriginalTimetableV6()
        XCTAssertTrue(
            v6.waitForRendered(timeout: 30),
            "V6 page should render before capturing baseline."
        )
        XCTAssertTrue(
            v6.isEmptyStateVisible(timeout: 10),
            "Empty-state baseline requires the EmptyState Label to be visible."
        )
        settle()
        try captureOrCompare(screen: "originalTimetable-v6-tablet-empty")
    }

    func testV6Tablet_WithSelectedTrain_Baseline() throws {
        startHome.loadSample()
        _ = startHome.waitForWorkGroupList(timeout: 30)
        let dtac = startHome.autoOpenForTesting()
        XCTAssertTrue(dtac.isDisplayed(), "DTAC should reach displayed state after AutoOpen.")

        let v6 = shell.navigateToOriginalTimetableV6()
        XCTAssertTrue(v6.waitForRendered(timeout: 30))
        XCTAssertTrue(
            v6.isCurrentBlockVisible(timeout: 15),
            "With-train baseline requires the CurrentBlock to be visible."
        )
        settle()
        Thread.sleep(forTimeInterval: 0.5)
        try captureOrCompare(screen: "originalTimetable-v6-tablet-with-train")
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
