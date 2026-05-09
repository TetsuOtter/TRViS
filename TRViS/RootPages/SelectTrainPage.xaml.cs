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

	async void LoadSampleButton_Clicked(object sender, EventArgs e)
	{
		logger.Info("Load Sample Button Clicked");

		try
		{
			viewModel.Loader?.Dispose();
			viewModel.Loader = await SampleDataLoader.CreateAsync();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Failed to load sample data");
			InstanceManager.CrashlyticsWrapper.Log(ex, "SelectTrainPage.LoadSampleButton_Clicked (CreateAsync failed)");
			await Util.DisplayAlertAsync(this, "エラー", $"サンプルデータの読み込みに失敗しました: {ex.Message}", "OK");
		}

		logger.Info("Load Sample Button Clicked Processing Complete");
	}

	/// <summary>
	/// Test seam: tapped from UI tests via AutomationId "SelectTrain.TestSeedButton".
	/// Seeds the URL history with two well-known fixtures so the connection-history
	/// selection bug fix can be exercised without typing a long URI through Appium
	/// SendKeys (which is flaky on iOS XCUITest).
	/// </summary>
	void TestSeedButton_Clicked(object sender, EventArgs e)
	{
#if UI_TEST
		logger.Info("TestSeedButton clicked: seeding URL history fixtures");
		viewModel.SeedUrlHistoryForTesting(new[]
		{
			"https://example.com/timetable-a.json",
			"https://example.com/timetable-b.json",
		});
#endif
	}

	/// <summary>
	/// Test seam: tapped from UI tests via "SelectTrain.TestSeedNextTrainSelectionButton".
	/// Cascades selection to a sample-data train whose NextTrainId is non-empty
	/// (WorkGroup "hako-order-test" → Work "work-linear" → Train "linear-train-1",
	/// NextTrainId = "linear-train-2"). Used to verify the NextTrainButton displays
	/// without depending on platform-flaky CollectionView taps.
	/// </summary>
	void TestSeedNextTrainSelectionButton_Clicked(object sender, EventArgs e)
	{
#if UI_TEST
		logger.Info("TestSeedNextTrainSelection clicked: selecting a train with NextTrainId");
		try
		{
			var wg = viewModel.WorkGroupList?.FirstOrDefault(w => w.Id == "hako-order-test");
			if (wg is null)
			{
				logger.Warn("hako-order-test WorkGroup not found in sample data");
				return;
			}
			viewModel.SelectedWorkGroup = wg;
			// SelectedWork / SelectedTrainData cascade automatically; the first work
			// of "hako-order-test" is "work-linear" whose first train ("linear-train-1")
			// has NextTrainId = "linear-train-2".
		}
		catch (Exception ex)
		{
			logger.Error(ex, "TestSeedNextTrainSelection failed");
		}
#endif
	}

	/// <summary>
	/// Test seam: tapped from UI tests via "SelectTrain.TestSeedGpsButton". Force-
	/// enables LocationService and pushes a hard-coded GPS coord so the auto-scroll
	/// pipeline can be exercised without typing a deeplink through Appium SendKeys.
	/// The coord matches sample-data row 6 (大宮: 139.790, 35.700).
	/// </summary>
	void TestSeedGpsButton_Clicked(object sender, EventArgs e)
	{
#if UI_TEST
		logger.Info("TestSeedGpsButton clicked: pushing fixture GPS coord");
		try
		{
			var locationService = InstanceManager.LocationService;
			locationService.SetLonLatLocationService();
			// Do NOT toggle IsEnabled here. On iOS, IsEnabled = true fires
			// IsEnabledChanged which wakes up LocationServiceGpsAdapter; that
			// adapter calls Geolocation.Default.StartListening which prompts
			// the system CoreLocation permission alert and stalls the test.
			// Calling SetGpsLocation still fires OnGpsLocationUpdated (the
			// observable side effect tests care about) before the IsEnabled
			// gate would early-return.
			locationService.SetGpsLocation(longitude: 139.790, latitude: 35.700, accuracy: 10.0, useAverageDistance: false);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "TestSeedGpsButton failed");
		}
#endif
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

#if UI_TEST
		// Make the test seed buttons findable. UI_TEST is defined only by the
		// CI workflow (or a developer building with /p:DefineConstants=UI_TEST);
		// in a normal Debug or Release build this branch is removed and the
		// buttons stay IsVisible="False" → unreachable from Appium.
		if (TestSeedButton is not null)
			TestSeedButton.IsVisible = true;
		if (TestSeedGpsButton is not null)
			TestSeedGpsButton.IsVisible = true;
		if (TestSeedNextTrainSelectionButton is not null)
			TestSeedNextTrainSelectionButton.IsVisible = true;
#endif

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
					viewModel.Loader = await SampleDataLoader.CreateAsync();
					return;
				}

				// No default timetable found or error occurred
				logger.Info("No default timetable found or error occurred - setting SampleDataLoader as fallback");
				viewModel.Loader = await SampleDataLoader.CreateAsync();
			}
			catch (Exception ex)
			{
				logger.Error(ex, "Error loading default timetable");
				InstanceManager.CrashlyticsWrapper.Log(ex, "SelectTrainPage.OnAppearing (TryLoadDefaultTimetableAsync failed)");
				try
				{
					viewModel.Loader = await SampleDataLoader.CreateAsync();
				}
				catch (Exception fallbackEx)
				{
					logger.Error(fallbackEx, "Fallback SampleDataLoader.CreateAsync also failed");
					InstanceManager.CrashlyticsWrapper.Log(fallbackEx, "SelectTrainPage.OnAppearing (fallback SampleDataLoader.CreateAsync failed)");
				}
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
				viewModel.Loader = await SampleDataLoader.CreateAsync();
				return;
			}

			var fileNames = jsonFiles.Select(f => f.Name).ToArray();
			var filePaths = jsonFiles.Select(f => f.FullName).ToArray();

			string? selectedFileName = await DisplayActionSheetAsync(
				"どのファイルを開きますか？",
				"キャンセル",
				null,
				fileNames
			);

			if (string.IsNullOrEmpty(selectedFileName) || selectedFileName == "キャンセル")
			{
				logger.Info("File selection cancelled");
				viewModel.Loader = await SampleDataLoader.CreateAsync();
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
					await Util.DisplayAlertAsync(this, "エラー", "ファイルの読み込みに失敗しました", "OK");
					viewModel.Loader = await SampleDataLoader.CreateAsync();
				}
			}
			else
			{
				logger.Warn("Invalid file selection index");
				viewModel.Loader = await SampleDataLoader.CreateAsync();
			}
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Error in ShowFileSelectionDialogAsync");
			InstanceManager.CrashlyticsWrapper.Log(ex, "SelectTrainPage.ShowFileSelectionDialogAsync failed");
			await Util.DisplayAlertAsync(this, "エラー", $"ファイル選択に失敗しました: {ex.Message}", "OK");
			viewModel.Loader = await SampleDataLoader.CreateAsync();
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
			await Util.DisplayAlertAsync(this, "Open Popup Failed", ex.ToString(), "OK");
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
					await Util.DisplayAlertAsync(this, "Unknown File Type", "The selected file is not a supported file type.", "OK");
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
			await Util.DisplayAlertAsync(this, "Cannot Open File", ex.ToString(), "OK");
		}

		logger.Info("Select File Button Clicked Processing Complete");
	}
}
