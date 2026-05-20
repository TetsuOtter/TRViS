// ScreenshotBaselineHelper.swift
// Manages baseline PNG files for XCUITest screenshot regression.
// Ports TRViS.UITests/Infrastructure/ScreenshotComparer.cs update-mode semantics
// and the baseline-path convention from ScreenshotRegressionTests.cs.
//
// Baseline directory layout mirrors C# project:
//   <baselineRoot>/<deviceClass>/<theme>/<lang>/<screen>.png
//
// Configuration is injected via simulator launchd env vars:
//   xcrun simctl spawn <UDID> launchctl setenv TRVIS_SCREENSHOT_BASELINE_DIR <abs path>
//   xcrun simctl spawn <UDID> launchctl setenv TRVIS_SCREENSHOT_DEVICE_CLASS <iphone|ipad-mini-a17>
//   xcrun simctl spawn <UDID> launchctl setenv TRVIS_SCREENSHOT_UPDATE       <0|1>
//
// iOS only — do NOT compile under macCatalyst.

#if !targetEnvironment(macCatalyst)
import XCTest
import UIKit

class ScreenshotBaselineHelper {

    // MARK: — Configuration (from env vars)
    //
    // xcodebuild's `KEY=VAL` arguments only set build settings; they do NOT
    // propagate as environment variables to the test-runner process. The
    // runner script (run-ui-tests-apple.sh) instead uses
    //
    //   xcrun simctl spawn <UDID> launchctl setenv TRVIS_SCREENSHOT_<KEY> <VAL>
    //
    // before invoking `xcodebuild test`. launchd inherits the variables and
    // every child process (including the XCUITest runner) sees them via
    // ProcessInfo.processInfo.environment.

    private static func env(_ key: String) -> String? {
        ProcessInfo.processInfo.environment["TRVIS_SCREENSHOT_\(key)"]
    }

    /// Absolute path to TRViS.UITests.Apple/Screenshots injected by the runner.
    static var baselineRoot: String {
        Self.env("BASELINE_DIR") ?? ""
    }

    /// Device class string: "iphone" or "ipad-mini-a17".
    static var deviceClass: String {
        Self.env("DEVICE_CLASS") ?? "iphone"
    }

    /// When true, overwrite the baseline instead of diffing against it.
    static var updateMode: Bool {
        let v = Self.env("UPDATE") ?? "0"
        return v == "1" || v.lowercased() == "true"
    }

    // MARK: — Path helpers

    /// Returns the baseline URL for a given (theme, language, screen) triple.
    static func baselineURL(theme: String, lang: String, screen: String) -> URL {
        URL(fileURLWithPath: baselineRoot)
            .appendingPathComponent(deviceClass)
            .appendingPathComponent(theme)
            .appendingPathComponent(lang)
            .appendingPathComponent("\(screen).png")
    }

    // NOTE: capture+attach+compare is implemented inline in
    // ScreenshotRegressionTests.capture(...) so that a list of all failing
    // screens can be accumulated and reported together rather than failing
    // on the first mismatch.
}
#endif
