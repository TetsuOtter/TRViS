using TRViS.DTAC;
using TRViS.IO;
using TRViS.Services;
using TRViS.Utils;
using TRViS.ViewModels;

namespace TRViS.RootPages;

public partial class SelectTrainPage : ContentPage
{
	public static readonly string NameOfThisClass = nameof(SelectTrainPage);
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	AppViewModel viewModel { get; }

	public SelectTrainPage()
	{
		logger.Trace("Creating (AppViewModel: {0})", viewModel);

		InitializeComponent();

		this.viewModel = InstanceManager.AppViewModel;
		this.BindingContext = viewModel;

		logger.Trace("Created");
	}

	void LoadSampleButton_Clicked(object sender, EventArgs e)
	{
		logger.Info("Load Sample Button Clicked");

		viewModel.Loader?.Dispose();
		viewModel.Loader = new SampleDataLoader();

		logger.Info("Load Sample Button Clicked Processing Complete");
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		if (viewModel.Loader is null)
		{
			logger.Info("Loader is null -> attempting to load default timetable");

			try
			{
				(bool success, bool requiresFileSelection, string? selectedFilePath, string? errorMessage) =
					await viewModel.TryLoadDefaultTimetableAsync();

				if (success && !requiresFileSelection)
				{
					logger.Info("Default timetable loaded successfully");
					// Loader is already set by TryLoadDefaultTimetableAsync
					// Navigate to DTAC page
					await NavigateToDTACAsync();
					return;
				}

				if (success && requiresFileSelection)
				{
					logger.Info("Multiple JSON files found - showing file selection");
					await ShowFileSelectionDialogAsync();
					return;
				}

				if (errorMessage == "PrivacyPolicyNotAccepted")
				{
					logger.Info("Privacy policy not accepted yet - will load default timetable after user accepts policy");
					// Set SampleDataLoader as fallback
					viewModel.Loader = new SampleDataLoader();
					return;
				}

				// No default timetable found or error occurred
				logger.Info("No default timetable found or error occurred - setting SampleDataLoader as fallback");
				viewModel.Loader = new SampleDataLoader();
			}
			catch (Exception ex)
			{
				logger.Error(ex, "Error loading default timetable");
				InstanceManager.CrashlyticsWrapper.Log(ex, "SelectTrainPage.OnAppearing (TryLoadDefaultTimetableAsync failed)");
				viewModel.Loader = new SampleDataLoader();
			}
		}
	}

	private async Task NavigateToDTACAsync()
	{
		try
		{
			logger.Info("Navigating to DTAC page");
			// Navigate to the DTAC ViewHost page which is the second FlyoutItem
			await Shell.Current.GoToAsync($"//{ViewHost.NameOfThisClass}");
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Error navigating to DTAC page");
			InstanceManager.CrashlyticsWrapper.Log(ex, "SelectTrainPage.NavigateToDTACAsync failed");
		}
	}

	private async Task ShowFileSelectionDialogAsync()
	{
		try
		{
			var jsonFiles = DefaultTimetableFileLoader.GetAvailableJsonFiles();

			if (jsonFiles.Length == 0)
			{
				logger.Warn("No JSON files found for selection");
				viewModel.Loader = new SampleDataLoader();
				return;
			}

			var fileNames = jsonFiles.Select(f => f.Name).ToArray();
			var filePaths = jsonFiles.Select(f => f.FullName).ToArray();

			string? selectedFileName = await DisplayActionSheet(
				"どのファイルを開きますか？",
				"キャンセル",
				null,
				fileNames
			);

			if (string.IsNullOrEmpty(selectedFileName) || selectedFileName == "キャンセル")
			{
				logger.Info("File selection cancelled");
				viewModel.Loader = new SampleDataLoader();
				return;
			}

			// Find the full path of the selected file
			int selectedIndex = Array.IndexOf(fileNames, selectedFileName);
			if (selectedIndex >= 0 && selectedIndex < filePaths.Length)
			{
				bool loaded = await viewModel.LoadSelectedTimetableFileAsync(filePaths[selectedIndex]);
				if (loaded)
				{
					logger.Info("Selected timetable file loaded successfully");
					// Navigate to DTAC page
					await NavigateToDTACAsync();
				}
				else
				{
					logger.Warn("Failed to load selected timetable file");
					await Util.DisplayAlert(this, "エラー", "ファイルの読み込みに失敗しました", "OK");
					viewModel.Loader = new SampleDataLoader();
				}
			}
			else
			{
				logger.Warn("Invalid file selection index");
				viewModel.Loader = new SampleDataLoader();
			}
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Error in ShowFileSelectionDialogAsync");
			InstanceManager.CrashlyticsWrapper.Log(ex, "SelectTrainPage.ShowFileSelectionDialogAsync failed");
			await Util.DisplayAlert(this, "エラー", $"ファイル選択に失敗しました: {ex.Message}", "OK");
			viewModel.Loader = new SampleDataLoader();
		}
	}

	async void LoadFromWebButton_Clicked(object sender, EventArgs e)
	{
		logger.Info("Load From Web Button Clicked");

		try
		{
			var popup = new SelectOnlineResourcePopup();
			popup.OnOpened();
			await Navigation.PushModalAsync(popup);
		}
		catch (Exception ex)
		{
			InstanceManager.CrashlyticsWrapper.Log(ex, "SelectTrainPage.LoadFromWebButton_Clicked (PushModalAsync failed)");
			logger.Error(ex, "PushModalAsync failed");
			await Util.DisplayAlert(this, "Open Popup Failed", ex.ToString(), "OK");
		}

		logger.Info("Load From Web Button Clicked Processing Complete");
	}

	async void SelectDatabaseButton_Clicked(object sender, EventArgs e)
	{
		logger.Info("Select File Button Clicked");

		ILoader? lastLoader = viewModel.Loader;
		try
		{
			var result = await FilePicker.Default.PickAsync();

			if (result is not null)
			{
				logger.Info("File Selected: {0}", result.FullPath);

				if (result.FullPath.EndsWith(".json"))
				{
					logger.Debug("Loading JSON File");
					viewModel.Loader = await LoaderJson.InitFromFileAsync(result.FullPath);
					logger.Trace("LoaderJson Initialized");
				}
				else if (result.FullPath.EndsWith(".sqlite") || result.FullPath.EndsWith(".db") || result.FullPath.EndsWith(".sqlite3"))
				{
					logger.Debug("Loading SQLite File");
					viewModel.Loader = new LoaderSQL(result.FullPath);
					logger.Trace("LoaderSQL Initialized");
				}
				else
				{
					logger.Warn("Unknown File Type");
					await Util.DisplayAlert(this, "Unknown File Type", "The selected file is not a supported file type.", "OK");
				}

				if (!ReferenceEquals(lastLoader, viewModel.Loader))
				{
					logger.Debug("Loader changed -> dispose lastLoader");
					// どちらもnullの可能性がある
					lastLoader?.Dispose();
				}
			}
			else
			{
				logger.Info("File Selection Canceled (PickAsync result is null))");
			}
		}
		catch (Exception ex)
		{
			if (!ReferenceEquals(lastLoader, viewModel.Loader))
			{
				logger.Debug("Loader changed -> restore lastLoader");
				viewModel.Loader?.Dispose();
				viewModel.Loader = lastLoader;
			}

			InstanceManager.CrashlyticsWrapper.Log(ex, "SelectTrainPage.SelectDatabaseButton_Clicked (PickAsync failed)");
			logger.Error(ex, "File Selection Failed");
			await Util.DisplayAlert(this, "Cannot Open File", ex.ToString(), "OK");
		}

		logger.Info("Select File Button Clicked Processing Complete");
	}
}
