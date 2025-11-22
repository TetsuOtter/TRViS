# TRViS.iOS.UITests

This project contains UI tests for the TRViS MAUI application using Swift and XCTest.

## Setup

1. Install Xcode and xcodegen:

   ```bash
   brew install xcodegen
   ```

2. Generate the Xcode project:

   ```bash
   cd TRViS.iOS.UITests
   xcodegen
   ```

3. Open the project in Xcode:
   ```bash
   open TRViS.iOS.UITests.xcodeproj
   ```

## Running Tests

Use the `iosUITest.sh` script to run the UI tests:

```bash
./iosUITest.sh [UDID/DeviceName]
```

This script will:

1. Build the MAUI app
2. Boot the specified simulator
3. Run the XCTest UI tests
4. Shutdown the simulator

## Test Structure

- `TRViS_UITests.swift`: Main test class containing UI tests
  - `testAppLaunches()`: Verifies the app launches successfully
  - `testTakeScreenshot()`: Takes a screenshot of the app
  - `testLaunchPerformance()`: Measures app launch performance

## Notes

- The tests use the bundle identifier `dev.t0r.trvis` to launch the MAUI app
- Screenshots are attached to test results for debugging
- Tests are designed to work with iOS Simulator
