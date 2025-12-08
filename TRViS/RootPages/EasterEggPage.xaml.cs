using TRViS.Services;
using TRViS.Utils;
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

		// Initialize AppThemePicker selection based on ViewModel
		UpdateAppThemePickerSelection();

		// Initialize TimeProgressionRatePicker selection based on ViewModel
		UpdateTimeProgressionRatePickerSelection();

		// Update picker when ViewModel's SelectedAppTheme changes
		ViewModel.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(EasterEggPageViewModel.SelectedAppTheme))
			{
				UpdateAppThemePickerSelection();
			}
			else if (e.PropertyName == nameof(EasterEggPageViewModel.TimeProgressionRate))
			{
				UpdateTimeProgressionRatePickerSelection();
			}
		};

#if IOS || MACCATALYST
		ShowMapWhenLandscapeHeaderLabel.IsVisible = DeviceInfo.Idiom != DeviceIdiom.Phone;
#else
		ShowMapWhenLandscapeHeaderLabel.IsVisible = false;
#endif

		// KeepScreenOnWhenRunning is only for phones and tablets
		KeepScreenOnWhenRunningHeaderLabel.IsVisible = DeviceInfo.Idiom == DeviceIdiom.Phone || DeviceInfo.Idiom == DeviceIdiom.Tablet;

		AdvancedSettingsBorder.IsVisible = ShowMapWhenLandscapeHeaderLabel.IsVisible || KeepScreenOnWhenRunningHeaderLabel.IsVisible;

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

	private bool _isUpdatingAppThemePicker = false;

	private void OnAppThemePickerSelectedIndexChanged(object sender, EventArgs e)
	{
		if (_isUpdatingAppThemePicker)
			return;

		if (sender is not Picker picker)
			return;

		AppTheme newTheme = picker.SelectedIndex switch
		{
			0 => AppTheme.Unspecified,
			1 => AppTheme.Light,
			2 => AppTheme.Dark,
			_ => AppTheme.Unspecified
		};

		logger.Info("AppTheme changed to {0}", newTheme);
		ViewModel.SelectedAppTheme = newTheme;
	}

	private void UpdateAppThemePickerSelection()
	{
		_isUpdatingAppThemePicker = true;
		try
		{
			AppThemePicker.SelectedIndex = ViewModel.SelectedAppTheme switch
			{
				AppTheme.Unspecified => 0,
				AppTheme.Light => 1,
				AppTheme.Dark => 2,
				_ => 0
			};
		}
		finally
		{
			_isUpdatingAppThemePicker = false;
		}
	}

	private bool _isUpdatingTimeProgressionRatePicker = false;

	private void OnTimeProgressionRatePickerSelectedIndexChanged(object sender, EventArgs e)
	{
		if (_isUpdatingTimeProgressionRatePicker)
			return;

		if (sender is not Picker picker)
			return;

		TimeProgressionRate newRate = picker.SelectedIndex switch
		{
			0 => TimeProgressionRate.Normal,
			1 => TimeProgressionRate.X30,
			2 => TimeProgressionRate.X60,
			_ => TimeProgressionRate.Normal
		};

		logger.Info("TimeProgressionRate changed to {0}", newRate);
		ViewModel.TimeProgressionRate = newRate;
	}

	private void UpdateTimeProgressionRatePickerSelection()
	{
		_isUpdatingTimeProgressionRatePicker = true;
		try
		{
			TimeProgressionRatePicker.SelectedIndex = ViewModel.TimeProgressionRate switch
			{
				TimeProgressionRate.Normal => 0,
				TimeProgressionRate.X30 => 1,
				TimeProgressionRate.X60 => 2,
				_ => 0
			};
		}
		finally
		{
			_isUpdatingTimeProgressionRatePicker = false;
		}
	}
}
