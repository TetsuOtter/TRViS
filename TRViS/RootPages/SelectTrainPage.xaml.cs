using CommunityToolkit.Maui.Views;

using Microsoft.AppCenter.Crashes;

using TRViS.IO;
using TRViS.ViewModels;

namespace TRViS.RootPages;

public partial class SelectTrainPage : ContentPage
{
	public static readonly string NameOfThisClass = nameof(SelectTrainPage);
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
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
			await this.ShowPopupAsync(new SelectOnlineResourcePopup());
		}
		catch (Exception ex)
		{
			Crashes.TrackError(ex);
			logger.Error(ex, "ShowPopupAsync failed");
			await Utils.DisplayAlert(this, "Open Popup Failed", ex.ToString(), "OK");
		}

		logger.Info("Load From Web Button Clicked Processing Complete");
	}

	async void SelectDatabaseButton_Clicked(object sender, EventArgs e)
	{
		logger.Info("Select File Button Clicked");

		try
		{
			var result = await FilePicker.Default.PickAsync();

			if (result is not null)
			{
				logger.Info("File Selected: {0}", result.FullPath);
				ILoader? lastLoader = viewModel.Loader;

				if (result.FullPath.EndsWith(".json"))
				{
					logger.Debug("Loading JSON File");
					viewModel.Loader = await LoaderJson.InitFromFileAsync(result.FullPath);
					logger.Trace("LoaderJson Initialized");
				}
				else
				{
					logger.Debug("Loading SQLite File");
					viewModel.Loader = new LoaderSQL(result.FullPath);
					logger.Trace("LoaderSQL Initialized");
				}

				if (!ReferenceEquals(lastLoader, viewModel.Loader))
				{
					logger.Debug("Loader changed -> dispose lastLoader");
					// どちらもnullの可能性がある
					lastLoader?.Dispose();
					return;
				}
			}
			else
			{
				logger.Info("File Selection Canceled (PickAsync result is null))");
			}
		}
		catch (Exception ex)
		{
			logger.Error(ex, "File Selection Failed");
			await Utils.DisplayAlert(this, "Cannot Open File", ex.ToString(), "OK");
		}

		logger.Info("Select File Button Clicked Processing Complete");
	}
}
