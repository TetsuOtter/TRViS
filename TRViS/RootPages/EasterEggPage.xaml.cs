using TRViS.ViewModels;

namespace TRViS.RootPages;

public partial class EasterEggPage : ContentPage
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	EasterEggPageViewModel ViewModel { get; }

	public EasterEggPage()
	{
		logger.Trace("EasterEggPage Creating");

		InitializeComponent();

		ViewModel = InstanceManager.EasterEggPageViewModel;
		BindingContext = ViewModel;

		LogFilePathLabel.Text = DirectoryPathProvider.GeneralLogFileDirectory.FullName;

		logger.Trace("EasterEggPage Created");
	}

	private void OnLoadFromPickerClicked(object sender, EventArgs e)
	{
		logger.Warn("Not Implemented");
	}
	private void OnSaveToPickerClicked(object sender, EventArgs e)
	{
		logger.Trace("Not Implemented");
	}

	private async void OnReloadSavedClicked(object sender, EventArgs e)
	{
		try
		{
			logger.Trace("Executing...");

			await ViewModel.LoadFromFileAsync();

			logger.Info("Reload Complete");
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Failed to reload");
			await DisplayAlert("Error", "Failed to reload\n" + ex.Message, "OK");
		}
	}

	private async void OnSaveClicked(object sender, EventArgs e)
	{
		try
		{
			logger.Trace("Executing...");

			await ViewModel.SaveAsync();

			logger.Info("Saved");
			await DisplayAlert("Success!", "Successfully saved", "OK");
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Failed to save");
			await DisplayAlert("Error", "Failed to save\n" + ex.Message, "OK");
		}
	}
}
