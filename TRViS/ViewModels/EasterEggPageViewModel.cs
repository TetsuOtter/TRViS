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

	public EasterEggPageViewModel()
	{
		logger.Trace("EasterEggPageViewModel Creating (with Task.Run)");

		Task.Run(LoadFromFileAsync);
	}

	public async Task LoadFromFileAsync()
	{
		logger.Info("Loading SettingFileStructure from default setting file");
		(SettingFileStructure settingFile, string? msg) = await SettingFileStructure.LoadFromJsonFileOrCreateAsync();

		await InitAsync(settingFile, msg == SettingFileStructure.settingFileCreatedMsg);
	}

	public async Task LoadFromFileAsync(string path)
	{
		(SettingFileStructure settingFile, string? msg) tmp;

		logger.Info("Loading SettingFileStructure from setting file (path: {0})", path);
		using (FileStream stream = File.OpenRead(path))
		{
			tmp = await SettingFileStructure.LoadFromJsonAsync(stream);
		}

		await InitAsync(tmp.settingFile, tmp.msg == SettingFileStructure.settingFileCreatedMsg);
	}

	async Task InitAsync(SettingFileStructure settingFile, bool isNewlyCreated)
	{
		logger.Debug("InitAsync (setting: {0}, isNewlyCreated: {1})", settingFile, isNewlyCreated);
		if (isNewlyCreated && Preferences.Default.ContainsKey(nameof(ShellBackgroundColor)))
		{
			int value = Preferences.Default.Get<int>(nameof(ShellBackgroundColor), 0);

			logger.Info("Setting title color stored in AppPreference (value: {0:X6})", value);
			settingFile.TitleColor = new(Color.FromInt(value));
			await settingFile.SaveToJsonFileAsync();
		}

		ShellBackgroundColor = settingFile.TitleColor.ToColor();
		Color_Red = settingFile.TitleColor.Red;
		Color_Green = settingFile.TitleColor.Green;
		Color_Blue = settingFile.TitleColor.Blue;

		SetTitleTextColor();
		logger.Trace("InitAsync Completed");
	}

	public async Task SaveAsync()
	{
		logger.Info("Saving setting to file...");
		SettingFileStructure settingFile = new()
		{
			TitleColor = new(ShellBackgroundColor),
		};

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

