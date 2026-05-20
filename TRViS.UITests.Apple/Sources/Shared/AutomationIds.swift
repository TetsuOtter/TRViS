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

        // UI_TEST-only seam buttons (subset needed for reset/setup)
        static let testSeedButton       = "StartHome.TestSeedButton"
        static let testClearLoaderButton = "StartHome.TestClearLoaderButton"
        static let testAutoOpenButton    = "StartHome.TestAutoOpenButton"
        static let testSetLanguageEnglishButton = "StartHome.TestSetLanguageEnglishButton"
        static let testSetLanguageJapaneseButton = "StartHome.TestSetLanguageJapaneseButton"
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

    enum Settings {
        static let reloadSavedButton = "Settings.ReloadSavedButton"
    }
}
