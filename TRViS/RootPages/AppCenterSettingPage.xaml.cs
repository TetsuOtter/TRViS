using TRViS.ViewModels;

namespace TRViS.RootPages;

public partial class AppCenterSettingPage : ContentPage
{
	AppCenterSettingViewModel AppCenterSettingViewModel { get; }
	public AppCenterSettingPage()
	{
		AppCenterSettingViewModel = new(InstanceManager.AppCenterSettingViewModel);
		InitializeComponent();
	}
}
