namespace TRViS.ViewModels;

public interface IFirebaseSetting
{
	bool IsEnabled { get; set; }

	bool IsLogShareEnabled { get; set; }

	bool IsAnalyticsEnabled { get; set; }

	string InstallId { get; }

	string LastAcceptedPrivacyPolicyRevision { get; set; }
}
