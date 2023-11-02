namespace TRViS.ViewModels;

public interface IAppCenterSetting
{
	bool IsEnabled { get; set; }

	bool IsLogShareEnabled { get; set; }

	bool IsAnalyticsEnabled { get; set; }

	string InstallId { get; }
}
