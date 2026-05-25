// ScreenshotRegressionTests.swift
// Screenshot-regression gate + Apple-review capture pass.
// Ports TRViS.UITests/Tests/ScreenshotRegressionTests.cs — iOS only.
//
// Walks every reachable screen in (light, dark) × (ja, en) and diffs each
// frame against a committed baseline in
//   TRViS.UITests.Apple/Screenshots/<deviceClass>/<theme>/<lang>/<screen>.png
//
// Determinism levers:
//  * TestFreezeClockButton  — pins AppTimeProvider to 09:41:00
//  * TestForceLightThemeButton / TestForceDarkThemeButton — force palette
//  * TestSetLanguageEnglishButton / JapaneseButton — pin UI language
//  * xcrun simctl status_bar override — pins iOS status bar (done in runner script)
//
// Env vars (set by run-ui-tests-apple.sh via `xcrun simctl spawn launchctl setenv`):
//  TRVIS_SCREENSHOT_UPDATE        = "1" → update mode (overwrite baselines)
//  TRVIS_SCREENSHOT_DEVICE_CLASS  = "iphone" | "ipad-mini-a17"
//  TRVIS_SCREENSHOT_BASELINE_DIR  = absolute path to Screenshots/ directory
//
// Regenerate baselines: ./update-screenshots-apple.sh
//
// iOS only — do NOT compile under macCatalyst.

#if !targetEnvironment(macCatalyst)
import XCTest

class ScreenshotRegressionTests: BaseUITestCase {

    private var start: StartHomePageObject!
    private var shell: AppShellPageObject!

    // MARK: — Setup

    override func setUpWithError() throws {
        try super.setUpWithError()
        continueAfterFailure = false

        start = StartHomePageObject(app: app, base: self)
        shell = AppShellPageObject(app: app, base: self)

        // A prior test in this session may have left the app on DTAC / Settings /
        // a modal. Get back to StartHome in Start mode before each test.
        if !start.isDisplayed(timeout: 5) {
            _ = shell.navigateToHome()
        }
        // Order matters: privacy first, then clearLoader. The privacy banner
        // intercepts taps to all StartHome controls (including the seam
        // TestClearLoaderButton) until accepted. On the first XCUITest run
        // of a fresh install (the runner script `simctl uninstall`s) the
        // clearLoaderForTesting() seam tap would silently no-op against the
        // unreachable button. acceptPrivacyPolicyIfNeeded() fast-paths when
        // already accepted, so the swap is cheap on the 2..N tests/iterations.
        // NOTE: the mid-test combo-loop reset below (≈line 98) intentionally
        // only calls clearLoaderForTesting() — by that point privacy is
        // already accepted in this process, so the loader reset is enough.
        start.acceptPrivacyPolicyIfNeeded()
        start.clearLoaderForTesting()
    }

    // MARK: — Main test

    /// Single test method: iterates over all (theme, lang) combos without
    /// restarting the app between iterations, keeping the total session within
    /// the 90 s per-test XCTest budget and avoiding cold-launch overhead × 4.
    func testCaptureAndDiffAllScreens() throws {
        // Skip gracefully when the baseline directory env var is not set (e.g.
        // when the existing ui-test-apple-xcuitest CI job runs the full suite
        // without screenshot flags, or when run locally without --device-class).
        try XCTSkipIf(
            ScreenshotBaselineHelper.baselineRoot.isEmpty,
            "Skipping ScreenshotRegressionTests: TRVIS_SCREENSHOT_BASELINE_DIR not set"
        )
        let combos: [(theme: String, lang: String)] = [
            ("light", "ja"),
            ("light", "en"),
            ("dark",  "ja"),
            ("dark",  "en"),
        ]

        var allFailures: [String] = []

        // Pre-warm: navigate to the Settings page once in light theme before
        // the combo loop so the circular nav icon is rendered and cached before
        // the first productive capture (light/ja). The icon's anti-aliasing is
        // non-deterministic on the very first Core Animation layer creation in a
        // session; all subsequent renders use the cached layer and are stable.
        // Without this, light/ja is always the first settings render and drifts ±1
        // per-channel from the baseline on every run.
        start.forceThemeForTesting(dark: false)
        Thread.sleep(forTimeInterval: 0.5)
        _ = shell.navigateToSettings()
        settle()
        _ = shell.navigateToHome()
        start.clearLoaderForTesting()
        start.resetThemeForTesting()
        Thread.sleep(forTimeInterval: 0.3)

        for combo in combos {
            let (theme, lang) = (combo.theme, combo.lang)
            let dark = (theme == "dark")

            // Between combo iterations, navigate back to StartHome / Start mode.
            // The first iteration is already set up by setUpWithError().
            if combo != combos.first! {
                if !start.isDisplayed(timeout: 5) {
                    _ = shell.navigateToHome()
                }
                start.clearLoaderForTesting()
            }

            var failures: [String] = []
            captureCombo(theme: theme, lang: lang, dark: dark, failures: &failures)
            allFailures.append(contentsOf: failures)
        }

        // Gate: only gated device classes fail the build on pixel diff.
        let gatedDeviceClasses = ["iphone", "ipad-mini-a17"]
        let deviceClass = ScreenshotBaselineHelper.deviceClass

        if ScreenshotBaselineHelper.updateMode {
            print("[ScreenshotRegression] All combos updated for \(deviceClass).")
            return
        }

        if !gatedDeviceClasses.contains(deviceClass) {
            XCTContext.runActivity(named: "Ungated device class '\(deviceClass)'") { _ in
                print(
                    "deviceClass '\(deviceClass)' is captured for review but excluded " +
                    "from the pixel-diff gate. \(allFailures.count) screen(s) differed (informational)."
                )
            }
            return
        }

        XCTAssertTrue(
            allFailures.isEmpty,
            "[\(deviceClass)] \(allFailures.count) screen(s) differ from baseline:\n  " +
            allFailures.joined(separator: "\n  ")
        )
    }

    // MARK: — Per-combo walk

    private func captureCombo(theme: String, lang: String, dark: Bool, failures: inout [String]) {

        // -- Pin clock + language + theme on StartHome --
        start.freezeClockForTesting()
        if lang == "en" {
            start.setLanguageEnglishForTesting()
        } else {
            start.setLanguageJapaneseForTesting()
        }
        start.forceThemeForTesting(dark: dark)
        // Language switch rebinds every {loc:Translate} caption; theme flip
        // repaints the whole visual tree. Give both a generous beat before
        // the first capture.
        Thread.sleep(forTimeInterval: 1.2)

        // 1. StartHome — Start mode
        capture(screen: "startHome-start", theme: theme, lang: lang, failures: &failures)

        // 2. Privacy-policy dialog (footer link → read-only; already accepted at launch)
        start.openPrivacyPolicyDialog()
        settleUntilVisuallyStable()
        capture(screen: "privacyPolicy", theme: theme, lang: lang, failures: &failures)
        start.closePrivacyPolicyDialog()

        // 3. Connect-to-server dialog (visually stable wait for async history)
        let connect = start.openConnectServerDialog()
        settleUntilVisuallyStable()
        capture(screen: "connectServer", theme: theme, lang: lang, failures: &failures)
        _ = connect.close()

        // 4. Select-file dialog
        let selectFile = start.openSelectFileDialog()
        settle()
        capture(screen: "selectFile", theme: theme, lang: lang, failures: &failures)
        _ = selectFile.close()

        // 5. Third-party licenses modal
        let tpl = start.openThirdPartyLicenses()
        settle()
        capture(screen: "thirdPartyLicenses", theme: theme, lang: lang, failures: &failures)
        _ = tpl.closeModal()

        // 6. StartHome — Home mode
        start.loadSample()
        _ = start.waitForWorkGroupList(timeout: 30)
        settle()
        capture(screen: "startHome-home", theme: theme, lang: lang, failures: &failures)

        // 7-9. DTAC (use HT seam so the horizontal-timetable button is present)
        let dtac = start.seedHorizontalTimetableAndOpenForTesting()
        dtac.switchToTimetableTab()
        settle()
        capture(screen: "dtac-timetable", theme: theme, lang: lang, failures: &failures)

        // Hako tab — tap and wait 500 ms for the tab content to render
        if let hakoTab = waitForElement(id: AutomationIds.DTAC.tabHako, timeout: 15) {
            hakoTab.tap()
        } else {
            XCTFail("DTAC.TabHako not found")
        }
        Thread.sleep(forTimeInterval: 0.5)
        capture(screen: "dtac-hako", theme: theme, lang: lang, failures: &failures)

        // BBCode / Hiragino font rendering check: 試単9091 selection steps.
        //
        // 試単9091 has BBCode remarks and a TrainInfo/BeforeDeparture payload that
        // exercises the HtmlAutoDetectLabel rendering pipeline. These captures gate
        // that the Remarks text and timetable 記事 column render correctly.
        //
        // Note: Remarks is closed before switching to 時刻表 (step 3 below) so that
        // dtac-timetable-shiken9091 shows a clean timetable view. The Remarks panel
        // is then re-opened explicitly for dtac-timetable-shiken9091-remarks (step 4).

        // Step 1: Select 試単9091 in the ハコ tab
        dtac.selectHakoTrain(trainNumber: "試単9091")
        Thread.sleep(forTimeInterval: 0.5)
        capture(screen: "dtac-hako-shiken9091", theme: theme, lang: lang, failures: &failures)

        // Step 2: Open the Remarks panel
        dtac.tapRemarksOpenClose()
        Thread.sleep(forTimeInterval: 0.5)
        capture(screen: "dtac-hako-shiken9091-remarks", theme: theme, lang: lang, failures: &failures)

        // Close Remarks before switching to 時刻表 so step 3 is unobscured
        dtac.tapRemarksOpenClose()
        Thread.sleep(forTimeInterval: 0.3)

        // Step 3: Switch to 時刻表 tab (試単9091 data, Remarks closed)
        dtac.switchToTimetableTab()
        settle()
        capture(screen: "dtac-timetable-shiken9091", theme: theme, lang: lang, failures: &failures)

        // Step 4: Open the Remarks panel in 時刻表 tab
        dtac.tapRemarksOpenClose()
        Thread.sleep(forTimeInterval: 0.5)
        capture(screen: "dtac-timetable-shiken9091-remarks", theme: theme, lang: lang, failures: &failures)

        // Step 5: Open the TrainInfo/BeforeDeparture area (PageHeader toggle)
        dtac.tapOpenClose()
        Thread.sleep(forTimeInterval: 0.5)
        capture(screen: "dtac-timetable-shiken9091-open", theme: theme, lang: lang, failures: &failures)

        // Reset state: close TrainInfo and Remarks before continuing
        dtac.tapOpenClose()
        dtac.tapRemarksOpenClose()
        Thread.sleep(forTimeInterval: 0.3)

        // 10. Horizontal timetable (conditional on button visibility)
        dtac.switchToTimetableTab()
        if dtac.isHorizontalTimetableButtonVisible(timeout: 5) {
            dtac.tapHorizontalTimetableButton()
            let ht = HorizontalTimetablePageObject(app: app, base: self)
            _ = waitForElement(id: AutomationIds.HorizontalTimetable.webView, timeout: 30)
            // WebView first-paint is slower than a native page swap; the dark-theme
            // first render in a session can take >1.5 s on loaded CI runners — use
            // a generous fixed wait to prevent blank-screen captures.
            Thread.sleep(forTimeInterval: 3.5)
            capture(screen: "horizontalTimetable", theme: theme, lang: lang, failures: &failures)
            // Pop back to DTAC — HT is a Shell-pushed page, flyout unreachable from here
            _ = ht.tapBack()
            // Wait for DTAC to be fully visible after WebView teardown before
            // searching for the Hako tab — ensures the orientation-reset tap
            // fires after the portrait transition is complete.
            _ = waitForElement(id: AutomationIds.DTAC.menuButton, timeout: 20)
            settle()
        } else {
            print("[ScreenshotRegression] horizontalTimetable: 横型時刻表 button not visible — screen skipped.")
        }

        // Reset the iOS interface-orientation mask before leaving DTAC.
        // ViewHost.UpdateOrientation() locks the process-wide mask to Landscape
        // while the timetable tab is shown and never resets it on navigation away.
        // Re-tapping the Hako tab drives the same code path that flips the mask back
        // to Portrait. ~900ms for the iOS RequestGeometryUpdate round-trip.
        if let hakoTab = waitForElement(id: AutomationIds.DTAC.tabHako, timeout: 15) {
            hakoTab.tap()
        }
        Thread.sleep(forTimeInterval: 2.0)

        // 11. Settings page (reached from DTAC via flyout)
        _ = shell.navigateToSettings()
        settle()
        capture(screen: "settings", theme: theme, lang: lang, failures: &failures)

        // Leave the app in a recoverable state for the next combo
        _ = shell.navigateToHome()

        // Home-mode leak guard: LoadSample() above puts StartHome into Home mode;
        // clear it so later combos start in Start mode with ConnectServerButton visible.
        start.clearLoaderForTesting()

        // Determinism reset: restore Japanese, live clock, and OS theme
        // so later fixtures/combos don't inherit this combo's seam state.
        start.setLanguageJapaneseForTesting()
        start.unfreezeClockForTesting()
        start.resetThemeForTesting()
    }

    // MARK: — Capture helper

    /// Takes a screenshot and either updates the baseline or diffs against it.
    /// Failures are accumulated into `failures` rather than failing immediately,
    /// so the full walk completes and produces a complete diff report.
    private func capture(screen: String, theme: String, lang: String, failures: inout [String]) {
        let shot    = XCUIScreen.main.screenshot()
        let pngData = ScreenshotBaselineHelper.maskNonDeterministicRegions(
            shot.pngRepresentation
        )

        // Always attach for CI artifact inspection / Apple review deliverable
        let attachment = XCTAttachment(data: pngData, uniformTypeIdentifier: "public.png")
        attachment.name = "\(screen)-\(theme)-\(lang)"
        attachment.lifetime = .keepAlways
        XCTContext.runActivity(named: "Screenshot: \(screen) [\(theme)/\(lang)]") { activity in
            activity.add(attachment)
        }

        let url = ScreenshotBaselineHelper.baselineURL(theme: theme, lang: lang, screen: screen)

        if ScreenshotBaselineHelper.updateMode {
            let dir = url.deletingLastPathComponent()
            try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
            do {
                try pngData.write(to: url)
                print("[ScreenshotBaseline] UPDATED \(url.path)")
            } catch {
                let msg = "[ScreenshotBaseline] Failed to write \(url.path): \(error)"
                print(msg)
                failures.append(msg)
            }
            return
        }

        guard FileManager.default.fileExists(atPath: url.path) else {
            let msg = "[ScreenshotBaseline] Baseline missing: \(url.path)"
            print(msg)
            failures.append(msg)
            return
        }

        let result = ScreenshotComparer.compare(actual: pngData, baselinePath: url, name: screen)
        print(result.message)
        if let diffData = result.diffData {
            let diffAttachment = XCTAttachment(data: diffData, uniformTypeIdentifier: "public.png")
            diffAttachment.name = "\(screen)-\(theme)-\(lang)-diff"
            diffAttachment.lifetime = .keepAlways
            XCTContext.runActivity(named: "Diff: \(screen) [\(theme)/\(lang)]") { activity in
                activity.add(diffAttachment)
            }
        }
        if !result.passed {
            failures.append(result.message)
        }
    }

    // MARK: — Settle helpers

    /// Fixed settle for native page/modal swaps (~700 ms, matches C# Settle()).
    private func settle() {
        Thread.sleep(forTimeInterval: 0.7)
    }

    /// Blocks until the framebuffer stops changing or a hard cap elapses.
    /// Used for the connect-server modal whose open animation + async history
    /// population can outlast the fixed settle window (matches C# SettleUntilVisuallyStable).
    private func settleUntilVisuallyStable(
        maxWait: TimeInterval = 6.0,
        probeInterval: TimeInterval = 0.25,
        requiredStableComparisons: Int = 2
    ) {
        settle() // Initial settle before probing
        var prev: Data? = nil
        var stable = 0
        let deadline = Date().addingTimeInterval(maxWait)

        while Date() < deadline {
            let cur = XCUIScreen.main.screenshot().pngRepresentation
            if let p = prev, p == cur {
                stable += 1
                if stable >= requiredStableComparisons {
                    return
                }
            } else {
                stable = 0
            }
            prev = cur
            Thread.sleep(forTimeInterval: probeInterval)
        }
    }
}

#endif
