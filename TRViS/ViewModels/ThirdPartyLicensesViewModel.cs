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

	string _lastLicense = "";

	public const string licenseFileDir = "licenses";
	async partial void OnSelectedLicenseDataChanged(LicenseData? value)
	{
		if (_lastLicense == value?.license)
			return;
		LicenseTextList = null;
		_lastLicense = "";

		if (value is null)
		{
			LicenseExpression = "";
			return;
		}

		try
		{
			List<MyKeyValuePair> list = new();
			if (value.licenseDataType == "expression")
			{
				LicenseExpression = value.license;

				foreach (string fileName in
					Regex.Split(value.license, @"\(|\)| ")
						.Where(static v => !string.IsNullOrWhiteSpace(v) && v != "AND" && v != "OR")
				)
				{
					list.Add(await LoadLicenseText(value.license));
				}
			}
			else
			{
				LicenseExpression = "";
				list.Add(await LoadLicenseText(value.license));
			}

			_lastLicense = value.license;
			LicenseTextList = list;
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex);
			await Shell.Current.DisplayAlertAsync("Cannot load License Info", $"{value.id}に関するライセンス情報の読み込みに失敗しました。\n(license: {value.license})\n{ex.Message}", "OK");
		}
	}

	async static Task<MyKeyValuePair> LoadLicenseText(string fileName)
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

		return new(fileName, text);
	}
}
