using CommunityToolkit.Mvvm.ComponentModel;
using TRViS.Models;

namespace TRViS.ViewModels;

public partial class ThirdPartyLicensesViewModel : ObservableObject
{
	[ObservableProperty]
	IReadOnlyList<LicenseData>? _LicenseDataArray;

	[ObservableProperty]
	LicenseData? _SelectedLicenseData;

	[ObservableProperty]
	string _LicenseText = "";

	public const string licenseFileDir = "licenses";
	async partial void OnSelectedLicenseDataChanged(LicenseData? value)
	{
		if (value is null)
		{
			LicenseText = "";
			return;
		}

		string path = Path.Combine(licenseFileDir, value.license);

		if (await FileSystem.AppPackageFileExistsAsync(path) != true)
		{
			LicenseText = "(Cannot Find File)";
			return;
		}

		using Stream stream = await FileSystem.OpenAppPackageFileAsync(path);
		using StreamReader reader = new(stream);

		LicenseText = await reader.ReadToEndAsync();
	}
}
