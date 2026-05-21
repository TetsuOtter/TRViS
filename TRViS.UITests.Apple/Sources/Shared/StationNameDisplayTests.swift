// StationNameDisplayTests.swift
// Mirrors TRViS.UITests/Tests/StationNameDisplayTests.cs.
//
// Regression for "1–4 character station names no longer fully display".
// Scans every timetable row's StationName label, strips the spacing characters
// StationNameConverter inserts between glyphs, and asserts that a fully
// rendered representative name for each length (1, 2, 3, 4 chars) is present.
//
// The C# fixture is [Platform(Exclude = "Linux")] — the Android UIAutomator2
// limitation (Linux NUnit host) does NOT apply here. Apple is fully supported.
//
// Per-test cold launch is used (BaseUITestCase.setUpWithError launches fresh).

import XCTest

final class StationNameDisplayTests: BaseUITestCase {

    // Characters StationNameConverter inserts between glyphs to spread a short
    // name across the column (EN SPACE for 2/3-char, THIN SPACE for 4-char).
    // The stripping is handled by DTACViewHostPageObject.readStrippedStationName().
    // Mirrors StationNameConverter.ConvertBack.

    // One representative name per length, all present in the default sample
    // train (1-1-1): 津(1), 大宮(2), 南浦和(3), さ新都心(4).
    private let targets: [String] = ["津", "大宮", "南浦和", "さ新都心"]

    // Default sample train 1-1-1 has ~16 rows; scan a little past that.
    private let maxRowIndexToScan = 18

    private var startHome: StartHomePageObject!

    override func setUpWithError() throws {
        try super.setUpWithError()
        startHome = StartHomePageObject(app: app, base: self)
        XCTAssertTrue(
            startHome.isDisplayed(),
            "StartHomePage should be displayed after launch (StationNameDisplayTests setUp)."
        )
        startHome.acceptPrivacyPolicyIfNeeded()
        XCTAssertTrue(
            startHome.isDisplayed(),
            "StartHomePage should be displayed after accepting privacy."
        )
    }

    // MARK: — Test: StationNames_OneToFourChars_AreFullyDisplayed

    /// Mirrors C# StationNameDisplayTests.StationNames_OneToFourChars_AreFullyDisplayed.
    ///
    /// Scans timetable rows for station name labels, strips converter spacing,
    /// and asserts each target (1–4 char) is present somewhere in the scan.
    /// Swipes up and rescans if any target is still missing (mirrors C# sweep loop).
    func testStationNames_OneToFourChars_AreFullyDisplayed() throws {
        XCTAssertTrue(startHome.isDisplayed())
        startHome.loadSample()
        _ = startHome.waitForWorkGroupList(timeout: 30)

        let dtac = startHome.autoOpenForTesting()
        dtac.switchToTimetableTab()

        // Give the timetable Grid a moment to populate accessibility elements.
        // The TimetableScrollView is already waited for in switchToTimetableTab(),
        // but the individual row labels may need additional layout time.
        Thread.sleep(forTimeInterval: 1.0)

        var rendered = Set<String>()
        let maxSweeps = 8

        for _ in 0..<maxSweeps {
            for i in 0...maxRowIndexToScan {
                if let text = dtac.readStrippedStationName(rowIndex: i), !text.isEmpty {
                    rendered.insert(text)
                }
            }

            if targets.allSatisfy({ rendered.contains($0) }) {
                break
            }

            dtac.swipeTimetableUp()
        }

        let missing = targets.filter { !rendered.contains($0) }
        XCTAssertTrue(
            missing.isEmpty,
            "Every 1–4 char station name must render in full. Missing: [\(missing.joined(separator: ", "))]. " +
            "Rendered names seen: [\(rendered.sorted(by: { $0.count < $1.count }).joined(separator: ", "))]. " +
            "A missing target with a shorter look-alike present (e.g. 'さ新' instead of 'さ新都心') " +
            "means the name wrapped and dropped glyphs."
        )
    }
}
