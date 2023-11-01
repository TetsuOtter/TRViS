using TRViS.ViewModels;

namespace TRViS.RootPages;

public partial class AppCenterSettingPage : ContentPage
{
	public static readonly string NameOfThisClass = nameof(AppCenterSettingPage);

	AppCenterSettingViewModel AppCenterSettingViewModel { get; }
	public AppCenterSettingPage()
	{
		AppCenterSettingViewModel = new(InstanceManager.AppCenterSettingViewModel);
		InitializeComponent();
	}
}
