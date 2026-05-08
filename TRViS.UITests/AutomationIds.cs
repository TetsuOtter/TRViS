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
		public const string WorkGroupList = "StartHome.WorkGroupList";
		public const string WorkList = "StartHome.WorkList";
		public const string OpenButton = "StartHome.OpenButton";
		public const string DisconnectButton = "StartHome.DisconnectButton";

		// UI_TEST-only seed seams.
		public const string TestSeedButton = "StartHome.TestSeedButton";
		public const string TestSeedGpsButton = "StartHome.TestSeedGpsButton";
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
