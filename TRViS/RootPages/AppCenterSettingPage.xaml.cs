using TRViS.ViewModels;

namespace TRViS.RootPages;

public partial class AppCenterSettingPage : ContentPage
{
	public static readonly string NameOfThisClass = nameof(AppCenterSettingPage);
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

	AppCenterSettingViewModel AppCenterSettingViewModel { get; }
	public AppCenterSettingPage()
	{
		logger.Trace("Creating");
		AppCenterSettingViewModel = new(InstanceManager.AppCenterSettingViewModel);
		BindingContext = AppCenterSettingViewModel;
		InitializeComponent();
		logger.Trace("Created");
	}

	private async void OnResetButtonClicked(object? sender, EventArgs e)
	{
		logger.Trace("Executing...");
		AppCenterSettingViewModel.CopyFrom(InstanceManager.AppCenterSettingViewModel);
		AppCenterSettingViewModel.IsEnabled = false;
		await AppCenterSettingViewModel.SaveAndApplySettings(true);
		logger.Trace("Executed");
	}

	private async void OnSaveButtonClicked(object? sender, EventArgs e)
	{
		logger.Trace("Executing...");

		bool isInitialSetting = !AppCenterSettingViewModel.IsEnabled;
		logger.Debug("IsInitialSetting: {0}", isInitialSetting);
		AppCenterSettingViewModel.IsEnabled = true;

		InstanceManager.AppCenterSettingViewModel.CopyFrom(AppCenterSettingViewModel);
		await InstanceManager.AppCenterSettingViewModel.SaveAndApplySettings(true);
		logger.Trace("SaveAndApply Executed");

		string installId = await AppCenterSettingViewModel.GetInstallId();
		await DisplayAlert("Success!", "Successfully saved\nYour InstallId: " + installId, "OK");

		// 初回はこのページが自動で表示されている状態のため、自動でページを移動するようにする
		// 次回以降はユーザが自分で移動してきたはずであるため、自動で移動しないようにする
		if (isInitialSetting)
		{
			await Shell.Current.GoToAsync("//" + nameof(SelectTrainPage));
		}
	}
}
