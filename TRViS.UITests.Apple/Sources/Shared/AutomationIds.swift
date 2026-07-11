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
        // Clears in-memory privacy-policy acceptance so the reconfirm banner
        // reappears, for capturing the not-yet-agreed VRT state (#287).
        static let testClearPrivacyPolicyButton = "StartHome.TestClearPrivacyPolicyButton"

        // GPS seed seam (DTACTimetableTests)
        static let testSeedGpsButton    = "StartHome.TestSeedGpsButton"

        // NextTrain seed seam (DTACTimetableTests)
        static let testSeedNextTrainSelectionButton = "StartHome.TestSeedNextTrainSelectionButton"

        // HorizontalTimetable seed seam (HorizontalTimetableTests)
        static let testSeedHorizontalTimetableButton = "StartHome.TestSeedHorizontalTimetableButton"

        // Hako diagram (ハコ図) seed seam (ScreenshotRegressionTests) — commits
        // WorkGroup "hako-diagram-test" / Work "hako-diagram-7stations" and
        // navigates to DTAC.
        static let testSeedHakoDiagramButton = "StartHome.TestSeedHakoDiagramButton"

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
        // Per-train ハコ図 (diagram) train-number button id (UI_TEST builds only,
        // tablet diagram layout — see DiagramView.cs). Append the TrainNumber.
        // Distinct from hakoRowPrefix because the diagram button is not a list row.
        static let hakoDiagramButtonPrefix = "DTAC.HakoDiagram."
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
        static let webView      = "HorizontalTimetable.WebView"
        static let webViewReady = "HorizontalTimetable.WebView.Ready"
        static let backButton   = "HorizontalTimetable.BackButton"
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

    /// Notification (通告) popup. Shown when a server-pushed Notification is
    /// received and judged unread. Has Title / BBCode body, an importance badge
    /// (Priority >= 1), a 受領 (Acknowledge) button and a 閉じる (Close) button.
    enum Notification {
        static let popup             = "Notification.Popup"
        static let orderNumber       = "Notification.OrderNumber"
        static let sender            = "Notification.Sender"
        static let receiver          = "Notification.Receiver"
        static let iconBadge         = "Notification.IconBadge"
        static let iconImage         = "Notification.IconImage"
        static let title             = "Notification.Title"
        static let body              = "Notification.Body"
        static let importantBadge    = "Notification.ImportantBadge"
        static let issuedAt          = "Notification.IssuedAt"
        static let acknowledgeButton = "Notification.AcknowledgeButton"
        static let dismissButton     = "Notification.DismissButton"
        static let closeButton       = "Notification.CloseButton"

        /// Small non-modal banner overlaid on the DTAC ViewHost's content area.
        /// Shown for the initial 受領必須 compact display (CompactDisplay=true) and
        /// for the acknowledged/区間連動 redisplay (no 受領 button in that case).
        /// Tapping it expands to the large Popup.
        enum Banner {
            static let root              = "Notification.Banner"
            static let summary           = "Notification.Banner.Summary"
            static let acknowledgeButton = "Notification.Banner.AcknowledgeButton"
            static let chevron           = "Notification.Banner.Chevron"
        }
    }
}
