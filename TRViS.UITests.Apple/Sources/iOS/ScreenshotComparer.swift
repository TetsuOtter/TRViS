// ScreenshotComparer.swift
// Pixel-level screenshot comparison for XCUITest screenshot regression.
// Ports TRViS.UITests/Infrastructure/ScreenshotComparer.cs — iOS only.
//
// Tolerance semantics (MUST match C# source):
//   channelTolerance  = 16  — per-channel delta threshold (max of R,G,B,A deltas)
//   maxDiffFraction   = 0.005 — at most 0.5 % of pixels may differ
//
// The method always writes <name>.actual.png and <name>.diff.png alongside
// the baseline so every frame can be inspected in CI artifacts.
//
// iOS only — do NOT compile under macCatalyst.

#if !targetEnvironment(macCatalyst)
import UIKit
import XCTest

struct ScreenshotCompareResult {
    let passed: Bool
    let diffFraction: Double
    let message: String
    let diffData: Data?
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
                message: "[\(name)] Could not decode actual screenshot PNG",
                diffData: nil
            )
        }

        guard let baselineData = try? Data(contentsOf: baselinePath),
              let baselineImage = UIImage(data: baselineData),
              let baselineCG    = baselineImage.cgImage else {
            return ScreenshotCompareResult(
                passed: false, diffFraction: 1.0,
                message: "[\(name)] Could not load baseline at \(baselinePath.path)",
                diffData: nil
            )
        }

        let w = actualCG.width
        let h = actualCG.height

        guard baselineCG.width == w, baselineCG.height == h else {
            return ScreenshotCompareResult(
                passed: false, diffFraction: 1.0,
                message: "[\(name)] Size mismatch: actual \(w)×\(h) vs baseline \(baselineCG.width)×\(baselineCG.height)",
                diffData: nil
            )
        }

        // --- Render both into RGBA8888 buffers ---
        guard let actualPixels   = rgbaPixels(cgImage: actualCG,   width: w, height: h),
              let baselinePixels = rgbaPixels(cgImage: baselineCG, width: w, height: h) else {
            return ScreenshotCompareResult(
                passed: false, diffFraction: 1.0,
                message: "[\(name)] Could not extract pixel data",
                diffData: nil
            )
        }

        // --- Pixel diff: count-only pass (no allocation on the pass path) ---
        let total     = w * h
        var diffCount = 0
        for i in 0..<total {
            let base4 = i * 4
            let delta = max(
                abs(Int(actualPixels[base4 + 0]) - Int(baselinePixels[base4 + 0])),
                abs(Int(actualPixels[base4 + 1]) - Int(baselinePixels[base4 + 1])),
                abs(Int(actualPixels[base4 + 2]) - Int(baselinePixels[base4 + 2])),
                abs(Int(actualPixels[base4 + 3]) - Int(baselinePixels[base4 + 3]))
            )
            if delta > channelTolerance { diffCount += 1 }
        }

        let diffFraction = Double(diffCount) / Double(total)
        let passed = diffFraction <= maxDiffFraction

        // Build diff image for all comparisons (red = changed, 33%-dimmed = unchanged)
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
            let delta = max(abs(aR - bR), abs(aG - bG), abs(aB - bB), abs(aA - bA))
            if delta > channelTolerance {
                diffPixels[base4 + 0] = 255
                diffPixels[base4 + 1] = 0
                diffPixels[base4 + 2] = 0
                diffPixels[base4 + 3] = 255
            } else {
                // Dimmed baseline pixel (33% brightness, matching C# port)
                diffPixels[base4 + 0] = UInt8(bR / 3)
                diffPixels[base4 + 1] = UInt8(bG / 3)
                diffPixels[base4 + 2] = UInt8(bB / 3)
                diffPixels[base4 + 3] = 255
            }
        }

        // Write actual + diff artifacts alongside baseline so CI can upload them
        let dir = baselinePath.deletingLastPathComponent()
        let stem = baselinePath.deletingPathExtension().lastPathComponent

        let actualPath = dir.appendingPathComponent("\(stem).actual.png")
        let diffPath   = dir.appendingPathComponent("\(stem).diff.png")

        try? actual.write(to: actualPath)
        let diffImageData = makePNG(pixels: diffPixels, width: w, height: h)
        if let data = diffImageData {
            try? data.write(to: diffPath)
        }

        let pct = String(format: "%.4f%%", diffFraction * 100)
        let msg = passed
            ? "[\(name)] PASS — diff \(pct) (\(diffCount)/\(total) px)"
            : "[\(name)] FAIL — diff \(pct) (\(diffCount)/\(total) px) exceeds \(maxDiffFraction * 100)%"

        return ScreenshotCompareResult(passed: passed, diffFraction: diffFraction, message: msg, diffData: diffImageData)
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
