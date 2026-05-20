// ScreenshotComparer.swift
// Pixel-level screenshot comparison for XCUITest screenshot regression.
// Ports TRViS.UITests/Infrastructure/ScreenshotComparer.cs — iOS only.
//
// Tolerance semantics (MUST match C# source):
//   channelTolerance  = 16  — per-channel delta threshold (max of R,G,B,A deltas)
//   maxDiffFraction   = 0.005 — at most 0.5 % of pixels may differ
//
// On failure the method writes <name>.actual.png and <name>.diff.png alongside
// the baseline so failures can be inspected in CI artifacts.
//
// iOS only — do NOT compile under macCatalyst.

#if !targetEnvironment(macCatalyst)
import UIKit
import XCTest

struct ScreenshotCompareResult {
    let passed: Bool
    let diffFraction: Double
    let message: String
}

class ScreenshotComparer {

    // Tolerance constants — must stay in sync with C# ScreenshotComparer.cs
    static let channelTolerance: Int = 16
    static let maxDiffFraction: Double = 0.005

    /// Compares `actual` against the image at `baselinePath`.
    ///
    /// - Parameters:
    ///   - actual:       Screenshot PNG data from XCUIScreen.main.screenshot().pngRepresentation
    ///   - baselinePath: Absolute path to the baseline PNG file.
    ///   - name:         Logical screenshot name used for error artifact filenames.
    /// - Returns: A ScreenshotCompareResult describing the outcome.
    static func compare(
        actual: Data,
        baselinePath: URL,
        name: String
    ) -> ScreenshotCompareResult {

        // --- Load images ---
        guard let actualImage = UIImage(data: actual),
              let actualCG    = actualImage.cgImage else {
            return ScreenshotCompareResult(
                passed: false, diffFraction: 1.0,
                message: "[\(name)] Could not decode actual screenshot PNG"
            )
        }

        guard let baselineData = try? Data(contentsOf: baselinePath),
              let baselineImage = UIImage(data: baselineData),
              let baselineCG    = baselineImage.cgImage else {
            return ScreenshotCompareResult(
                passed: false, diffFraction: 1.0,
                message: "[\(name)] Could not load baseline at \(baselinePath.path)"
            )
        }

        let w = actualCG.width
        let h = actualCG.height

        guard baselineCG.width == w, baselineCG.height == h else {
            return ScreenshotCompareResult(
                passed: false, diffFraction: 1.0,
                message: "[\(name)] Size mismatch: actual \(w)×\(h) vs baseline \(baselineCG.width)×\(baselineCG.height)"
            )
        }

        // --- Render both into RGBA8888 buffers ---
        guard let actualPixels   = rgbaPixels(cgImage: actualCG,   width: w, height: h),
              let baselinePixels = rgbaPixels(cgImage: baselineCG, width: w, height: h) else {
            return ScreenshotCompareResult(
                passed: false, diffFraction: 1.0,
                message: "[\(name)] Could not extract pixel data"
            )
        }

        // --- Pixel diff ---
        let total     = w * h
        var diffCount = 0
        // Build diff image buffer (red = changed pixel, dimmed = unchanged)
        var diffPixels = [UInt8](repeating: 255, count: total * 4)

        for i in 0..<total {
            let base4 = i * 4
            let aR = Int(actualPixels[base4 + 0])
            let aG = Int(actualPixels[base4 + 1])
            let aB = Int(actualPixels[base4 + 2])
            let aA = Int(actualPixels[base4 + 3])
            let bR = Int(baselinePixels[base4 + 0])
            let bG = Int(baselinePixels[base4 + 1])
            let bB = Int(baselinePixels[base4 + 2])
            let bA = Int(baselinePixels[base4 + 3])

            let delta = max(
                abs(aR - bR), abs(aG - bG),
                abs(aB - bB), abs(aA - bA)
            )

            if delta > channelTolerance {
                diffCount += 1
                // Bright red pixel in diff image
                diffPixels[base4 + 0] = 255
                diffPixels[base4 + 1] = 0
                diffPixels[base4 + 2] = 0
                diffPixels[base4 + 3] = 255
            } else {
                // Dimmed baseline pixel (50% brightness)
                diffPixels[base4 + 0] = UInt8(bR / 2)
                diffPixels[base4 + 1] = UInt8(bG / 2)
                diffPixels[base4 + 2] = UInt8(bB / 2)
                diffPixels[base4 + 3] = 255
            }
        }

        let diffFraction = Double(diffCount) / Double(total)
        let passed = diffFraction <= maxDiffFraction

        if !passed {
            // Write actual + diff artifacts alongside baseline so CI can upload them
            let dir = baselinePath.deletingLastPathComponent()
            let stem = baselinePath.deletingPathExtension().lastPathComponent

            let actualPath = dir.appendingPathComponent("\(stem).actual.png")
            let diffPath   = dir.appendingPathComponent("\(stem).diff.png")

            try? actual.write(to: actualPath)
            if let diffImage = makePNG(pixels: diffPixels, width: w, height: h) {
                try? diffImage.write(to: diffPath)
            }
        }

        let pct = String(format: "%.4f%%", diffFraction * 100)
        let msg = passed
            ? "[\(name)] PASS — diff \(pct) (\(diffCount)/\(total) px)"
            : "[\(name)] FAIL — diff \(pct) (\(diffCount)/\(total) px) exceeds \(maxDiffFraction * 100)%"

        return ScreenshotCompareResult(passed: passed, diffFraction: diffFraction, message: msg)
    }

    // MARK: — Private helpers

    /// Returns a flat RGBA8888 byte array for `cgImage` rendered at `width`×`height`.
    private static func rgbaPixels(cgImage: CGImage, width: Int, height: Int) -> [UInt8]? {
        let bytesPerRow   = width * 4
        var buffer = [UInt8](repeating: 0, count: height * bytesPerRow)

        guard let ctx = CGContext(
            data: &buffer,
            width: width,
            height: height,
            bitsPerComponent: 8,
            bytesPerRow: bytesPerRow,
            space: CGColorSpaceCreateDeviceRGB(),
            bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
        ) else { return nil }

        ctx.draw(cgImage, in: CGRect(x: 0, y: 0, width: width, height: height))
        return buffer
    }

    /// Encodes a flat RGBA8888 byte array into PNG `Data`.
    private static func makePNG(pixels: [UInt8], width: Int, height: Int) -> Data? {
        let bytesPerRow = width * 4
        var buf = pixels
        guard let ctx = CGContext(
            data: &buf,
            width: width,
            height: height,
            bitsPerComponent: 8,
            bytesPerRow: bytesPerRow,
            space: CGColorSpaceCreateDeviceRGB(),
            bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
        ),
        let cgImage = ctx.makeImage() else { return nil }

        let uiImage = UIImage(cgImage: cgImage)
        return uiImage.pngData()
    }
}
#endif
