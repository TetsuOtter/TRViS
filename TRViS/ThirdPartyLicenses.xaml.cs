using System.ComponentModel;
using System.Text.Json;
using TRViS.Controls;
using TRViS.Models;
using TRViS.ViewModels;

namespace TRViS;

public partial class ThirdPartyLicenses : ContentPage
{
	ThirdPartyLicensesViewModel viewModel { get; }
	public ThirdPartyLicenses()
	{
		InitializeComponent();

		viewModel = new ThirdPartyLicensesViewModel();

		BindingContext = viewModel;

		viewModel.PropertyChanged += ViewModel_PropertyChanged;

		Task.Run(LoadLicenseList);
	}

	private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(ThirdPartyLicensesViewModel.LicenseTextList))
			return;

		VerticalStackLayout licenses = new();
		if (viewModel.LicenseTextList?.Count > 0)
		{
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
		var list =
			(await LoadLicenseList("license_list.json"))
			.Concat(await LoadLicenseList("license_list_custom.json"))
			.ToList();

		list.Sort((v1, v2) => string.Compare(v1.id, v2.id));

		viewModel.LicenseDataArray = list;
	}

	static async Task<LicenseData[]> LoadLicenseList(string fileName)
	{
		LicenseData[]? result = null;

		string path = Path.Combine(ThirdPartyLicensesViewModel.licenseFileDir, fileName);
		if (await FileSystem.AppPackageFileExistsAsync(path))
		{
			using Stream stream = await FileSystem.OpenAppPackageFileAsync(path);
			result = await JsonSerializer.DeserializeAsync<LicenseData[]>(stream);
		}

		result ??= Array.Empty<LicenseData>();

		return result;
	}
}
