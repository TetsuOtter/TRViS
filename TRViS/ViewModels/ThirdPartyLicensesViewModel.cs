using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using TRViS.Models;

namespace TRViS.ViewModels;

public record MyKeyValuePair(string Key, string Value);

public partial class ThirdPartyLicensesViewModel : ObservableObject
{
	[ObservableProperty]
	IReadOnlyList<LicenseData>? _LicenseDataArray;

	[ObservableProperty]
	LicenseData? _SelectedLicenseData;

	[ObservableProperty]
	List<MyKeyValuePair>? _LicenseTextList;

	[ObservableProperty]
	string _LicenseExpression = "";

	public const string licenseFileDir = "licenses";
	async partial void OnSelectedLicenseDataChanged(LicenseData? value)
	{
		List<MyKeyValuePair> list = new();

		if (value is null)
		{
			LicenseTextList = list;
			LicenseExpression = "";
			return;
		}


		if (value.licenseDataType == "expression")
		{
			LicenseExpression = value.license;

			foreach (string fileName in
				Regex.Split(value.license, @"\(|\)| ")
					.Where(v => !string.IsNullOrWhiteSpace(v) && v != "AND" && v != "OR")
			)
				await loadLicenseText(list, fileName);
		}
		else
		{
			LicenseExpression = "";
			await loadLicenseText(list, value.license);
		}

		LicenseTextList = list;
	}

	async static Task loadLicenseText(List<MyKeyValuePair> list, string fileName)
	{
		string path = Path.Combine(licenseFileDir, fileName);

		string text = "";
		if (await FileSystem.AppPackageFileExistsAsync(path) != true)
		{
			text = $"(Cannot Find File: {fileName})";
		}
		else
		{
			using Stream stream = await FileSystem.OpenAppPackageFileAsync(path);
			using StreamReader reader = new(stream);

			text = await reader.ReadToEndAsync();
		}

		list.Add(new(fileName, text));
	}
}
