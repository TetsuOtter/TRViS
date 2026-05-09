using TRViS.FirebaseWrapper;
using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS.RootPages;

public partial class PrivacyPolicyDialog : ContentPage
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	FirebaseSettingViewModel FirebaseSettingViewModel { get; }

	public PrivacyPolicyDialog()
	{
		logger.Trace("Creating");
		// Work on a working copy so cancellation (close without save) leaves the live VM untouched.
		FirebaseSettingViewModel = new(InstanceManager.FirebaseSettingViewModel);
		BindingContext = FirebaseSettingViewModel;
		InitializeComponent();
		logger.Trace("Created");
	}

	private void OnResetButtonClicked(object? sender, EventArgs e)
	{
		logger.Trace("Reset clicked");
		FirebaseSettingViewModel.CopyFrom(InstanceManager.FirebaseSettingViewModel);
		FirebaseSettingViewModel.IsEnabled = false;
		FirebaseSettingViewModel.SaveAndApplySettings(true);
	}

	private async void OnSaveButtonClicked(object? sender, EventArgs e)
	{
		logger.Trace("Save clicked");
		// "Initial setting" = was-not-yet-accepted before this save. Used to gate the
		// post-save Shell.GoToAsync: on Mac Catalyst / iOS, flipping FlyoutBehavior
		// from Disabled to Flyout via IsEnabledChanged does not re-render the
		// navigation bar / flyout toggle button. The legacy FirebaseSettingPage
		// worked around this by calling Shell.Current.GoToAsync after save in the
		// same condition. We mirror that here so the flyout toggle becomes
		// reachable for the first time post-acceptance.
		bool wasInitialSetting = !InstanceManager.FirebaseSettingViewModel.IsPrivacyPolicyAccepted;

		FirebaseSettingViewModel.IsEnabled = true;
		FirebaseSettingViewModel.LastAcceptedPrivacyPolicyRevision = Constants.PRIVACY_POLICY_REVISION;

		InstanceManager.FirebaseSettingViewModel.CopyFrom(FirebaseSettingViewModel);
		InstanceManager.FirebaseSettingViewModel.SaveAndApplySettings(true);

		MauiProgram.ConfigureFirebase();
		InstanceManager.AnalyticsWrapper.Log(AnalyticsEvents.PrivacyPolicyAccepted);

		await Navigation.PopModalAsync();

		if (wasInitialSetting)
		{
			try
			{
				await Shell.Current.GoToAsync("//" + nameof(StartHomePage));
			}
			catch (Exception ex)
			{
				logger.Error(ex, "Post-save Shell navigation failed");
			}
		}
	}

	private async void OnCloseClicked(object? sender, EventArgs e)
	{
		logger.Trace("Close clicked");
		await Navigation.PopModalAsync();
	}
}
