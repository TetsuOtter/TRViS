// SelectFileDialogTests.swift
// XCUITest port of TRViS.UITests/Tests/SelectFileDialogTests.cs (10 tests).
//
// C# uses ShareSessionAcrossTestsInFixture=true with shared-session recovery
// (close stray dialog, navigate home, clear loader, clear sample files).
// This port uses per-test cold launch — all recovery blocks are dropped.
//
// SetUp calls clearSampleFilesForTesting() before every test to wipe
// TimetableFileDirectory + clear the FilePicker override static, matching the
// C# SetUp rationale for iOS noReset:true container warmth.
//
// Tests skipped:
//   None from this wave — all 10 C# tests run on iOS (Platform(Exclude="Win") only).

import XCTest

final class SelectFileDialogTests: BaseUITestCase {

    private var startHome: StartHomePageObject!

    override func setUpWithError() throws {
        try super.setUpWithError()
        startHome = StartHomePageObject(app: app, base: self)
        XCTAssertTrue(
            startHome.isDisplayed(),
            "StartHomePage should be displayed after launch (SelectFileDialogTests setUp)."
        )
        startHome.acceptPrivacyPolicyIfNeeded()
        XCTAssertTrue(
            startHome.isDisplayed(),
            "StartHomePage should be displayed after accepting privacy (SelectFileDialogTests setUp)."
        )
        // Wipe TimetableFileDirectory and clear any FilePicker override so each
        // test starts from a known-clean state.
        startHome.clearSampleFilesForTesting()
    }

    // MARK: — Test: OpenDialog_OnCleanInstall_ShowsEmptyStateWithBrowseButton

    /// Mirrors C# OpenDialog_OnCleanInstall_ShowsEmptyStateWithBrowseButton.
    ///
    /// With TimetableFileDirectory wiped in setUp, the dialog shows empty state.
    /// On iOS the OpenStorageLocationButton is also expected to be visible.
    func testOpenDialog_OnCleanInstall_ShowsEmptyStateWithBrowseButton() throws {
        XCTAssertTrue(startHome.isDisplayed())

        let dialog = startHome.openSelectFileDialog()

        XCTAssertTrue(
            dialog.isDisplayed(),
            "Dialog should be displayed."
        )
        XCTAssertTrue(
            dialog.isEmptyStateVisible(),
            "With no files, the dialog should default to the empty state."
        )
        XCTAssertTrue(
            dialog.browseButton.exists,
            "The browse button should remain visible in the empty state."
        )
        // iOS: openStorageLocationButton is visible (Android-only exclusion does not apply here).
        XCTAssertTrue(
            dialog.openStorageLocationButton.exists,
            "The 'open storage location' button should be reachable so the user can drop files in."
        )
    }

    // MARK: — Test: Close_ReturnsToStartHomePage

    /// Mirrors C# Close_ReturnsToStartHomePage.
    func testClose_ReturnsToStartHomePage() throws {
        let dialog = startHome.openSelectFileDialog()
        XCTAssertTrue(dialog.isDisplayed())

        let back = dialog.close()
        Thread.sleep(forTimeInterval: 0.3)
        XCTAssertTrue(
            back.isDisplayed(),
            "After Close the StartHomePage should be visible again."
        )
    }

    // MARK: — Test: SeededSqlite_AppearsInFileListView

    /// Mirrors C# SeededSqlite_AppearsInFileListView.
    ///
    /// If the seed throws inside SQLiteConnection (Batteries_V2.Init /
    /// linker stripping regression), no file appears and the dialog shows
    /// empty state — that is the production bug this test guards against.
    func testSeededSqlite_AppearsInFileListView() throws {
        XCTAssertTrue(startHome.isDisplayed())

        startHome.seedSqliteForTesting()
        // Brief settle so the seed write completes before we open the dialog
        // (the dialog enumerates files synchronously in OnAppearing).
        Thread.sleep(forTimeInterval: 0.5)

        let dialog = startHome.openSelectFileDialog()
        XCTAssertTrue(
            dialog.isDisplayed(),
            "Dialog should be displayed."
        )

        XCTAssertTrue(
            dialog.isFileListVisible(),
            "After seeding a SQLite fixture the dialog should render the file list. " +
            "Empty state showing means the seed threw inside SQLiteConnection — " +
            "apply SQLitePCL.Batteries_V2.Init() in MauiProgram.CreateMauiApp()."
        )
    }

    // MARK: — Test: SeededSqlite_TappingCard_LoadsAndDismissesDialog

    /// Mirrors C# SeededSqlite_TappingCard_LoadsAndDismissesDialog.
    ///
    /// C# excludes Win; iOS is included. Tests Stage 2 of the SQLite repro:
    /// card reachable + tap → dialog dismisses (load succeeded).
    func testSeededSqlite_TappingCard_LoadsAndDismissesDialog() throws {
        XCTAssertTrue(startHome.isDisplayed())

        startHome.seedSqliteForTesting()
        Thread.sleep(forTimeInterval: 0.5)

        let dialog = startHome.openSelectFileDialog()
        XCTAssertTrue(dialog.isDisplayed(), "Dialog should be displayed.")

        let fileName = StartHomePageObject.uiTestSqliteFixtureFileName
        let fileEl = dialog.fileItem(fileName: fileName)
        XCTAssertTrue(
            fileEl.exists,
            "Seeded SQLite '\(fileName)' should appear as a card."
        )

        fileEl.tap()
        // Generous wait: load is async (Task.Run) + modal pop animation.
        Thread.sleep(forTimeInterval: 1.5)

        // File list no longer visible ⇒ modal dismissed ⇒ load succeeded.
        // We don't probe StartHome.Title because on iPhone XCUITest may report
        // it visible=false after the layout shifts to Home mode (C# comment).
        XCTAssertFalse(
            dialog.isFileListVisible(timeout: 2),
            "After tapping the seeded SQLite card, the SelectFile dialog should dismiss. " +
            "If the file list is still visible, the load failed — likely LoaderSQL.CreateAsync " +
            "threw inside the live MAUI runtime (open-flag / read-path issue)."
        )
    }

    // MARK: — Test: OpenDialog_WithSeededFixtures_ShowsFolderAndFileAtRoot

    /// Mirrors C# OpenDialog_WithSeededFixtures_ShowsFolderAndFileAtRoot.
    /// C# excludes Win; iOS is included.
    func testOpenDialog_WithSeededFixtures_ShowsFolderAndFileAtRoot() throws {
        XCTAssertTrue(startHome.isDisplayed())
        startHome.seedSampleFilesForTesting()
        Thread.sleep(forTimeInterval: 0.3)

        let dialog = startHome.openSelectFileDialog()

        XCTAssertTrue(
            dialog.isFileListVisible(),
            "Seeded fixtures should switch the dialog to the file-list state."
        )
        XCTAssertFalse(
            dialog.isBreadcrumbVisible(),
            "At the root directory the breadcrumb should be hidden."
        )
        XCTAssertTrue(
            dialog.isFolderItemVisible(folderName: StartHomePageObject.seededSubFolderName),
            "Sub-folder card '\(StartHomePageObject.seededSubFolderName)' should be reachable by AutomationId."
        )
        XCTAssertTrue(
            dialog.isFileItemVisible(fileName: StartHomePageObject.seededRootFileName),
            "Root file card '\(StartHomePageObject.seededRootFileName)' should be reachable by AutomationId."
        )
    }

    // MARK: — Test: TapFolder_DrillsIntoSubFolder_ShowsBreadcrumbAndUpCard

    /// Mirrors C# TapFolder_DrillsIntoSubFolder_ShowsBreadcrumbAndUpCard.
    /// C# excludes Win; iOS is included.
    func testTapFolder_DrillsIntoSubFolder_ShowsBreadcrumbAndUpCard() throws {
        XCTAssertTrue(startHome.isDisplayed())
        startHome.seedSampleFilesForTesting()
        Thread.sleep(forTimeInterval: 0.3)

        let dialog = startHome.openSelectFileDialog()
        let subFolder = StartHomePageObject.seededSubFolderName
        guard dialog.isFolderItemVisible(folderName: subFolder) else {
            XCTFail("Precondition: sub-folder card '\(subFolder)' should be visible at root.")
            return
        }

        dialog.tapFolderItem(folderName: subFolder)
        Thread.sleep(forTimeInterval: 0.4)

        XCTAssertTrue(
            dialog.isBreadcrumbVisible(),
            "After drilling into a sub-folder the breadcrumb should be shown."
        )
        // Breadcrumb label should include the sub-folder name.
        let breadcrumbLabel = dialog.breadcrumb.label
        XCTAssertTrue(
            breadcrumbLabel.contains(subFolder),
            "Breadcrumb text should include the sub-folder name. Got='\(breadcrumbLabel)'."
        )
        XCTAssertTrue(
            dialog.isFileItemVisible(fileName: StartHomePageObject.seededNestedFileName),
            "Nested file card '\(StartHomePageObject.seededNestedFileName)' should be visible after drill-down."
        )
        XCTAssertTrue(
            dialog.upFolderItem.exists,
            "Up-folder card should be visible when not at the root."
        )
    }

    // MARK: — Test: TapUpFolder_ReturnsToRoot

    /// Mirrors C# TapUpFolder_ReturnsToRoot.
    /// C# excludes Win; iOS is included.
    func testTapUpFolder_ReturnsToRoot() throws {
        XCTAssertTrue(startHome.isDisplayed())
        startHome.seedSampleFilesForTesting()
        Thread.sleep(forTimeInterval: 0.3)

        let dialog = startHome.openSelectFileDialog()
        let subFolder = StartHomePageObject.seededSubFolderName

        dialog.tapFolderItem(folderName: subFolder)
        Thread.sleep(forTimeInterval: 0.4)
        guard dialog.isBreadcrumbVisible() else {
            XCTFail("Precondition: breadcrumb should be visible after drilling in.")
            return
        }

        dialog.tapUpFolder()
        Thread.sleep(forTimeInterval: 0.4)

        XCTAssertFalse(
            dialog.isBreadcrumbVisible(),
            "Back at root the breadcrumb should be hidden again."
        )
        XCTAssertTrue(
            dialog.isFolderItemVisible(folderName: subFolder),
            "Sub-folder card should be visible again at the root."
        )
        XCTAssertFalse(
            dialog.isFileItemVisible(fileName: StartHomePageObject.seededNestedFileName),
            "Nested file card should not be visible at the root."
        )
    }

    // MARK: — Test: TapFileCard_LoadsFileAndDismissesModal

    /// Mirrors C# TapFileCard_LoadsFileAndDismissesModal.
    /// C# excludes Win; iOS is included.
    ///
    /// Tapping the root file card → real load path runs → modal dismisses →
    /// StartHome transitions to Home mode (WorkGroupList visible).
    func testTapFileCard_LoadsFileAndDismissesModal() throws {
        XCTAssertTrue(startHome.isDisplayed())
        startHome.seedSampleFilesForTesting()
        Thread.sleep(forTimeInterval: 0.3)

        let dialog = startHome.openSelectFileDialog()
        let rootFile = StartHomePageObject.seededRootFileName
        guard dialog.isFileItemVisible(fileName: rootFile) else {
            XCTFail("Precondition: root file card '\(rootFile)' should be visible.")
            return
        }

        dialog.tapFileItem(fileName: rootFile)

        // Successful load transitions Start→Home mode (TRANSITION_MS≈380 ms).
        // Poll generously for slow CI runners.
        XCTAssertTrue(
            startHome.isWorkGroupListVisible(timeout: 10),
            "After a successful load the modal should dismiss and StartHome should be in Home mode (WorkGroupList visible)."
        )
    }

    // MARK: — Test: BrowseButton_FollowsFilePickerOverride_LoadsAndDismisses

    /// Mirrors C# BrowseButton_FollowsFilePickerOverride_LoadsAndDismisses.
    ///
    /// Sets up the FilePicker override (a JSON fixture outside TimetableFileDirectory
    /// + override that returns its path), opens dialog in empty state, taps Browse —
    /// the real load path runs without driving the OS picker UI.
    func testBrowseButton_FollowsFilePickerOverride_LoadsAndDismisses() throws {
        XCTAssertTrue(startHome.isDisplayed())

        // Set up the override before opening the modal — the FilePickerProvider
        // is a static so it doesn't matter which page is active when the seam fires.
        startHome.setupBrowseFallbackForTesting()
        Thread.sleep(forTimeInterval: 0.2)

        let dialog = startHome.openSelectFileDialog()
        guard dialog.isEmptyStateVisible() else {
            XCTFail(
                "Precondition: empty state expected " +
                "(TimetableFileDirectory was wiped in setUp; fallback file lives outside it)."
            )
            return
        }

        dialog.tapBrowse()

        // Override returns a valid file synchronously; dialog runs the real load+dismiss path.
        XCTAssertTrue(
            startHome.isWorkGroupListVisible(timeout: 10),
            "After the override returns a valid file the modal should dismiss and StartHome should be in Home mode."
        )
    }

    // MARK: — Test: OpenStorageLocationButton_IsReachable

    /// Mirrors C# OpenStorageLocationButton_IsReachable.
    ///
    /// The C# test has an Android branch (button hidden) but we're on iOS only.
    /// Just verify the button is reachable.
    func testOpenStorageLocationButton_IsReachable() throws {
        let dialog = startHome.openSelectFileDialog()
        XCTAssertTrue(
            dialog.openStorageLocationButton.exists,
            "The OpenStorageLocation button should be reachable in both file-list and empty states."
        )
    }
}
