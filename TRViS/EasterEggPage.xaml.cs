using TRViS.MyAppCustomizables;
using TRViS.ViewModels;

namespace TRViS;

public partial class EasterEggPage : ContentPage
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
	EasterEggPageViewModel ViewModel { get; }

	public EasterEggPage(EasterEggPageViewModel vm)
	{
		logger.Trace("EasterEggPage Creating (EasterEggPageViewModel: {0})", vm);

		InitializeComponent();

		BindingContext = vm;
		ViewModel = vm;

		LogFilePathLabel.Text = DirectoryPathProvider.NormalLogFileDirectory.FullName;

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
		logger.Trace("Executing...");

		await ViewModel.LoadFromFileAsync();

		logger.Info("Reload Complete");
	}

	private async void OnSaveClicked(object sender, EventArgs e)
	{
		logger.Trace("Executing...");

		await ViewModel.SaveAsync();

		logger.Info("Saved");
		await DisplayAlert("Success!", "Successfully saved", "OK");
	}
}
