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
	}

	public static class DTAC
	{
		public const string MenuButton = "DTAC.MenuButton";
		public const string TimeLabel = "DTAC.TimeLabel";
		public const string TitleLabel = "DTAC.TitleLabel";
		public const string TabHako = "DTAC.TabHako";
		public const string TabTimetable = "DTAC.TabTimetable";
		public const string TabWorkAffix = "DTAC.TabWorkAffix";
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
