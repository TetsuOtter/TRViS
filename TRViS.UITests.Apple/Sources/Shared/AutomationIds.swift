// AutomationIds.swift
// XCUITest equivalents of TRViS.UITests/AutomationIds.cs
// Mirrors the C# naming hierarchy using Swift enums with static string constants.
// Only the identifiers needed for AppLaunchTests (and the shared base) are included here;
// add more as new tests require them.

enum AutomationIds {

    enum Shell {
        static let versionLabel = "Shell.VersionLabel"

        enum Flyout {
            static let startHome = "Shell.Flyout.StartHome"
            static let dtac     = "Shell.Flyout.DTAC"
            static let settings = "Shell.Flyout.Settings"
            static let originalTimetableV1 = "Shell.Flyout.OriginalTimetableV1"
            static let originalTimetableV2 = "Shell.Flyout.OriginalTimetableV2"
            static let originalTimetableV4 = "Shell.Flyout.OriginalTimetableV4"
            static let originalTimetableV6 = "Shell.Flyout.OriginalTimetableV6"
        }
    }

    enum StartHome {
        static let appHeader  = "StartHome.AppHeader"
        static let appIcon    = "StartHome.AppIcon"
        static let title      = "StartHome.Title"
        static let startBody  = "StartHome.StartBody"
        static let homeBody   = "StartHome.HomeBody"

        // Start mode — primary buttons
        static let connectServerButton = "StartHome.ConnectServerButton"
        static let selectFileButton    = "StartHome.SelectFileButton"
        static let loadDemoButton      = "StartHome.LoadDemoButton"

        // Start mode — privacy banner + footer links
        static let privacyReconfirmBanner = "StartHome.PrivacyReconfirmBanner"
        static let privacyReconfirmText   = "StartHome.PrivacyReconfirmText"
        static let privacyPolicyButton    = "StartHome.PrivacyPolicyButton"
        static let thirdPartyLicensesButton = "StartHome.ThirdPartyLicensesButton"

        // Home mode — selection UI
        static let workGroupList = "StartHome.WorkGroupList"
        static let workGroupChip = "StartHome.WorkGroupChip"

        // UI_TEST-only seam buttons
        static let testSeedButton       = "StartHome.TestSeedButton"
        static let testClearLoaderButton = "StartHome.TestClearLoaderButton"
        static let testAutoOpenButton    = "StartHome.TestAutoOpenButton"
        static let testSetLanguageEnglishButton  = "StartHome.TestSetLanguageEnglishButton"
        static let testSetLanguageJapaneseButton = "StartHome.TestSetLanguageJapaneseButton"

        // Test seams used by ScreenshotRegressionTests for pixel-stable captures
        // Pins AppTimeProvider at 09:41:00 so the DTAC live clock is pixel-stable.
        static let testFreezeClockButton   = "StartHome.TestFreezeClockButton"
        static let testUnfreezeClockButton = "StartHome.TestUnfreezeClockButton"
        // Force app-wide Light / Dark theme for deterministic cross-palette captures.
        static let testForceLightThemeButton = "StartHome.TestForceLightThemeButton"
        static let testForceDarkThemeButton  = "StartHome.TestForceDarkThemeButton"
        // Reset theme to Unspecified (follow OS) after the screenshot walk.
        static let testResetThemeButton = "StartHome.TestResetThemeButton"

        // GPS seed seam (DTACTimetableTests)
        static let testSeedGpsButton    = "StartHome.TestSeedGpsButton"

        // NextTrain seed seam (DTACTimetableTests)
        static let testSeedNextTrainSelectionButton = "StartHome.TestSeedNextTrainSelectionButton"

        // HorizontalTimetable seed seam (HorizontalTimetableTests)
        static let testSeedHorizontalTimetableButton = "StartHome.TestSeedHorizontalTimetableButton"

        // URL-history seams (ConnectServer tests)
        static let testClearHistoryButton = "StartHome.TestClearHistoryButton"

        // Home mode — loader/connection status (#261)
        static let loaderInfoTitle   = "StartHome.LoaderInfoTitle"
        static let openButton        = "StartHome.OpenButton"
        static let disconnectButton  = "StartHome.DisconnectButton"
        // Visible only while a WebSocket loader's connection is lost.
        static let reconnectButton   = "StartHome.ReconnectButton"

        // WebSocket seam buttons (WebSocketReconnectTests / WebSocketStatusIndicatorTests)
        static let testSimulateWebSocketDisconnectButton  = "StartHome.TestSimulateWebSocketDisconnectButton"
        static let testSimulateWebSocketConnectedButton   = "StartHome.TestSimulateWebSocketConnectedButton"

        // SQLite / sample-file seams (SelectFile tests)
        static let testSeedSqliteButton         = "StartHome.TestSeedSqliteButton"
        static let testSeedSampleFilesButton    = "StartHome.TestSeedSampleFilesButton"
        static let testClearSampleFilesButton   = "StartHome.TestClearSampleFilesButton"
        static let testSetupBrowseFallbackButton = "StartHome.TestSetupBrowseFallbackButton"

        // Direct invoker for OnSelectFileClicked — bypasses the styled button
        // to avoid UIAutomator2 dispatch issues; kept here for parity.
        static let testOpenSelectFileDialogButton = "StartHome.TestOpenSelectFileDialogButton"
    }

    enum PrivacyDialog {
        static let title         = "PrivacyDialog.Title"
        static let closeButton   = "PrivacyDialog.CloseButton"
        static let analyticsSwitch = "PrivacyDialog.AnalyticsSwitch"
        static let resetButton   = "PrivacyDialog.ResetButton"
        static let saveButton    = "PrivacyDialog.SaveButton"
    }

    enum DTAC {
        static let menuButton             = "DTAC.MenuButton"
        static let timeLabel              = "DTAC.TimeLabel"
        static let titleLabel             = "DTAC.TitleLabel"
        static let tabHako                = "DTAC.TabHako"
        static let tabTimetable           = "DTAC.TabTimetable"
        static let tabWorkAffix           = "DTAC.TabWorkAffix"

        static let startEndRunButton      = "DTAC.StartEndRunButton"
        static let locationServiceButton  = "DTAC.LocationServiceButton"
        static let openCloseButton        = "DTAC.OpenCloseButton"
        // Per-train ハコ row button id (UI_TEST builds only). Append the TrainNumber.
        static let hakoRowPrefix          = "DTAC.HakoRow."
        // Remarks panel toggle (UI_TEST builds only). openCloseButton is the PageHeader toggle.
        static let remarksOpenCloseButton = "DTAC.RemarksOpenCloseButton"
        static let timetableScrollView    = "DTAC.TimetableScrollView"
        static let verticalTimetableView  = "DTAC.VerticalTimetableView"
        static let nextTrainButton        = "DTAC.NextTrainButton"
        static let horizontalTimetableButton = "DTAC.HorizontalTimetableButton"

        // UI_TEST-only seam buttons
        static let testNavigateHomeButton = "DTAC.TestNavigateHomeButton"

        // UI_TEST-only seams (#266): mutate AppViewModel's WebSocket connection flags
        // so the AppBar status indicator can be driven through states on DTAC.
        static let testWsConnectedButton    = "DTAC.TestWsConnectedButton"
        static let testWsDisconnectedButton = "DTAC.TestWsDisconnectedButton"
        static let testWsReconnectingButton = "DTAC.TestWsReconnectingButton"
        static let testSeedIsInfoRowTransitionButton = "DTAC.TestSeedIsInfoRowTransitionButton"

        // UI_TEST-only state mirrors
        static let testTitleSeam          = "DTAC.TestTitleSeam"
        static let testTimeSeam           = "DTAC.TestTimeSeam"
        static let testSeamTitlePrefix    = "T:"
        static let testSeamTimePrefix     = "C:"

        // AutomationId patterns for timetable row components (UI_TEST builds only).
        // Use String(format:) to substitute the row index.
        static let timetableRowStationNamePattern = "TimetableRow.%d.StationName"
        static let timetableRowInfoRowPattern     = "TimetableRow.%d.InfoRow"
    }

    /// Horizontal timetable page (PNG/JPG/PDF/URI displayed in a WebView).
    enum HorizontalTimetable {
        static let webView    = "HorizontalTimetable.WebView"
        static let backButton = "HorizontalTimetable.BackButton"
    }

    enum ThirdParty {
        static let licenseList      = "ThirdParty.LicenseList"
        static let modalCloseButton = "ThirdParty.ModalCloseButton"
    }

    /// Connect-to-Server modal dialog.
    /// Two states: history list (rich cards keyed by URL) and a new-connection form.
    enum ConnectServer {
        static let title              = "ConnectServer.Title"
        static let closeButton        = "ConnectServer.CloseButton"

        // History list state
        static let historyList        = "ConnectServer.HistoryList"
        // Per-row id is "<historyItemPrefix><url>" — entire card is tappable.
        static let historyItemPrefix  = "ConnectServer.HistoryItem."
        static let newConnectionButton = "ConnectServer.NewConnectionButton"

        // New-connection form state
        static let backToHistoryButton = "ConnectServer.BackToHistoryButton"
        static let urlInput            = "ConnectServer.UrlInput"
        static let saveConnectionSwitch = "ConnectServer.SaveConnectionSwitch"
        static let connectButton       = "ConnectServer.ConnectButton"
    }

    /// Select-File modal dialog.
    /// Lists JSON/SQLite files from the app documents folder as rich cards plus
    /// a "他の場所からファイルを開く" button that falls back to the OS picker.
    enum SelectFile {
        static let title           = "SelectFile.Title"
        static let closeButton     = "SelectFile.CloseButton"

        // File list state
        static let fileList        = "SelectFile.FileList"
        // Label inside FileListView — probed because ScrollView AutomationId
        // isn't always surfaced reliably in the accessibility tree.
        static let fileListHint    = "SelectFile.FileListHint"
        // Per-row ids — entire card is tappable.
        static let fileItemPrefix   = "SelectFile.FileItem."
        static let folderItemPrefix = "SelectFile.FolderItem."
        static let upFolderItem    = "SelectFile.UpFolderItem"

        // Breadcrumb showing the current relative path (only visible when not at root).
        static let breadcrumb      = "SelectFile.Breadcrumb"

        // Empty state (visible when the current folder has no supported files).
        static let emptyMessage    = "SelectFile.EmptyMessage"

        // Always-visible footer actions.
        static let browseButton              = "SelectFile.BrowseButton"
        static let openStorageLocationButton = "SelectFile.OpenStorageLocationButton"
    }

    /// Shared title bar (TRViS.DTAC.AppBar), shown on the DTAC ViewHost.
    enum AppBar {
        // UI_TEST-only invisible mirror Label reflecting AppViewModel.ServerConnectionStatus (#266).
        // Sentinel-prefixed so it is always non-empty / findable. Strip the prefix before asserting.
        static let connectionStatus       = "AppBar.ConnectionStatus"
        static let connectionStatusPrefix = "S:"
    }

    enum Settings {
        static let reloadSavedButton = "Settings.ReloadSavedButton"
    }

    /// Independent timetable display pages (V1/V2/V4/V6).
    /// V1 = "Modern Classic" — CollectionView-based row list with sticky train
    /// header, marker badges, memo dots, note toggles. Phase 1 covers tablet
    /// layout (width >= 600pt); compact placeholder shows on narrower viewports.
    enum OriginalTimetable {
        enum V1 {
            static let root               = "OriginalTimetable.V1.Root"
            static let tabletGrid         = "OriginalTimetable.V1.TabletGrid"
            static let compactPlaceholder = "OriginalTimetable.V1.CompactPlaceholder"

            static let header             = "OriginalTimetable.V1.Header"
            static let headerTypeChip     = "OriginalTimetable.V1.Header.TypeChip"
            static let headerTrainNumber  = "OriginalTimetable.V1.Header.TrainNumber"
            static let headerDestination  = "OriginalTimetable.V1.Header.Destination"
            static let headerCarCount     = "OriginalTimetable.V1.Header.CarCount"
            static let headerMaxSpeed     = "OriginalTimetable.V1.Header.MaxSpeed"

            static let emptyState         = "OriginalTimetable.V1.EmptyState"
            static let rowsList           = "OriginalTimetable.V1.RowsList"

            // Per-row id patterns; append the row's TimetableRow.Id.
            static let rowPrefix = "OriginalTimetable.V1.Row."
            static func row(_ rowId: String) -> String           { rowPrefix + rowId }
            static func marker(_ rowId: String) -> String        { rowPrefix + rowId + ".Marker" }
            static func memo(_ rowId: String) -> String          { rowPrefix + rowId + ".Memo" }
            static func clear(_ rowId: String) -> String         { rowPrefix + rowId + ".Clear" }
            static func markerBadge(_ rowId: String) -> String   { rowPrefix + rowId + ".MarkerBadge" }
            // Phase 2 — folded inline note body Border rendered below the
            // normal row when ToggleNote is on; carries the row's Remarks.
            static func noteBody(_ rowId: String) -> String      { rowPrefix + rowId + ".NoteBody" }

            // Phase 2 overlay UI — marker chooser popover (AnchorPopover) and
            // the bottom-sheet memo editor. Both are statically declared in
            // XAML so AutomationIds are constant, not row-scoped.
            enum MarkerPopover {
                static let none    = "OriginalTimetable.V1.MarkerPopover.None"
                static let flag    = "OriginalTimetable.V1.MarkerPopover.Flag"
                static let caution = "OriginalTimetable.V1.MarkerPopover.Caution"
                static let star    = "OriginalTimetable.V1.MarkerPopover.Star"
            }

            enum MemoSheet {
                static let root   = "OriginalTimetable.V1.MemoSheet"
                static let scrim  = "OriginalTimetable.V1.MemoSheet.Scrim"
                static let editor = "OriginalTimetable.V1.MemoSheet.Editor"
                static let save   = "OriginalTimetable.V1.MemoSheet.Save"
                static let delete = "OriginalTimetable.V1.MemoSheet.Delete"
                static let cancel = "OriginalTimetable.V1.MemoSheet.Cancel"
            }

            // UI_TEST-only seam: invokes OnCycleMarker / OnClearMarker on the
            // first normal (non-section-break) row through the same handlers
            // the SwipeView Command bindings wire to. Lets us cover the
            // View→VM marker pipeline without depending on simulated swipe
            // gestures reaching the SwipeItem on every platform / OS version.
            static let testCycleMarkerRow0Button = "OriginalTimetable.V1.Test.CycleMarkerRow0"
            static let testClearMarkerRow0Button = "OriginalTimetable.V1.Test.ClearMarkerRow0"
        }
    }
}
