//
//  TRViS_UITests.swift
//  TRViS.iOS.UITests
//
//  Created by TetsuOtter on 2025/11/19.
//

import XCTest

final class TRViS_UITests: XCTestCase {

    override func setUpWithError() throws {
        // Put setup code here. This method is called before the invocation of each test method in the class.

        // In UI tests it is usually best to stop immediately when a failure occurs.
        continueAfterFailure = false

        // In UI tests it’s important to set the initial state - such as interface orientation - required for your tests before they run. The setUp method is a good place to do this.
    }

    override func tearDownWithError() throws {
        // Put teardown code here. This method is called after the invocation of each test method in the class.
    }

    func testAppLaunches() throws {
        // UI tests must launch the application that they test.
        let app = XCUIApplication(bundleIdentifier: "dev.t0r.trvis")
        app.launch()

        // Use XCTAssert and related functions to verify your tests produce the correct results.
        XCTAssertTrue(app.waitForExistence(timeout: 10))
    }

    func testTakeScreenshot() throws {
        let app = XCUIApplication(bundleIdentifier: "dev.t0r.trvis")
        app.launch()

        // Wait for the app to load
        XCTAssertTrue(app.waitForExistence(timeout: 10))

        // Take a screenshot
        let screenshot = XCUIScreen.main.screenshot()
        let attachment = XCTAttachment(screenshot: screenshot)
        attachment.name = "App Screenshot"
        attachment.lifetime = .keepAlways
        add(attachment)
    }

    func testLaunchPerformance() throws {
        if #available(macOS 10.15, iOS 13.0, tvOS 13.0, watchOS 7.0, *) {
            // This measures how long it takes to launch your application.
            measure(metrics: [XCTApplicationLaunchMetric()]) {
                XCUIApplication(bundleIdentifier: "dev.t0r.trvis").launch()
            }
        }
    }
}
