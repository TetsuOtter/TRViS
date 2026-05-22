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

    // MARK: — Non-deterministic region masking

    /// Paints a black rectangle over the status-bar date/time area so it does
    /// not cause pixel-diff noise from run to run.
    ///
    /// ipad-mini-a17 (2x, portrait 1488×2266 px):
    ///   The calendar date to the right of the pinned time ("9:41  Thu May 21")
    ///   changes daily and cannot be overridden by `simctl status_bar override`.
    ///   Mask: x=20, y=15, w=270, h=35 (logical pixels — covers time + date).
    ///
    /// iphone — iPhone 16 (3x, portrait 1178×2556 px / landscape 2556×1178 px):
    ///   The pinned time "9:41" sits in the Dynamic Island / status bar (top-left).
    ///   Mask: x=140, y=40, w=150, h=100 (logical pixels, portrait and landscape).
    ///
    /// Orientation note: XCUIScreen landscape screenshots carry an Exif
    /// imageOrientation tag (e.g. .left for iPhone).  UIImage.size already
    /// accounts for that tag (returns logical landscape dimensions); cgImage
    /// does not (returns raw portrait pixels).  Using UIImage.size ensures the
    /// renderer canvas matches the logical display dimensions so the image is
    /// drawn without stretching, and mask coordinates stay in the same
    /// status-bar-relative position regardless of orientation.
    static func maskNonDeterministicRegions(_ data: Data) -> Data {
        guard let image = UIImage(data: data) else { return data }
        // Use UIImage.size (logical, orientation-aware) — NOT cgImage dimensions
        // (raw pixels, orientation-unaware).  For landscape iPhone screenshots the
        // two differ: cgImage is portrait-sized, image.size is landscape-sized.
        let pw = Int(image.size.width)
        let ph = Int(image.size.height)
        guard pw > 0, ph > 0 else { return data }

        // Render at 1:1 pixel mapping so CGRect uses raw pixel coordinates.
        let format = UIGraphicsImageRendererFormat()
        format.scale = 1.0
        let size = CGSize(width: CGFloat(pw), height: CGFloat(ph))
        let renderer = UIGraphicsImageRenderer(size: size, format: format)
        let masked = renderer.image { _ in
            image.draw(in: CGRect(origin: .zero, size: size))
            UIColor.black.setFill()
            switch deviceClass {
            case "ipad-mini-a17":
                UIRectFill(CGRect(x: 20, y: 15, width: 270, height: 35))
            default: // iphone
                if pw < ph { // portrait
                    UIRectFill(CGRect(x: 140, y: 40, width: 150, height: 100))
                }
            }
        }
        return masked.pngData() ?? data
    }
}
#endif
