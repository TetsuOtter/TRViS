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
		// Visible only when no WorkGroup is tentatively selected. Doubles as a
		// regression marker: if the auto-cascade ever returns, this hint disappears.
		public const string WorkPendingHint = "StartHome.WorkPendingHint";
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
		// Seeds a minimal SQLite fixture into TimetableFileDirectory via the same
		// sqlite-net write path LoaderSQL reads from. Tests use this to verify the
		// MAUI runtime can actually open SQLite (catches Batteries_V2.Init /
		// linker stripping regressions that NUnit-only tests can't reach).
		public const string TestSeedSqliteButton = "StartHome.TestSeedSqliteButton";
		// Wipes TimetableFileDirectory contents so SelectFile tests start from a
		// known empty state regardless of prior session leftovers.
		public const string TestClearTimetablesButton = "StartHome.TestClearTimetablesButton";
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
