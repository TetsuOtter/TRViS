// AutomationIds.swift
// XCUITest equivalents of TRViS.UITests/AutomationIds.cs
// Mirrors the C# naming hierarchy using Swift enums with static string constants.
// Only the identifiers needed for AppLaunchTests (and the shared base) are included here;
// add more as additional tests are ported.

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

        // URL-history seams (ConnectServer tests)
        static let testClearHistoryButton = "StartHome.TestClearHistoryButton"

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
        static let testNavigateHomeButton = "DTAC.TestNavigateHomeButton"
        static let testTitleSeam          = "DTAC.TestTitleSeam"
        static let testTimeSeam           = "DTAC.TestTimeSeam"
        static let testSeamTitlePrefix    = "T:"
        static let testSeamTimePrefix     = "C:"
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

    enum Settings {
        static let reloadSavedButton = "Settings.ReloadSavedButton"
    }
}
