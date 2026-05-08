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
			public const string SelectTrain = "Shell.Flyout.SelectTrain";
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

	public static class SelectTrain
	{
		public const string Title = "SelectTrain.Title";
		public const string LoadSampleButton = "SelectTrain.LoadSampleButton";
		public const string LoadFromWebButton = "SelectTrain.LoadFromWebButton";
		public const string SelectDatabaseButton = "SelectTrain.SelectDatabaseButton";
		public const string WorkGroupList = "SelectTrain.WorkGroupList";
		public const string WorkList = "SelectTrain.WorkList";
		// DEBUG-only seed buttons used by UI tests.
		public const string TestSeedButton = "SelectTrain.TestSeedButton";
		public const string TestSeedGpsButton = "SelectTrain.TestSeedGpsButton";
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

	public static class SelectOnlineResource
	{
		public const string CloseButton = "SelectOnlineResource.CloseButton";
		public const string LoadButton = "SelectOnlineResource.LoadButton";
		public const string UrlInput = "SelectOnlineResource.UrlInput";
		public const string UrlHistoryList = "SelectOnlineResource.UrlHistoryList";
		public const string AdviceLabel = "SelectOnlineResource.AdviceLabel";
		// Per-row id is "<UrlHistoryItemPrefix><url>".
		public const string UrlHistoryItemPrefix = "SelectOnlineResource.UrlHistoryItem.";
	}

	public static class Settings
	{
		public const string ReloadSavedButton = "Settings.ReloadSavedButton";
		public const string SaveButton = "Settings.SaveButton";
	}

	public static class ThirdParty
	{
		public const string LicenseList = "ThirdParty.LicenseList";
	}
}
