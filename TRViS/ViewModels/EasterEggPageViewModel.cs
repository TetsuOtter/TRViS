using CommunityToolkit.Mvvm.ComponentModel;
using TRViS.MyAppCustomizables;

namespace TRViS.ViewModels;

public partial class EasterEggPageViewModel : ObservableObject
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

	[ObservableProperty]
	Color _ShellBackgroundColor = Colors.Black;

	[ObservableProperty]
	Color _ShellTitleTextColor = Colors.White;

	[ObservableProperty]
	int _Color_Red;
	[ObservableProperty]
	int _Color_Green;
	[ObservableProperty]
	int _Color_Blue;

	[ObservableProperty]
	double _LocationServiceInterval_Seconds = 1;

	public IReadOnlyList<double> LocationServiceIntervalItems { get; } = new List<double>()
	{
		0.1,
		0.2,
		0.25,
		0.5,
		1,
		2,
		3,
		4,
		5,
		10,
		30,
		60,
	};

	[ObservableProperty]
	string _LocationServiceIntervalSettingHeaderLabel = "";
	partial void OnLocationServiceInterval_SecondsChanged(double value)
	{
		logger.Debug("OnLocationServiceInterval_SecondsChanged (value: {0})", value);
		LocationServiceIntervalSettingHeaderLabel = $"Location Service Interval: {value:F2} [s]";
	}

    public DTACMarkerViewModel MarkerViewModel { get; }

	public EasterEggPageViewModel()
	{
		logger.Trace("EasterEggPageViewModel Creating (with Task.Run)");

		MarkerViewModel = InstanceManager.DTACMarkerViewModel;

		Task.Run(LoadFromFileAsync);
	}

	public async Task LoadFromFileAsync()
	{
		logger.Info("Loading SettingFileStructure from default setting file");
		(SettingFileStructure settingFile, string? msg) = await SettingFileStructure.LoadFromJsonFileOrCreateAsync();

		await InitAsync(settingFile, msg);
	}

	public async Task LoadFromFileAsync(string path)
	{
		(SettingFileStructure settingFile, string? msg) tmp;

		logger.Info("Loading SettingFileStructure from setting file (path: {0})", path);
		using (FileStream stream = File.OpenRead(path))
		{
			tmp = await SettingFileStructure.LoadFromJsonAsync(stream);
		}

		await InitAsync(tmp.settingFile, tmp.msg);
	}

	async Task InitAsync(SettingFileStructure settingFile, string? errorMsg)
	{
		bool isNewlyCreated = errorMsg == SettingFileStructure.settingFileCreatedMsg;
		logger.Debug("InitAsync (setting: {0}, isNewlyCreated: {1})", settingFile, isNewlyCreated);
		if (isNewlyCreated && Preferences.Default.ContainsKey(nameof(ShellBackgroundColor)))
		{
			int value = Preferences.Default.Get<int>(nameof(ShellBackgroundColor), 0);

			logger.Info("Setting title color stored in AppPreference (value: {0:X6})", value);
			settingFile.TitleColor = new(Color.FromInt(value));
			MarkerViewModel?.SetToSettings(settingFile);
			await settingFile.SaveToJsonFileAsync();
		}

		if (!isNewlyCreated && errorMsg is not null)
		{
			await Shell.Current.DisplayAlert(
				"Failed to load setting file",
				errorMsg,
				"OK"
			);
			// 読み込み自体に失敗しているため、設定の反映は行わない
			return;
		}

		if (settingFile.LocationServiceInterval_Seconds < SettingFileStructure.MinimumLocationServiceIntervalValue)
		{
			logger.Warn("Setting LocationServiceInterval({0}) to default value (value: {1})", settingFile.LocationServiceInterval_Seconds, SettingFileStructure.MinimumLocationServiceIntervalValue);

			// ここでは上書き保存は行わない。警告を出すのみに留める。
			await Shell.Current.DisplayAlert(
				"Invalid LocationServiceInterval Value",
				$"value({settingFile.LocationServiceInterval_Seconds}) must be same or more than {SettingFileStructure.MinimumLocationServiceIntervalValue}",
				"OK"
			);

			settingFile.LocationServiceInterval_Seconds = SettingFileStructure.MinimumLocationServiceIntervalValue;
		}

		ShellBackgroundColor = settingFile.TitleColor.ToColor();
		Color_Red = settingFile.TitleColor.Red;
		Color_Green = settingFile.TitleColor.Green;
		Color_Blue = settingFile.TitleColor.Blue;
		LocationServiceInterval_Seconds = settingFile.LocationServiceInterval_Seconds;

		MarkerViewModel?.UpdateList(settingFile);

		SetTitleTextColor();

		if (settingFile.InitialTheme is AppTheme theme and not AppTheme.Unspecified)
		{
			InstanceManager.AppViewModel.CurrentAppTheme = theme;
			if (Application.Current is not null)
			{
				MainThread.BeginInvokeOnMainThread(() =>
				{
					Application.Current.UserAppTheme = theme;
				});
			}
		}

		logger.Trace("InitAsync Completed");
	}

	public async Task SaveAsync()
	{
		logger.Info("Saving setting to file...");
		SettingFileStructure settingFile = new()
		{
			TitleColor = new(ShellBackgroundColor),
			LocationServiceInterval_Seconds = LocationServiceInterval_Seconds
		};

		MarkerViewModel?.SetToSettings(settingFile);

		await settingFile.SaveToJsonFileAsync();
	}

	partial void OnShellBackgroundColorChanged(Color value)
	{
		if (value is not null)
		{
			SetTitleTextColor();
		}
	}

	void SetTitleTextColor()
	{
		// ref: http://www.asahi-net.or.jp/~gx4s-kmgi/page04.html
		ShellTitleTextColor = Utils.GetTextColorFromBGColor(Color_Red, Color_Green, Color_Blue);
	}

	partial void OnColor_RedChanged(int value)
		=> ShellBackgroundColor = Color.FromRgb(Color_Red, Color_Green, Color_Blue);
	partial void OnColor_GreenChanged(int value)
		=> ShellBackgroundColor = Color.FromRgb(Color_Red, Color_Green, Color_Blue);
	partial void OnColor_BlueChanged(int value)
		=> ShellBackgroundColor = Color.FromRgb(Color_Red, Color_Green, Color_Blue);
}

