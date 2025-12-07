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

	protected override void OnAppearing()
	{
		base.OnAppearing();

		if (viewModel.Loader is null)
		{
			logger.Info("Loader is null -> set SampleDataLoader");
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
