using TRViS.FirebaseWrapper;
using TRViS.ViewModels;

namespace TRViS.RootPages;

public partial class FirebaseSettingPage : ContentPage
{
	public static readonly string NameOfThisClass = nameof(FirebaseSettingPage);
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	FirebaseSettingViewModel FirebaseSettingViewModel { get; }
	public FirebaseSettingPage()
	{
		logger.Trace("Creating");
		FirebaseSettingViewModel = new(InstanceManager.FirebaseSettingViewModel);
		BindingContext = FirebaseSettingViewModel;
		InitializeComponent();
		logger.Trace("Created");
	}

	private void OnResetButtonClicked(object? sender, EventArgs e)
	{
		logger.Trace("Executing...");
		FirebaseSettingViewModel.CopyFrom(InstanceManager.FirebaseSettingViewModel);
		FirebaseSettingViewModel.IsEnabled = false;
		FirebaseSettingViewModel.SaveAndApplySettings(true);
		logger.Trace("Executed");
	}

	private async void OnSaveButtonClicked(object? sender, EventArgs e)
	{
		logger.Trace("Executing...");

		bool isInitialSetting = !FirebaseSettingViewModel.IsEnabled || !FirebaseSettingViewModel.IsPrivacyPolicyAccepted;
		logger.Debug("IsInitialSetting: {0}", isInitialSetting);
		FirebaseSettingViewModel.IsEnabled = true;
		FirebaseSettingViewModel.LastAcceptedPrivacyPolicyRevision = Constants.PRIVACY_POLICY_REVISION;

		InstanceManager.FirebaseSettingViewModel.CopyFrom(FirebaseSettingViewModel);
		InstanceManager.FirebaseSettingViewModel.SaveAndApplySettings(true);
		logger.Trace("SaveAndApply Executed");
		MauiProgram.ConfigureFirebase();
		InstanceManager.AnalyticsWrapper.Log(AnalyticsEvents.PrivacyPolicyAccepted);

		await DisplayAlert("Success!", "Successfully saved\nYour InstallId: " + FirebaseSettingViewModel.InstallId, "OK");

		// 初回はこのページが自動で表示されている状態のため、自動でページを移動するようにする
		// 次回以降はユーザが自分で移動してきたはずであるため、自動で移動しないようにする
		if (isInitialSetting)
		{
			await Shell.Current.GoToAsync("//" + nameof(SelectTrainPage));
		}
	}
}
