using System.Text.Json;
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

		Task.Run(LoadLicenseList);
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
