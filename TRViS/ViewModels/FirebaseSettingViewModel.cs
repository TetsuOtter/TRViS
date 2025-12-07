using CommunityToolkit.Mvvm.ComponentModel;

using TRViS.Services;
using TRViS.Utils;

namespace TRViS.ViewModels;

public partial class FirebaseSettingViewModel : ObservableObject, IFirebaseSetting
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	[ObservableProperty]
	public partial bool IsEnabled { get; set; }

	[ObservableProperty]
	public partial bool IsLogShareEnabled { get; set; }

	[ObservableProperty]
	public partial bool IsAnalyticsEnabled { get; set; }

	[ObservableProperty]
	public partial string LastAcceptedPrivacyPolicyRevision { get; set; } = "";
	public bool IsPrivacyPolicyAccepted => LastAcceptedPrivacyPolicyRevision == Constants.PRIVACY_POLICY_REVISION;

	public string InstallId { get; private set; } = "";

	public FirebaseSettingViewModel()
	{
		logger.Trace("Creating");
		IsEnabled = AppPreferenceService.Get(AppPreferenceKeys.IsAppCenterEnabled, false, out _);
		IsAnalyticsEnabled = AppPreferenceService.Get(AppPreferenceKeys.IsAppCenterAnalyticsEnabled, false, out _);
		IsLogShareEnabled = AppPreferenceService.Get(AppPreferenceKeys.IsAppCenterLogShareEnabled, false, out _);
		LastAcceptedPrivacyPolicyRevision = AppPreferenceService.Get(AppPreferenceKeys.LastAcceptedPrivacyPolicyRevision, string.Empty, out _);
		InstallId = AppPreferenceService.Get(AppPreferenceKeys.InstallId, string.Empty, out _);

		logger.Trace("Created (IsEnabled: {0}, IsAnalyticsEnabled: {1}, IsLogShareEnabled: {2}, InstallId: {3}, LastAcceptedPrivacyPolicyRevision: {4})",
			IsEnabled,
			IsAnalyticsEnabled,
			IsLogShareEnabled,
			InstallId,
			LastAcceptedPrivacyPolicyRevision
		);

		if (string.IsNullOrEmpty(InstallId))
		{
			InstallId = Guid.NewGuid().ToString();
			logger.Info("New InstallId: {0}", InstallId);
		}

		if (!IsPrivacyPolicyAccepted)
		{
			logger.Warn($"PrivacyPolicy not accepted (last accepted: {LastAcceptedPrivacyPolicyRevision})");
			IsEnabled = false;
		}
	}

	public FirebaseSettingViewModel(FirebaseSettingViewModel src)
	{
		logger.Trace("Copying (src: {0})", src);
		CopyFrom(src);
		logger.Trace("Copied (IsEnabled: {0}, IsAnalyticsEnabled: {1}, IsLogShareEnabled: {2}, InstallId: {3}, LastAcceptedPrivacyPolicyRevision: {4})",
			IsEnabled,
			IsAnalyticsEnabled,
			IsLogShareEnabled,
			InstallId,
			LastAcceptedPrivacyPolicyRevision
		);
	}

	public FirebaseSettingViewModel CopyFrom(IFirebaseSetting src)
	{
		logger.Trace("Copying (src: {0})", src);
		IsEnabled = src.IsEnabled;
		IsAnalyticsEnabled = src.IsAnalyticsEnabled;
		IsLogShareEnabled = src.IsLogShareEnabled;
		InstallId = src.InstallId;
		LastAcceptedPrivacyPolicyRevision = src.LastAcceptedPrivacyPolicyRevision;
		logger.Trace("Copied  (dst: {0})", this);
		return this;
	}

	public ValueChangedEventHandler<bool>? IsEnabledChanged;
	partial void OnIsEnabledChanged(bool oldValue, bool newValue)
	{
		IsEnabledChanged?.Invoke(this, oldValue, newValue);
	}

	public bool SaveAndApplySettings(bool doSave = true)
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
			AppPreferenceService.Set(AppPreferenceKeys.InstallId, InstallId);
			AppPreferenceService.Set(AppPreferenceKeys.LastAcceptedPrivacyPolicyRevision, LastAcceptedPrivacyPolicyRevision);
		}

		if (IsEnabled)
		{
			InstanceManager.AnalyticsWrapper.SetIsEnabled(IsAnalyticsEnabled);
		}

		return true;
	}

	#region overrides
	public bool Equals(FirebaseSettingViewModel? other)
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
			&&
			LastAcceptedPrivacyPolicyRevision == other.LastAcceptedPrivacyPolicyRevision
		;
	}
	public override bool Equals(object? obj)
	{
		if (obj is FirebaseSettingViewModel vm)
			return Equals(vm);
		else
			return (false);
	}

	public override int GetHashCode() => HashCode.Combine(IsEnabled, IsAnalyticsEnabled, IsLogShareEnabled, InstallId, LastAcceptedPrivacyPolicyRevision);

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
			+ ", "
			+ $"LastAcceptedPrivacyPolicyRevision: {LastAcceptedPrivacyPolicyRevision}"
			+ "}";
	}
	#endregion overrides
}
