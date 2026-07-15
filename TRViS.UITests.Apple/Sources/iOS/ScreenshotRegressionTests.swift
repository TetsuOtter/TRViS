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
        start.clearLoaderForTesting()
        start.acceptPrivacyPolicyIfNeeded()
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

        // 0. StartHome — privacy policy not yet agreed (#287): reconfirm banner
        // overlays the primary buttons and the flyout is disabled. Re-accept via
        // the real Save path immediately after so the rest of the walk (and the
        // flyout it depends on) proceeds from the normal accepted state.
        start.clearPrivacyPolicyAcceptanceForTesting()
        settle()
        capture(screen: "startHome-privacyNotAgreed", theme: theme, lang: lang, failures: &failures)
        start.acceptPrivacyPolicyIfNeeded()
        // acceptPrivacyPolicyIfNeeded()'s internal 0.3 s wait is tuned for
        // dismissing the dialog, not for the banner-hide/button-reveal
        // animation this re-acceptance also triggers. Without this settle,
        // the very next capture below lands mid-animation (~0.7% pixel diff
        // on startHome-start, reproducibly, across all combos).
        settle()

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
        XCTAssertTrue(
            tpl.waitForLoadedContent(timeout: 30),
            "Third-party license list did not finish loading before screenshot capture."
        )
        settleUntilVisuallyStable()
        capture(screen: "thirdPartyLicenses", theme: theme, lang: lang, failures: &failures)
        _ = tpl.closeModal()

        // 6. StartHome — Home mode
        start.loadSample()
        _ = start.waitForWorkGroupList(timeout: 30)
        settle()
        capture(screen: "startHome-home", theme: theme, lang: lang, failures: &failures)

        // 6b. StartHome — Home mode with a server-pushed icon (SVG light+dark;
        // PNG/JPEG/GIF share the same decode-and-display path so aren't
        // separately VRT-covered). Must run after loadSample() above — see
        // injectServerIconForTesting's doc comment for why.
        start.injectServerIconForTesting()
        XCTAssertTrue(
            start.isServerIconImageVisible(timeout: 5),
            "Server icon should be visible after TestInjectServerIconButton."
        )
        settle()
        capture(screen: "startHome-serverIcon", theme: theme, lang: lang, failures: &failures)

        // 7-9. DTAC (use HT seam so the horizontal-timetable button is present)
        let dtac = start.seedHorizontalTimetableAndOpenForTesting()
        dtac.switchToTimetableTab()
        settleUntilVisuallyStable(maxWait: 15.0)
        capture(screen: "dtac-timetable", theme: theme, lang: lang, failures: &failures)

        // Hako tab — tap and wait for the tab content to render. A fixed sleep
        // here occasionally raced the render on CI (half-rendered frame: AppBar
        // and train boxes missing, e.g. dark/ja), so settle until stable instead.
        if let hakoTab = waitForElement(id: AutomationIds.DTAC.tabHako, timeout: 15) {
            hakoTab.tap()
        } else {
            XCTFail("DTAC.TabHako not found")
        }
        settleUntilVisuallyStable(maxWait: 10.0)
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
        settleUntilVisuallyStable(maxWait: 15.0)
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
            // The wrapper is visible before WKWebView commits its first
            // navigation. Waiting a fixed number of seconds was still racy on
            // loaded iPad CI runners, occasionally capturing the dark page
            // background instead of the transparent seeded PNG on WebView white.
            guard waitForElement(
                id: AutomationIds.HorizontalTimetable.webViewReady, timeout: 30
            ) != nil else {
                XCTFail("Horizontal timetable WebView did not finish navigation")
                return
            }
            settle()
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

        // 12. Hako diagram (ハコ図) — dedicated Work with 7 turn-back stations,
        // deterministically exercising the tablet-only diagram layout regardless
        // of Work1-1's turn-back station count. Settings (step 11) left us off
        // DTAC, so the seam buttons on StartHome are unreachable until we
        // navigate home first.
        _ = shell.navigateToHome()
        let hako = start.seedHakoDiagramForTesting()
        _ = hako.waitForHakoTrain(trainNumber: "D701")
        // The train controls are created before SimpleView's busy overlay fades
        // out. Wait for the framebuffer to stop changing so the activity
        // indicator is never baked into a baseline or compared mid-animation.
        settleUntilVisuallyStable(maxWait: 15.0)
        capture(screen: "dtac-hako-diagram", theme: theme, lang: lang, failures: &failures)

        // 13. Notification (通告) banner + popup — self-contained like the Hako
        // diagram step above: return home, inject a 受領必須 compact notification
        // via the UI_TEST connect-dialog deeplink seam, load the sample data and
        // enter D-TAC. ViewHost.OnAppearing() backfills the banner from the
        // NotificationCenter snapshot, so it's showing by the time D-TAC settles.
        _ = shell.navigateToHome()
        start.clearLoaderForTesting()
        start.injectNotificationForTesting(deeplink: Self.notificationDeeplink)
        start.loadSample()
        _ = start.waitForWorkGroupList(timeout: 30)
        let notifDtac = start.autoOpenForTesting()
        _ = notifDtac.switchToTimetableTab()
        settleUntilVisuallyStable(maxWait: 15.0)

        let banner = NotificationBannerPageObject(app: app, base: self)
        if banner.isShown(timeout: 10) {
            capture(screen: "notification-banner", theme: theme, lang: lang, failures: &failures)

            let popup = banner.tapToExpand()
            if popup.isDisplayed(timeout: 10) {
                settle()
                capture(screen: "notification-popup", theme: theme, lang: lang, failures: &failures)
                popup.acknowledge()
                _ = popup.waitUntilDismissed()
            } else {
                XCTFail("Notification popup did not appear after tapping the banner.")
            }
        } else {
            XCTFail("Notification banner did not appear on D-TAC after inject.")
        }

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

    // MARK: — Notification fixture

    /// A 受領必須 compact notification carrying order/sender/receiver, an icon
    /// badge, and a Priority>=1 (importance badge) — exercises every optional
    /// field row in one capture. ASCII-only (see NotificationBannerTests.cs):
    /// deeplink values pass through HttpUtility.ParseQueryString, so kept simple
    /// to avoid non-ASCII percent-encoding pitfalls. compact=true shows the small
    /// banner first; tapping it expands to the large popup. reset=true clears
    /// any notification state left by a prior combo iteration.
    private static let notificationDeeplink =
        "trvis://_test/notification?id=vrt-notice-1&title=VRT%20Notice"
        + "&body=%5Bb%5DSection%20Notice%5B%2Fb%5D%3A%20Reduce%20speed%20to%2025km%2Fh."
        + "&priority=1&ordernumber=NX-042&sender=Dispatcher&receiver=Crew"
        + "&icontext=%21&iconcolor=%23FFC107&compact=true&reset=true"
}

#endif
