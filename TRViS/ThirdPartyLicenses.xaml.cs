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
		using Stream stream = await FileSystem.OpenAppPackageFileAsync(Path.Combine(ThirdPartyLicensesViewModel.licenseFileDir, "license_list.json"));
		using StreamReader reader = new(stream);

		viewModel.LicenseDataArray = await JsonSerializer.DeserializeAsync<LicenseData[]>(stream);
	}
}
