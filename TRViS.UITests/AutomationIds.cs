namespace TRViS.UITests;

/// <summary>
/// AutomationId constants matching the values set in XAML files.
/// </summary>
public static class AutomationIds
{
	public static class Shell
	{
		public const string VersionLabel = "Shell.VersionLabel";

		public static class Flyout
		{
			public const string StartHome = "Shell.Flyout.StartHome";
			public const string DTAC = "Shell.Flyout.DTAC";
			public const string ThirdPartyLicenses = "Shell.Flyout.ThirdPartyLicenses";
			public const string Settings = "Shell.Flyout.Settings";
			public const string Firebase = "Shell.Flyout.Firebase";
			public const string PrivacyPolicy = "Shell.Flyout.PrivacyPolicy";
		}
	}

	public static class Firebase
	{
		public const string Title = "Firebase.Title";
		public const string AnalyticsSwitch = "Firebase.AnalyticsSwitch";
		public const string ResetButton = "Firebase.ResetButton";
		public const string SaveButton = "Firebase.SaveButton";
	}

	/// <summary>
	/// Start/Home page (replaces the legacy SelectTrain page). The same page
	/// renders two visual states; the Start-mode buttons (Connect/SelectFile/Demo,
	/// Privacy/TPL links) live in StartBody, the Home-mode list/buttons live in
	/// HomeBody. Both share the animated app-header at the top.
	/// </summary>
	public static class StartHome
	{
		public const string AppHeader = "StartHome.AppHeader";
		public const string AppIcon = "StartHome.AppIcon";
		public const string Title = "StartHome.Title";
		public const string StartBody = "StartHome.StartBody";
		public const string HomeBody = "StartHome.HomeBody";

		// Start mode — primary buttons
		public const string ConnectServerButton = "StartHome.ConnectServerButton";
		public const string SelectFileButton = "StartHome.SelectFileButton";
		public const string LoadDemoButton = "StartHome.LoadDemoButton";

		// Start mode — privacy banner + footer links
		public const string PrivacyReconfirmBanner = "StartHome.PrivacyReconfirmBanner";
		public const string PrivacyReconfirmText = "StartHome.PrivacyReconfirmText";
		public const string PrivacyPolicyButton = "StartHome.PrivacyPolicyButton";
		public const string ThirdPartyLicensesButton = "StartHome.ThirdPartyLicensesButton";

		// Home mode — selection lists and action buttons
		public const string LoaderInfoTitle = "StartHome.LoaderInfoTitle";
		public const string LoaderInfoDetail = "StartHome.LoaderInfoDetail";
		// Two-step picker: each step has a list (full picker) and a chip (compact summary
		// shown after selection). Tapping the chip clears the selection and re-opens
		// the list. Auto-fill happens when a list arrives with exactly one item AND
		// the user has not previously cleared their selection.
		public const string WorkGroupList = "StartHome.WorkGroupList";
		public const string WorkGroupChip = "StartHome.WorkGroupChip";
		public const string WorkList = "StartHome.WorkList";
		public const string WorkChip = "StartHome.WorkChip";
		public const string OpenButton = "StartHome.OpenButton";
		public const string DisconnectButton = "StartHome.DisconnectButton";

		// UI_TEST-only seed seams.
		public const string TestSeedButton = "StartHome.TestSeedButton";
		public const string TestSeedGpsButton = "StartHome.TestSeedGpsButton";
		// Picks the first WorkGroup + first Work and commits via the same code path
		// as 開く. Lets DTAC-focused tests skip the picker UI without depending on
		// auto-cascade behavior in TimetableSelectionManager.
		public const string TestAutoOpenButton = "StartHome.TestAutoOpenButton";
		// Clears AppViewModel.ExternalResourceUrlHistory in-memory + on disk so
		// "empty history" tests can guarantee an empty list regardless of prior
		// noReset:true session state.
		public const string TestClearHistoryButton = "StartHome.TestClearHistoryButton";
		// Picks the first WorkGroup + first Work, replaces the Work record with a
		// clone that has HasETrainTimetable=true + a tiny PNG payload, and
		// navigates to DTAC. Lets the horizontal-timetable tests exercise the
		// "button visible" path without doctoring the sample data fixture.
		public const string TestSeedHorizontalTimetableButton = "StartHome.TestSeedHorizontalTimetableButton";
		// Seeds a minimal SQLite fixture into TimetableFileDirectory via the same
		// sqlite-net write path LoaderSQL reads from. Tests use this to verify the
		// MAUI runtime can actually open SQLite (catches Batteries_V2.Init /
		// linker stripping regressions that NUnit-only tests can't reach).
		public const string TestSeedSqliteButton = "StartHome.TestSeedSqliteButton";
		// Wipes TimetableFileDirectory contents so SelectFile tests start from a
		// known empty state regardless of prior session leftovers.
		public const string TestClearTimetablesButton = "StartHome.TestClearTimetablesButton";
		// Writes the canonical SelectFileDialog fixture (root file + sub-folder
		// with another file) into TimetableFileDirectory.
		public const string TestSeedSampleFilesButton = "StartHome.TestSeedSampleFilesButton";
		// Wipes TimetableFileDirectory + nulls FilePickerProvider.OverrideForTesting.
		// Tests call this in SetUp because iOS noReset:true keeps the documents
		// folder warm across sessions and the override static survives Driver.Quit().
		public const string TestClearSampleFilesButton = "StartHome.TestClearSampleFilesButton";
		// Writes a JSON fixture into CacheDirectory and installs a FilePicker
		// override that returns its path. Lets the Browse-fallback test exercise
		// the post-pick load path without driving the OS file picker UI.
		public const string TestSetupBrowseFallbackButton = "StartHome.TestSetupBrowseFallbackButton";
		// Cascades selection to a sample-data train whose NextTrainId is non-empty
		// (linear-train-1) so the NextTrainButton-visibility regression test for
		// #225 doesn't rely on the default first-train selection (which has an
		// empty NextTrainId).
		public const string TestSeedNextTrainSelectionButton = "StartHome.TestSeedNextTrainSelectionButton";
		// Sets AppViewModel.Loader=null + disposes the previous loader, mirroring
		// OnDisconnectClicked's clear path but skipping the user-facing confirm
		// dialog. Used by fixtures that share a single Appium session and need
		// each test to start from "Start mode" (LoadDemoButton visible) — by
		// default a prior LoadSample leaves the page in Home mode and the
		// LoadDemo button hidden behind the loader-info card.
		public const string TestClearLoaderButton = "StartHome.TestClearLoaderButton";

		// Direct invoker for OnSelectFileClicked. Bypasses the styled
		// SelectFileButton because Appium UIAutomator2's ACTION_CLICK against
		// MAUI's PrimaryActionButton-styled Button silently fails to dispatch
		// Button.Clicked on Android in the shared-session run (CI run
		// 25734141479: seam buttons fire, SelectFileButton does not, both have
		// enabled=true / clickable=true / visible=true in the accessibility
		// tree). The seam handler routes to OnSelectFileClicked so the test
		// still exercises Navigation.PushModalAsync(SelectFileDialog).
		public const string TestOpenSelectFileDialogButton = "StartHome.TestOpenSelectFileDialogButton";
	}

	public static class PrivacyDialog
	{
		public const string Title = "PrivacyDialog.Title";
		public const string CloseButton = "PrivacyDialog.CloseButton";
		public const string AnalyticsSwitch = "PrivacyDialog.AnalyticsSwitch";
		public const string ResetButton = "PrivacyDialog.ResetButton";
		public const string SaveButton = "PrivacyDialog.SaveButton";
	}

	public static class DTAC
	{
		public const string MenuButton = "DTAC.MenuButton";
		public const string TimeLabel = "DTAC.TimeLabel";
		public const string TitleLabel = "DTAC.TitleLabel";
		public const string TabHako = "DTAC.TabHako";
		public const string TabTimetable = "DTAC.TabTimetable";
		public const string TabWorkAffix = "DTAC.TabWorkAffix";

		public const string StartEndRunButton = "DTAC.StartEndRunButton";
		public const string LocationServiceButton = "DTAC.LocationServiceButton";
		public const string OpenCloseButton = "DTAC.OpenCloseButton";
		public const string TimetableScrollView = "DTAC.TimetableScrollView";
		public const string VerticalTimetableView = "DTAC.VerticalTimetableView";
		public const string NextTrainButton = "DTAC.NextTrainButton";

		// Visible only when the selected Work carries an embedded horizontal
		// timetable (HasETrainTimetable + ETrainTimetableContent). Tapping it
		// pushes HorizontalTimetablePage onto the Shell stack.
		public const string HorizontalTimetableButton = "DTAC.HorizontalTimetableButton";

		// UI_TEST-only seam: issues Shell.Current.GoToAsync("//StartHomePage")
		// directly so shared-session fixtures can recover from DTAC without
		// going through the flyout. The flyout is unreliable on Android when
		// VerticalView mode has locked orientation to Landscape — the MenuButton
		// click dispatches but the NavigationView never attaches to the
		// DrawerLayout, so WaitForFlyoutItem times out. Bypassing via direct
		// shell navigation triggers ViewHost.OnDisappearing which also unlocks
		// the orientation.
		public const string TestNavigateHomeButton = "DTAC.TestNavigateHomeButton";

		// UI_TEST-only state mirrors. The real AppBar TitleLabel / TimeLabel
		// don't reliably surface on iOS (iOS only exposes a Label in the
		// accessibility tree when its text is non-empty, and TimeLabel
		// additionally hides on narrow phones via a width threshold). These
		// invisible mirror labels are kept always non-empty by sentinel
		// prefixes (TestSeamTitlePrefix / TestSeamTimePrefix) so they are
		// always findable. Tests strip the prefix before asserting.
		public const string TestTitleSeam = "DTAC.TestTitleSeam";
		public const string TestTimeSeam = "DTAC.TestTimeSeam";
		public const string TestSeamTitlePrefix = "T:";
		public const string TestSeamTimePrefix = "C:";

		// UI_TEST-only seam: changes the first non-InfoRow in the current train's
		// TimetableRows to IsInfoRow=true, then re-sets AppViewModel.SelectedTrainData
		// with the modified clone. This exercises the WebSocket soft-update code path
		// (same train ID → ApplyPositionAlignedDiff → PropertyChanged("IsInfoRow") →
		// UpdateAllComponents) to reproduce the "station-name label stays visible after
		// IsInfoRow false→true transition" bug.
		public const string TestSeedIsInfoRowTransitionButton = "DTAC.TestSeedIsInfoRowTransitionButton";

		// AutomationId pattern for timetable row components (set in UI_TEST builds only).
		// Use string.Format: TimetableRowStationNamePattern.Replace("{0}", rowIndex.ToString())
		public const string TimetableRowStationNamePattern = "TimetableRow.{0}.StationName";
		public const string TimetableRowInfoRowPattern = "TimetableRow.{0}.InfoRow";
	}

	/// <summary>
	/// Horizontal timetable page (PNG/JPG/PDF/URI displayed in a WebView).
	/// Reached by tapping <see cref="DTAC.HorizontalTimetableButton"/>.
	/// </summary>
	public static class HorizontalTimetable
	{
		public const string WebView = "HorizontalTimetable.WebView";
		// AppBar back button. Tapping it pops the page back to DTAC so the
		// Shell flyout is reachable again. Exposed to the test layer so a
		// fixture-end TearDown can return the app to a Shell-rooted page
		// (HT is a Shell.GoToAsync push and the flyout is gated to roots).
		public const string BackButton = "HorizontalTimetable.BackButton";
	}

	/// <summary>
	/// Connect-to-Server modal dialog (replaces the legacy SelectOnlineResource popup).
	/// Two states: history list (rich cards keyed by URL) and a new-connection form
	/// (URL Entry + "save connection" Switch + Connect button).
	/// </summary>
	public static class ConnectServer
	{
		public const string Title = "ConnectServer.Title";
		public const string CloseButton = "ConnectServer.CloseButton";

		// History list state
		public const string HistoryList = "ConnectServer.HistoryList";
		// Per-row id is "<HistoryItemPrefix><url>" — entire card is tappable
		// (no shared Entry, so tap loads directly).
		public const string HistoryItemPrefix = "ConnectServer.HistoryItem.";
		public const string NewConnectionButton = "ConnectServer.NewConnectionButton";

		// New-connection form state
		public const string BackToHistoryButton = "ConnectServer.BackToHistoryButton";
		public const string UrlInput = "ConnectServer.UrlInput";
		public const string SaveConnectionSwitch = "ConnectServer.SaveConnectionSwitch";
		public const string ConnectButton = "ConnectServer.ConnectButton";
	}

	/// <summary>
	/// Select-File modal dialog (replaces the direct OS FilePicker behaviour).
	/// Lists JSON/SQLite files from the app documents folder as rich cards plus
	/// a "他の場所からファイルを開く" button that falls back to the OS picker.
	/// </summary>
	public static class SelectFile
	{
		public const string Title = "SelectFile.Title";
		public const string CloseButton = "SelectFile.CloseButton";

		// File list state
		public const string FileList = "SelectFile.FileList";
		// Label inside FileListView. Used by IsFileListVisible() because Android's
		// UiAutomator2 doesn't surface a ScrollView's AutomationId reliably, so
		// probing FileList directly returns false even when files are showing.
		// Labels are surfaced consistently across all four platforms.
		public const string FileListHint = "SelectFile.FileListHint";
		// Per-row ids — entire card is tappable. Folder cards drill into the folder;
		// the up-folder card is rendered above sibling cards when not at root.
		public const string FileItemPrefix = "SelectFile.FileItem.";
		public const string FolderItemPrefix = "SelectFile.FolderItem.";
		public const string UpFolderItem = "SelectFile.UpFolderItem";

		// Breadcrumb showing the current relative path (only visible when not at root).
		public const string Breadcrumb = "SelectFile.Breadcrumb";

		// Empty state (visible when the current folder has no folders or supported files).
		public const string EmptyMessage = "SelectFile.EmptyMessage";

		// Always-visible footer actions.
		// Browse: opens the OS FilePicker for files outside the documents folder.
		// OpenStorageLocation: reveals the documents folder in the OS file manager.
		public const string BrowseButton = "SelectFile.BrowseButton";
		public const string OpenStorageLocationButton = "SelectFile.OpenStorageLocationButton";
	}

	public static class Settings
	{
		public const string ReloadSavedButton = "Settings.ReloadSavedButton";
		public const string SaveButton = "Settings.SaveButton";
	}

	public static class ThirdParty
	{
		public const string LicenseList = "ThirdParty.LicenseList";
		// Visible only when the page is hosted as a modal (asModal:true).
		public const string ModalCloseButton = "ThirdParty.ModalCloseButton";
	}
}
