using CommunityToolkit.Mvvm.ComponentModel;

using Microsoft.Maui.Controls;

using TRViS.Localization;
using TRViS.MyAppCustomizables;
using TRViS.Services;
using TRViS.Utils;
using TRViS.LocationService.Abstractions;

namespace TRViS.ViewModels;

public partial class EasterEggPageViewModel : ObservableObject
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	Color _ShellBackgroundColor = Colors.Black;
	public Color ShellBackgroundColor
	{
		// コード生成するとCompiled Bindingが上手く働かないため、手書き。
		get => _ShellBackgroundColor;
		set
		{
			if (SetProperty(ref _ShellBackgroundColor, value))
			{
				SetTitleTextColor();
			}
		}
	}

	Color _ShellTitleTextColor = Colors.White;
	public Color ShellTitleTextColor
	{
		// コード生成するとCompiled Bindingが上手く働かないため、手書き。
		get => _ShellTitleTextColor;
		set => SetProperty(ref _ShellTitleTextColor, value);
	}

	[ObservableProperty]
	public partial int Color_Red { get; set; }
	[ObservableProperty]
	public partial int Color_Green { get; set; }
	[ObservableProperty]
	public partial int Color_Blue { get; set; }

	[ObservableProperty]
	public partial double LocationServiceInterval_Seconds { get; set; } = 1;

	[ObservableProperty]
	public partial bool ShowMapWhenLandscape { get; set; } = false;

	[ObservableProperty]
	public partial bool KeepScreenOnWhenRunning { get; set; } = false;

	[ObservableProperty]
	public partial HorizontalTimetableButtonLabel HorizontalTimetableButtonLabel { get; set; } = HorizontalTimetableButtonLabel.Train;

	[ObservableProperty]
	public partial PdfJsRenderEngine PdfJsRenderEngine { get; set; } = PdfJsRenderEngine.V2Svg;

	[ObservableProperty]
	public partial AppTheme SelectedAppTheme { get; set; } = AppTheme.Unspecified;

	[ObservableProperty]
	public partial TimeProgressionRate TimeProgressionRate { get; set; } = TimeProgressionRate.Normal;

	[ObservableProperty]
	public partial AppLanguage SelectedAppLanguage { get; set; } = AppLanguage.System;

	// Set while we mirror the OS language into SelectedAppLanguage at startup,
	// so OnSelectedAppLanguageChanged does not write that mirrored value back
	// to the OS (which would be a redundant no-op at best, churn at worst).
	bool _suppressLanguageWriteBack;

	partial void OnSelectedAppLanguageChanged(AppLanguage value)
	{
		logger.Info("OnSelectedAppLanguageChanged: {0}", value);
		// In-session UI updates immediately via resx (iOS won't re-localize
		// the native bundle mid-session).
		LocalizationResourceManager.Current.SetLanguage(value);
		// On iOS / Mac Catalyst, mirror the choice into the OS so the per-app
		// language entry in Settings stays in sync and next launch / native
		// dialogs match. No-op on Android / Windows.
		if (!_suppressLanguageWriteBack)
			AppLanguageOsBridge.WriteOsOverride(value);
	}

	partial void OnSelectedAppThemeChanged(AppTheme value)
	{
		logger.Info("OnSelectedAppThemeChanged: {0}", value);
		InstanceManager.AppViewModel.CurrentAppTheme = value;
		if (Application.Current is not null)
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				Application.Current.UserAppTheme = value;
			});
		}
	}

	partial void OnTimeProgressionRateChanged(TimeProgressionRate value)
	{
		logger.Info("OnTimeProgressionRateChanged: {0}", value);
		InstanceManager.TimeProvider.ProgressionRate = value;
	}

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
			await Shell.Current.DisplayAlertAsync(
				AppResources.Settings_AlertLoadSettingFailedTitle,
				errorMsg,
				AppResources.Common_OK
			);
			// 読み込み自体に失敗しているため、設定の反映は行わない
			return;
		}

		if (settingFile.LocationServiceInterval_Seconds < SettingFileStructure.MinimumLocationServiceIntervalValue)
		{
			logger.Warn("Setting LocationServiceInterval({0}) to default value (value: {1})", settingFile.LocationServiceInterval_Seconds, SettingFileStructure.MinimumLocationServiceIntervalValue);

			// ここでは上書き保存は行わない。警告を出すのみに留める。
			await Shell.Current.DisplayAlertAsync(
				AppResources.Settings_AlertInvalidLocationIntervalTitle,
				string.Format(AppResources.Settings_AlertInvalidLocationIntervalFormat, settingFile.LocationServiceInterval_Seconds, SettingFileStructure.MinimumLocationServiceIntervalValue),
				AppResources.Common_OK
			);

			settingFile.LocationServiceInterval_Seconds = SettingFileStructure.MinimumLocationServiceIntervalValue;
		}

		ShellBackgroundColor = settingFile.TitleColor.ToColor();
		Color_Red = settingFile.TitleColor.Red;
		Color_Green = settingFile.TitleColor.Green;
		Color_Blue = settingFile.TitleColor.Blue;
		LocationServiceInterval_Seconds = settingFile.LocationServiceInterval_Seconds;
		ShowMapWhenLandscape = settingFile.ShowMapWhenLandscape;
		KeepScreenOnWhenRunning = settingFile.KeepScreenOnWhenRunning;
		HorizontalTimetableButtonLabel = settingFile.HorizontalTimetableButtonLabel;
		PdfJsRenderEngine = settingFile.PdfJsRenderEngine;
		SelectedAppTheme = settingFile.InitialTheme ?? AppTheme.Unspecified;
		TimeProgressionRate = settingFile.TimeProgressionRate;
		if (AppLanguageOsBridge.IsSupported)
		{
			// iOS / Mac Catalyst: OS (AppleLanguages) が真実。Picker は OS の
			// 実効言語をミラー表示し、解決は OS に追従 (System) させる。
			// _suppressLanguageWriteBack: ここでの代入で
			// OnSelectedAppLanguageChanged が AppleLanguages を上書きするのを防ぐ
			// (起動時はミラーするだけで OS へ書き戻さない)。
			_suppressLanguageWriteBack = true;
			try
			{
				SelectedAppLanguage = AppLanguageOsBridge.GetEffectiveLanguage();
			}
			finally
			{
				_suppressLanguageWriteBack = false;
			}
			LocalizationResourceManager.Current.SetLanguage(AppLanguage.System);
		}
		else
		{
			SelectedAppLanguage = settingFile.AppLanguage;
			// SetLanguage を明示的に呼ぶ (OnSelectedAppLanguageChanged は値が
			// 変わらないと発火しないため、起動時の system 既定からの再適用を保証)。
			LocalizationResourceManager.Current.SetLanguage(settingFile.AppLanguage);
		}

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
			LocationServiceInterval_Seconds = LocationServiceInterval_Seconds,
			ShowMapWhenLandscape = ShowMapWhenLandscape,
			KeepScreenOnWhenRunning = KeepScreenOnWhenRunning,
			HorizontalTimetableButtonLabel = HorizontalTimetableButtonLabel,
			PdfJsRenderEngine = PdfJsRenderEngine,
			InitialTheme = SelectedAppTheme,
			TimeProgressionRate = TimeProgressionRate,
			AppLanguage = SelectedAppLanguage,
		};

		MarkerViewModel?.SetToSettings(settingFile);

		await settingFile.SaveToJsonFileAsync();
	}

	void SetTitleTextColor()
	{
		// ref: http://www.asahi-net.or.jp/~gx4s-kmgi/page04.html
		ShellTitleTextColor = Util.GetTextColorFromBGColor(Color_Red, Color_Green, Color_Blue);
	}

	partial void OnColor_RedChanged(int value)
		=> ShellBackgroundColor = Color.FromRgb(Color_Red, Color_Green, Color_Blue);
	partial void OnColor_GreenChanged(int value)
		=> ShellBackgroundColor = Color.FromRgb(Color_Red, Color_Green, Color_Blue);
	partial void OnColor_BlueChanged(int value)
		=> ShellBackgroundColor = Color.FromRgb(Color_Red, Color_Green, Color_Blue);
}

