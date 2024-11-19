using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using TRViS.Controls;
using TRViS.Models;
using TRViS.ViewModels;

namespace TRViS.RootPages;

public partial class ThirdPartyLicenses : ContentPage
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
	ThirdPartyLicensesViewModel viewModel { get; }
	public ThirdPartyLicenses()
	{
		logger.Trace("Creating");

		InitializeComponent();

		viewModel = new ThirdPartyLicensesViewModel();

		BindingContext = viewModel;

		viewModel.PropertyChanged += ViewModel_PropertyChanged;

		logger.Trace("Creating Task to Load License List");
		Task.Run(LoadLicenseList);

		logger.Trace("Created");
	}

	private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(ThirdPartyLicensesViewModel.LicenseTextList))
			return;

		logger.Debug("LicenseTextList Changed");

		VerticalStackLayout licenses = new();
		if (viewModel.LicenseTextList?.Count > 0)
		{
			logger.Debug("LicenseTextList Length: {0}", viewModel.LicenseTextList.Count);
			foreach (var v in viewModel.LicenseTextList)
			{
				licenses.Children.Add(new HtmlAutoDetectLabel()
				{
					Text = v.Value
				});
				licenses.Children.Add(new BoxView()
				{
					HeightRequest = 1,
					BackgroundColor = new(0x80, 0x80, 0x80)
				});
			}
		}
		else
		{
			logger.Warn("LicenseTextList is null or empty");
			// NULL or Length=0
			licenses.Children.Add(new Label()
			{
				Text = "(No License Info)"
			});
		}

		LicenseTextArea.Content = licenses;
	}

	async void LoadLicenseList()
	{
		logger.Info("Loading License List");
		var list =
			(await LoadLicenseList("license_list.json"))
			.Concat(await LoadLicenseList("license_list_custom.json"))
			.ToList();

		list.Sort(static (v1, v2) => string.Compare(v1.id, v2.id));

		viewModel.LicenseDataArray = list;
		logger.Info("License List Loaded");
	}

	[JsonSourceGenerationOptions]
	[JsonSerializable(typeof(LicenseData[]))]
	internal partial class LicenseDataArrayJsonSourceGenerationContext : JsonSerializerContext { }

	static async Task<LicenseData[]> LoadLicenseList(string fileName)
	{
		logger.Info("Loading License List from {0}", fileName);
		LicenseData[]? result = null;

		string path = Path.Combine(ThirdPartyLicensesViewModel.licenseFileDir, fileName);
		logger.Debug("License List Path: {0}", path);
		if (await FileSystem.AppPackageFileExistsAsync(path))
		{
			using Stream stream = await FileSystem.OpenAppPackageFileAsync(path);
			result = await JsonSerializer.DeserializeAsync<LicenseData[]>(stream, LicenseDataArrayJsonSourceGenerationContext.Default.LicenseDataArray);
			logger.Debug("License List Loaded from App Package (Length: {0})", result?.Length ?? 0);
		}

		if (result is null)
		{
			logger.Warn("License List Not Found in App Package");
			result = Array.Empty<LicenseData>();
		}

		logger.Info("License List Loaded from {0}", fileName);
		return result;
	}
}
