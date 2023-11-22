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

	async void LoadFromWebButton_Clicked(object sender, EventArgs e)
	{
		logger.Info("Load From Web Button Clicked");

		try
		{
			string? url = await DisplayPromptAsync("Load From Web", "ファイルへのリンクを入力してください", "OK", "Cancel", "https://");

			if (string.IsNullOrEmpty(url))
			{
				logger.Info("URL is null or empty");
				return;
			}

			await viewModel.LoadExternalFileAsync(url, AppLinkType.Unknown, CancellationToken.None);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Load From Web Failed");
			await Utils.DisplayAlert(this, "Cannot Load from Web", ex.ToString(), "OK");
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
				viewModel.Loader?.Dispose();

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
