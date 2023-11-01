using CommunityToolkit.Mvvm.ComponentModel;

using Microsoft.AppCenter;

using TRViS.Services;

namespace TRViS.ViewModels;

public partial class AppCenterSettingViewModel : ObservableObject, IAppCenterSetting
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

	[ObservableProperty]
	bool _IsEnabled;

	[ObservableProperty]
	bool _IsLogShareEnabled;

	[ObservableProperty]
	bool _IsAnalyticsEnabled;

	public string InstallId { get; private set; } = string.Empty;

	public AppCenterSettingViewModel()
	{
		logger.Trace("Creating");
		IsEnabled = AppPreferenceService.Get(AppPreferenceKeys.IsAppCenterEnabled, false, out _);
		IsAnalyticsEnabled = AppPreferenceService.Get(AppPreferenceKeys.IsAppCenterAnalyticsEnabled, false, out _);
		IsLogShareEnabled = AppPreferenceService.Get(AppPreferenceKeys.IsAppCenterLogShareEnabled, false, out _);
		logger.Trace("Created (IsEnabled: {0}, IsAnalyticsEnabled: {1}, IsLogShareEnabled: {2})",
			IsEnabled,
			IsAnalyticsEnabled,
			IsLogShareEnabled
		);
	}

	public AppCenterSettingViewModel(AppCenterSettingViewModel src)
	{
		logger.Trace("Copying (src: {0})", src);
		CopyFrom(src);
		logger.Trace("Copied (IsEnabled: {0}, IsAnalyticsEnabled: {1}, IsLogShareEnabled: {2}, InstallId: {3})",
			IsEnabled,
			IsAnalyticsEnabled,
			IsLogShareEnabled,
			InstallId
		);
	}

	public AppCenterSettingViewModel CopyFrom(IAppCenterSetting src)
	{
		IsEnabled = src.IsEnabled;
		IsAnalyticsEnabled = src.IsAnalyticsEnabled;
		IsLogShareEnabled = src.IsLogShareEnabled;
		InstallId = src.InstallId;
		return this;
	}

	public ValueChangedEventHandler<bool>? IsEnabledChanged;
	partial void OnIsEnabledChanged(bool oldValue, bool newValue)
	{
		IsEnabledChanged?.Invoke(this, oldValue, newValue);
	}

	public static async Task<string> GetInstallId()
	{
		if (await AppCenter.IsEnabledAsync() != true)
		{
			logger.Warn("AppCenter is not enabled");
			return string.Empty;
		}

		Guid? installId = await AppCenter.GetInstallIdAsync();
		if (installId is Guid id)
		{
			logger.Trace("InstallId: {0}", id);
			return id.ToString();
		}
		else
		{
			logger.Warn("InstallId is null");
			return string.Empty;
		}
	}

	public async Task<bool> SaveAndApplySettings(bool doSave = true)
	{
		logger.Trace("Saving (IsEnabled: {0}, IsAnalyticsEnabled: {1}, IsLogShareEnabled: {2})",
			IsEnabled,
			IsAnalyticsEnabled,
			IsLogShareEnabled
		);
		if (doSave)
		{
			AppPreferenceService.Set(AppPreferenceKeys.IsAppCenterEnabled, IsEnabled);
			AppPreferenceService.Set(AppPreferenceKeys.IsAppCenterAnalyticsEnabled, IsAnalyticsEnabled);
			AppPreferenceService.Set(AppPreferenceKeys.IsAppCenterLogShareEnabled, IsLogShareEnabled);
		}

		bool applySuccess = await TRViS.Services.AppCenterService.ApplySettingAsync(this);
		if (applySuccess && IsEnabled)
			InstallId = await GetInstallId();
		return applySuccess;
	}

	#region overrides
	public bool Equals(AppCenterSettingViewModel? other)
	{
		if (other is null)
			return false;

		if (ReferenceEquals(this, other))
			return true;

		return
			IsEnabled == other.IsEnabled
			&&
			IsAnalyticsEnabled == other.IsAnalyticsEnabled
			&&
			IsLogShareEnabled == other.IsLogShareEnabled
			&&
			InstallId == other.InstallId
		;
	}
	public override bool Equals(object? obj)
	{
		if (obj is AppCenterSettingViewModel vm)
			return Equals(vm);
		else
			return (false);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(IsEnabled, IsAnalyticsEnabled, IsLogShareEnabled, InstallId);
	}

	public override string ToString()
	{
		return "{"
			+ $"IsEnabled: {IsEnabled}"
			+ ", "
			+ $"IsAnalyticsEnabled: {IsAnalyticsEnabled}"
			+ ", "
			+ $"IsLogShareEnabled: {IsLogShareEnabled}"
			+ ", "
			+ $"InstallId: {InstallId}"
			+ "}";
	}
	#endregion overrides
}
