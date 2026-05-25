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
            // Phase 3 replaced the static <600pt placeholder with a real
            // CollectionView-backed compact layout. The constant stays so
            // pre-Phase 3 screenshots / comments still resolve; new code
            // should target `compactRoot` and the per-row helpers below.
            static let compactPlaceholder = "OriginalTimetable.V1.CompactPlaceholder"
            static let compactRoot        = "OriginalTimetable.V1.Compact.Root"
            static let compactHeader      = "OriginalTimetable.V1.Compact.Header"
            static let compactRowsList    = "OriginalTimetable.V1.Compact.RowsList"

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

            // Phase 3 — tweaks panel (gear button in header, overlay shown
            // on tap). Density tri-state segmented buttons set vm.Density;
            // ShowPasses toggle is two-way bound to vm.ShowPasses.
            static let tweaksButton = "OriginalTimetable.V1.TweaksButton"
            enum Tweaks {
                static let overlay     = "OriginalTimetable.V1.Tweaks.Overlay"
                static let scrim       = "OriginalTimetable.V1.Tweaks.Scrim"
                static let title       = "OriginalTimetable.V1.Tweaks.Title"
                static let showPasses  = "OriginalTimetable.V1.Tweaks.ShowPasses"
                enum Density {
                    static let compact     = "OriginalTimetable.V1.Tweaks.Density.Compact"
                    static let comfortable = "OriginalTimetable.V1.Tweaks.Density.Comfortable"
                    static let spacious    = "OriginalTimetable.V1.Tweaks.Density.Spacious"
                }
            }

            // UI_TEST-only seam: invokes OnCycleMarker / OnClearMarker on the
            // first normal (non-section-break) row through the same handlers
            // the SwipeView Command bindings wire to. Lets us cover the
            // View→VM marker pipeline without depending on simulated swipe
            // gestures reaching the SwipeItem on every platform / OS version.
            static let testCycleMarkerRow0Button = "OriginalTimetable.V1.Test.CycleMarkerRow0"
            static let testClearMarkerRow0Button = "OriginalTimetable.V1.Test.ClearMarkerRow0"
        }

        /// V2 = "Card Stack" — CollectionView-based stacked-card list with a
        /// sticky train header, SwipeView per row, shared bottom-sheet memo
        /// editor + tweaks overlay. Tablet (>=600pt) and compact layouts both
        /// implemented. Only the identifiers actually emitted by
        /// OriginalTimetableV2Page.xaml are mirrored here.
        enum V2 {
            static let root              = "OriginalTimetable.V2.Root"
            static let tabletGrid        = "OriginalTimetable.V2.TabletGrid"
            static let compactGrid       = "OriginalTimetable.V2.CompactGrid"

            static let header            = "OriginalTimetable.V2.Header"
            static let emptyState        = "OriginalTimetable.V2.EmptyState"
            static let rowsList          = "OriginalTimetable.V2.RowsList"

            static let compactHeader     = "OriginalTimetable.V2.Compact.Header"
            static let compactEmptyState = "OriginalTimetable.V2.Compact.EmptyState"
            static let compactRowsList   = "OriginalTimetable.V2.Compact.RowsList"

            // Per-row id patterns; append the row's TimetableRow.Id.
            static let rowPrefix = "OriginalTimetable.V2.Row."
            static func row(_ rowId: String) -> String         { rowPrefix + rowId }
            static func marker(_ rowId: String) -> String      { rowPrefix + rowId + ".Marker" }
            static func memo(_ rowId: String) -> String        { rowPrefix + rowId + ".Memo" }
            static func clear(_ rowId: String) -> String       { rowPrefix + rowId + ".Clear" }
            static func markerBadge(_ rowId: String) -> String { rowPrefix + rowId + ".MarkerBadge" }
            static func noteBody(_ rowId: String) -> String    { rowPrefix + rowId + ".NoteBody" }

            static let tweaksButton = "OriginalTimetable.V2.TweaksButton"
            enum Tweaks {
                static let overlay    = "OriginalTimetable.V2.Tweaks.Overlay"
                static let scrim      = "OriginalTimetable.V2.Tweaks.Scrim"
                static let title      = "OriginalTimetable.V2.Tweaks.Title"
                static let showPasses = "OriginalTimetable.V2.Tweaks.ShowPasses"
                enum Density {
                    static let compact     = "OriginalTimetable.V2.Tweaks.Density.Compact"
                    static let comfortable = "OriginalTimetable.V2.Tweaks.Density.Comfortable"
                    static let spacious    = "OriginalTimetable.V2.Tweaks.Density.Spacious"
                }
            }

            enum MemoSheet {
                static let root   = "OriginalTimetable.V2.MemoSheet"
                static let scrim  = "OriginalTimetable.V2.MemoSheet.Scrim"
                static let editor = "OriginalTimetable.V2.MemoSheet.Editor"
                static let save   = "OriginalTimetable.V2.MemoSheet.Save"
                static let delete = "OriginalTimetable.V2.MemoSheet.Delete"
                static let cancel = "OriginalTimetable.V2.MemoSheet.Cancel"
            }
        }

        /// V4 = "Next Big" — hero card highlighting next-arrival station + a
        /// MiniList of upcoming rows. AutomationIds match exactly what
        /// OriginalTimetableV4Page.xaml emits.
        enum V4 {
            static let root         = "OriginalTimetable.V4.Root"
            static let tabletGrid   = "OriginalTimetable.V4.TabletGrid"
            static let compactGrid  = "OriginalTimetable.V4.CompactGrid"

            // Persistent train-info strip rendered above the Hero area.
            static let trainStripe            = "OriginalTimetable.V4.TrainStripe"
            static let trainStripeTypeChip    = "OriginalTimetable.V4.TrainStripe.TypeChip"
            static let trainStripeTrainNumber = "OriginalTimetable.V4.TrainStripe.TrainNumber"

            // Hero card.
            static let hero               = "OriginalTimetable.V4.Hero"
            static let heroMarkerBadge    = "OriginalTimetable.V4.Hero.MarkerBadge"
            static let heroMarkerAdd      = "OriginalTimetable.V4.Hero.MarkerAdd"
            static let heroStation        = "OriginalTimetable.V4.HeroStation"
            static let heroArrive         = "OriginalTimetable.V4.HeroArrive"
            static let heroDepart         = "OriginalTimetable.V4.HeroDepart"
            static let heroTrack          = "OriginalTimetable.V4.HeroTrack"
            static let heroPreview        = "OriginalTimetable.V4.HeroPreview"
            static let heroNoteBody       = "OriginalTimetable.V4.Hero.NoteBody"

            static let emptyState       = "OriginalTimetable.V4.EmptyState"
            static let miniList         = "OriginalTimetable.V4.MiniList"
            static let compactMiniList  = "OriginalTimetable.V4.Compact.MiniList"

            // Per-row id patterns for MiniList rows.
            static let rowPrefix = "OriginalTimetable.V4.Row."
            static func row(_ rowId: String) -> String         { rowPrefix + rowId }
            static func marker(_ rowId: String) -> String      { rowPrefix + rowId + ".Marker" }
            static func memo(_ rowId: String) -> String        { rowPrefix + rowId + ".Memo" }
            static func clear(_ rowId: String) -> String       { rowPrefix + rowId + ".Clear" }
            static func markerBadge(_ rowId: String) -> String { rowPrefix + rowId + ".MarkerBadge" }
            static func noteBody(_ rowId: String) -> String    { rowPrefix + rowId + ".NoteBody" }

            static let tweaksButton = "OriginalTimetable.V4.TweaksButton"
            enum Tweaks {
                static let overlay    = "OriginalTimetable.V4.Tweaks.Overlay"
                static let scrim      = "OriginalTimetable.V4.Tweaks.Scrim"
                static let title      = "OriginalTimetable.V4.Tweaks.Title"
                static let showPasses = "OriginalTimetable.V4.Tweaks.ShowPasses"
                enum Density {
                    static let compact     = "OriginalTimetable.V4.Tweaks.Density.Compact"
                    static let comfortable = "OriginalTimetable.V4.Tweaks.Density.Comfortable"
                    static let spacious    = "OriginalTimetable.V4.Tweaks.Density.Spacious"
                }
            }

            enum MemoSheet {
                static let root   = "OriginalTimetable.V4.MemoSheet"
                static let scrim  = "OriginalTimetable.V4.MemoSheet.Scrim"
                static let editor = "OriginalTimetable.V4.MemoSheet.Editor"
                static let save   = "OriginalTimetable.V4.MemoSheet.Save"
                static let delete = "OriginalTimetable.V4.MemoSheet.Delete"
                static let cancel = "OriginalTimetable.V4.MemoSheet.Cancel"
            }
        }

        /// V6 = "Bold Editorial" — masthead + train stripe + past chips +
        /// large CurrentBlock + UpcomingList. AutomationIds match exactly what
        /// OriginalTimetableV6Page.xaml emits.
        enum V6 {
            static let root              = "OriginalTimetable.V6.Root"
            static let tabletGrid        = "OriginalTimetable.V6.TabletGrid"
            static let compactGrid       = "OriginalTimetable.V6.CompactGrid"

            static let masthead          = "OriginalTimetable.V6.Masthead"
            static let trainStripe       = "OriginalTimetable.V6.TrainStripe"
            static let pastChips         = "OriginalTimetable.V6.PastChips"
            static let currentBlock      = "OriginalTimetable.V6.CurrentBlock"
            static let currentBlockMarkerBadge = "OriginalTimetable.V6.CurrentBlock.MarkerBadge"
            static let currentBlockStationName = "OriginalTimetable.V6.CurrentBlock.StationName"
            static let currentBlockNoteBody    = "OriginalTimetable.V6.CurrentBlock.NoteBody"
            static let emptyState        = "OriginalTimetable.V6.EmptyState"
            static let upcomingList      = "OriginalTimetable.V6.UpcomingList"

            // Compact-layout mirrors.
            static let compactMasthead     = "OriginalTimetable.V6.Compact.Masthead"
            static let compactTrainStripe  = "OriginalTimetable.V6.Compact.TrainStripe"
            static let compactPastChips    = "OriginalTimetable.V6.Compact.PastChips"
            static let compactCurrentBlock = "OriginalTimetable.V6.Compact.CurrentBlock"
            static let compactCurrentBlockMarkerBadge = "OriginalTimetable.V6.Compact.CurrentBlock.MarkerBadge"
            static let compactCurrentBlockNoteBody    = "OriginalTimetable.V6.Compact.CurrentBlock.NoteBody"
            static let compactEmptyState   = "OriginalTimetable.V6.Compact.EmptyState"
            static let compactUpcomingList = "OriginalTimetable.V6.Compact.UpcomingList"

            static let tweaksButton        = "OriginalTimetable.V6.TweaksButton"
            static let compactTweaksButton = "OriginalTimetable.V6.Compact.TweaksButton"

            // Per-row id patterns (UpcomingList rows).
            static let rowPrefix = "OriginalTimetable.V6.Row."
            static func row(_ rowId: String) -> String         { rowPrefix + rowId }
            static func marker(_ rowId: String) -> String      { rowPrefix + rowId + ".Marker" }
            static func memo(_ rowId: String) -> String        { rowPrefix + rowId + ".Memo" }
            static func clear(_ rowId: String) -> String       { rowPrefix + rowId + ".Clear" }
            static func markerBadge(_ rowId: String) -> String { rowPrefix + rowId + ".MarkerBadge" }
            static func noteBody(_ rowId: String) -> String    { rowPrefix + rowId + ".NoteBody" }

            // V6-specific: past-chip prefix.
            static let pastChipPrefix = "OriginalTimetable.V6.PastChip."
            static func pastChip(_ rowId: String) -> String    { pastChipPrefix + rowId }

            enum Tweaks {
                static let overlay    = "OriginalTimetable.V6.Tweaks.Overlay"
                static let scrim      = "OriginalTimetable.V6.Tweaks.Scrim"
                static let title      = "OriginalTimetable.V6.Tweaks.Title"
                static let showPasses = "OriginalTimetable.V6.Tweaks.ShowPasses"
                enum Density {
                    static let compact     = "OriginalTimetable.V6.Tweaks.Density.Compact"
                    static let comfortable = "OriginalTimetable.V6.Tweaks.Density.Comfortable"
                    static let spacious    = "OriginalTimetable.V6.Tweaks.Density.Spacious"
                }
            }

            enum MemoSheet {
                static let root   = "OriginalTimetable.V6.MemoSheet"
                static let scrim  = "OriginalTimetable.V6.MemoSheet.Scrim"
                static let editor = "OriginalTimetable.V6.MemoSheet.Editor"
                static let save   = "OriginalTimetable.V6.MemoSheet.Save"
                static let delete = "OriginalTimetable.V6.MemoSheet.Delete"
                static let cancel = "OriginalTimetable.V6.MemoSheet.Cancel"
            }
        }
    }
}
