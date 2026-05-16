using TRViS.Localization;
using TRViS.MyAppCustomizables;
using TRViS.Services;
using TRViS.Utils;
using TRViS.ViewModels;
using TRViS.LocationService.Abstractions;

namespace TRViS.RootPages;

public partial class EasterEggPage : ContentPage
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	EasterEggPageViewModel ViewModel { get; }

	public EasterEggPage()
	{
		logger.Trace("EasterEggPage Creating");

		InitializeComponent();

		ViewModel = InstanceManager.EasterEggPageViewModel;
		BindingContext = ViewModel;

		LogFilePathLabel.Text = DirectoryPathProvider.GeneralLogFileDirectory.FullName;

		// Picker item text is localized, so the items are populated from code
		// (not static <Picker.Items> in XAML) and rebuilt whenever the UI
		// language changes. Selection is restored from the ViewModel afterwards.
		RebuildLocalizedPickers();
		LocalizationResourceManager.Current.CultureChanged += (_, _) =>
			MainThread.BeginInvokeOnMainThread(RebuildLocalizedPickers);

		// Update picker when ViewModel's SelectedAppTheme changes
		ViewModel.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(EasterEggPageViewModel.SelectedAppTheme))
			{
				UpdateAppThemePickerSelection();
			}
			else if (e.PropertyName == nameof(EasterEggPageViewModel.TimeProgressionRate))
			{
				UpdateTimeProgressionRatePickerSelection();
			}
			else if (e.PropertyName == nameof(EasterEggPageViewModel.HorizontalTimetableButtonLabel))
			{
				UpdateHorizontalTimetableButtonLabelPickerSelection();
			}
			else if (e.PropertyName == nameof(EasterEggPageViewModel.PdfJsRenderEngine))
			{
				UpdatePdfJsRenderEnginePickerSelection();
			}
			else if (e.PropertyName == nameof(EasterEggPageViewModel.SelectedAppLanguage))
			{
				UpdateLanguagePickerSelection();
			}
		};

#if IOS || MACCATALYST
		ShowMapWhenLandscapeHeaderLabel.IsVisible = DeviceInfo.Idiom != DeviceIdiom.Phone;
#else
		ShowMapWhenLandscapeHeaderLabel.IsVisible = false;
#endif

		// KeepScreenOnWhenRunning is only for phones and tablets
		KeepScreenOnWhenRunningHeaderLabel.IsVisible = DeviceInfo.Idiom == DeviceIdiom.Phone || DeviceInfo.Idiom == DeviceIdiom.Tablet;

		logger.Trace("EasterEggPage Created");
	}

	private void OnLoadFromPickerClicked(object sender, EventArgs e)
	{
		logger.Warn("Not Implemented");
	}
	private void OnSaveToPickerClicked(object sender, EventArgs e)
	{
		logger.Trace("Not Implemented");
	}

	private async void OnReloadSavedClicked(object sender, EventArgs e)
	{
		try
		{
			logger.Trace("Executing...");

			await ViewModel.LoadFromFileAsync();

			logger.Info("Reload Complete");
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Failed to reload");
			await DisplayAlertAsync(AppResources.Common_Error, string.Format(AppResources.Settings_AlertReloadFailedFormat, ex.Message), AppResources.Common_OK);
		}
	}

	private async void OnSaveClicked(object sender, EventArgs e)
	{
		try
		{
			logger.Trace("Executing...");

			await ViewModel.SaveAsync();

			logger.Info("Saved");
			await DisplayAlertAsync(AppResources.Common_Success, AppResources.Settings_AlertSavedSuccess, AppResources.Common_OK);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Failed to save");
			await DisplayAlertAsync(AppResources.Common_Error, string.Format(AppResources.Settings_AlertSaveFailedFormat, ex.Message), AppResources.Common_OK);
		}
	}

	private bool _isUpdatingAppThemePicker = false;

	private void OnAppThemePickerSelectedIndexChanged(object sender, EventArgs e)
	{
		if (_isUpdatingAppThemePicker)
			return;

		if (sender is not Picker picker)
			return;

		AppTheme newTheme = picker.SelectedIndex switch
		{
			0 => AppTheme.Unspecified,
			1 => AppTheme.Light,
			2 => AppTheme.Dark,
			_ => AppTheme.Unspecified
		};

		logger.Info("AppTheme changed to {0}", newTheme);
		ViewModel.SelectedAppTheme = newTheme;
	}

	private void UpdateAppThemePickerSelection()
	{
		_isUpdatingAppThemePicker = true;
		try
		{
			AppThemePicker.SelectedIndex = ViewModel.SelectedAppTheme switch
			{
				AppTheme.Unspecified => 0,
				AppTheme.Light => 1,
				AppTheme.Dark => 2,
				_ => 0
			};
		}
		finally
		{
			_isUpdatingAppThemePicker = false;
		}
	}

	private bool _isUpdatingTimeProgressionRatePicker = false;

	private void OnTimeProgressionRatePickerSelectedIndexChanged(object sender, EventArgs e)
	{
		if (_isUpdatingTimeProgressionRatePicker)
			return;

		if (sender is not Picker picker)
			return;

		TimeProgressionRate newRate = picker.SelectedIndex switch
		{
			0 => TimeProgressionRate.Normal,
			1 => TimeProgressionRate.X30,
			2 => TimeProgressionRate.X60,
			_ => TimeProgressionRate.Normal
		};

		logger.Info("TimeProgressionRate changed to {0}", newRate);
		ViewModel.TimeProgressionRate = newRate;
	}

	private void UpdateTimeProgressionRatePickerSelection()
	{
		_isUpdatingTimeProgressionRatePicker = true;
		try
		{
			TimeProgressionRatePicker.SelectedIndex = ViewModel.TimeProgressionRate switch
			{
				TimeProgressionRate.Normal => 0,
				TimeProgressionRate.X30 => 1,
				TimeProgressionRate.X60 => 2,
				_ => 0
			};
		}
		finally
		{
			_isUpdatingTimeProgressionRatePicker = false;
		}
	}

	private bool _isUpdatingHorizontalTimetableButtonLabelPicker = false;

	private void OnHorizontalTimetableButtonLabelPickerSelectedIndexChanged(object sender, EventArgs e)
	{
		if (_isUpdatingHorizontalTimetableButtonLabelPicker)
			return;

		if (sender is not Picker picker)
			return;

		HorizontalTimetableButtonLabel newLabel = picker.SelectedIndex switch
		{
			0 => HorizontalTimetableButtonLabel.Horizontal,
			1 => HorizontalTimetableButtonLabel.Train,
			2 => HorizontalTimetableButtonLabel.ETrain,
			_ => HorizontalTimetableButtonLabel.Train
		};

		logger.Info("HorizontalTimetableButtonLabel changed to {0}", newLabel);
		ViewModel.HorizontalTimetableButtonLabel = newLabel;
	}

	private void UpdateHorizontalTimetableButtonLabelPickerSelection()
	{
		_isUpdatingHorizontalTimetableButtonLabelPicker = true;
		try
		{
			HorizontalTimetableButtonLabelPicker.SelectedIndex = ViewModel.HorizontalTimetableButtonLabel switch
			{
				HorizontalTimetableButtonLabel.Horizontal => 0,
				HorizontalTimetableButtonLabel.Train => 1,
				HorizontalTimetableButtonLabel.ETrain => 2,
				_ => 1
			};
		}
		finally
		{
			_isUpdatingHorizontalTimetableButtonLabelPicker = false;
		}
	}

	// ----- Language picker -----

	private bool _isUpdatingLanguagePicker = false;

	private void OnLanguagePickerSelectedIndexChanged(object sender, EventArgs e)
	{
		if (_isUpdatingLanguagePicker)
			return;

		if (sender is not Picker picker)
			return;

		AppLanguage newLanguage = picker.SelectedIndex switch
		{
			0 => AppLanguage.System,
			1 => AppLanguage.Japanese,
			2 => AppLanguage.English,
			_ => AppLanguage.System
		};

		logger.Info("AppLanguage changed to {0}", newLanguage);
		ViewModel.SelectedAppLanguage = newLanguage;
	}

	private void UpdateLanguagePickerSelection()
	{
		_isUpdatingLanguagePicker = true;
		try
		{
			LanguagePicker.SelectedIndex = ViewModel.SelectedAppLanguage switch
			{
				AppLanguage.System => 0,
				AppLanguage.Japanese => 1,
				AppLanguage.English => 2,
				_ => 0
			};
		}
		finally
		{
			_isUpdatingLanguagePicker = false;
		}
	}

	/// <summary>
	/// 言語依存の Picker (テーマ / 時間進行 / 横型時刻表ラベル / 言語) の
	/// 表示項目を現在の言語で再構築し、選択状態を復元する。起動時と
	/// 言語変更時に呼ばれる。PDF エンジン Picker も表示名がローカライズ
	/// されるため併せて再構築する。
	/// </summary>
	private void RebuildLocalizedPickers()
	{
		_isUpdatingAppThemePicker = true;
		_isUpdatingTimeProgressionRatePicker = true;
		_isUpdatingHorizontalTimetableButtonLabelPicker = true;
		_isUpdatingLanguagePicker = true;
		try
		{
			AppThemePicker.Items.Clear();
			AppThemePicker.Items.Add(AppResources.Settings_Theme_System);
			AppThemePicker.Items.Add(AppResources.Settings_Theme_Light);
			AppThemePicker.Items.Add(AppResources.Settings_Theme_Dark);

			TimeProgressionRatePicker.Items.Clear();
			TimeProgressionRatePicker.Items.Add(AppResources.Settings_TimeProgression_1x);
			TimeProgressionRatePicker.Items.Add(AppResources.Settings_TimeProgression_30x);
			TimeProgressionRatePicker.Items.Add(AppResources.Settings_TimeProgression_60x);

			HorizontalTimetableButtonLabelPicker.Items.Clear();
			HorizontalTimetableButtonLabelPicker.Items.Add(AppResources.Settings_HTBL_Horizontal);
			HorizontalTimetableButtonLabelPicker.Items.Add(AppResources.Settings_HTBL_Train);
			HorizontalTimetableButtonLabelPicker.Items.Add(AppResources.Settings_HTBL_ETrain);

			LanguagePicker.Items.Clear();
			LanguagePicker.Items.Add(AppResources.Settings_Language_System);
			LanguagePicker.Items.Add(AppResources.Settings_Language_Japanese);
			LanguagePicker.Items.Add(AppResources.Settings_Language_English);
		}
		finally
		{
			_isUpdatingAppThemePicker = false;
			_isUpdatingTimeProgressionRatePicker = false;
			_isUpdatingHorizontalTimetableButtonLabelPicker = false;
			_isUpdatingLanguagePicker = false;
		}

		UpdateAppThemePickerSelection();
		UpdateTimeProgressionRatePickerSelection();
		UpdateHorizontalTimetableButtonLabelPickerSelection();
		UpdateLanguagePickerSelection();

		// PDF engine options depend on the device; its display names are localized.
		PopulatePdfJsRenderEnginePicker();
		UpdatePdfJsRenderEnginePickerSelection();
	}

	// v3 は Safari 13+ (nullish coalescing) が必要なため iOS 13 以降で提供する。
	// v5 (pdf.js 公式 legacy ビルド) の対応下限は Safari 16.4 (= iOS 16.4) のため
	// iOS 16.4 以降でのみ提供する。iOS 12 系は v2 系のみ。
	// iOS 以外 (Android/Windows/macCatalyst) は近代 WebView のため全て扱える。
	private static bool SupportsV3()
		=> !OperatingSystem.IsIOS() || OperatingSystem.IsIOSVersionAtLeast(13);

	private static bool SupportsV5()
		=> !OperatingSystem.IsIOS() || OperatingSystem.IsIOSVersionAtLeast(16, 4);

	private static IReadOnlyList<PdfJsRenderEngine> PdfJsRenderEngineOptions()
	{
		if (!SupportsV3())
			return new[] { PdfJsRenderEngine.V2Svg, PdfJsRenderEngine.V2Canvas };
		if (!SupportsV5())
			return new[] { PdfJsRenderEngine.V3Svg, PdfJsRenderEngine.V3Canvas };
		return new[] { PdfJsRenderEngine.V3Svg, PdfJsRenderEngine.V3Canvas, PdfJsRenderEngine.V5Canvas };
	}

	private static string PdfJsRenderEngineDisplayName(PdfJsRenderEngine engine)
	{
		// "pdf.js v2" のバージョン部分は固有名なので翻訳しない。描画方式
		// (SVG / canvas) のみローカライズする。
		(string version, string mode) = engine switch
		{
			PdfJsRenderEngine.V2Svg => ("v2", AppResources.Settings_PdfRender_Svg),
			PdfJsRenderEngine.V2Canvas => ("v2", AppResources.Settings_PdfRender_Canvas),
			PdfJsRenderEngine.V3Svg => ("v3", AppResources.Settings_PdfRender_Svg),
			PdfJsRenderEngine.V3Canvas => ("v3", AppResources.Settings_PdfRender_Canvas),
			PdfJsRenderEngine.V5Canvas => ("v5", AppResources.Settings_PdfRender_Canvas),
			_ => (engine.ToString(), string.Empty)
		};
		return string.Format(AppResources.Settings_PdfEngineDisplayFormat, version, mode);
	}

	private IReadOnlyList<PdfJsRenderEngine> _pdfJsRenderEngineOptions = [];
	private bool _isUpdatingPdfJsRenderEnginePicker = false;

	private void PopulatePdfJsRenderEnginePicker()
	{
		_pdfJsRenderEngineOptions = PdfJsRenderEngineOptions();

		_isUpdatingPdfJsRenderEnginePicker = true;
		try
		{
			PdfJsRenderEnginePicker.Items.Clear();
			foreach (PdfJsRenderEngine engine in _pdfJsRenderEngineOptions)
				PdfJsRenderEnginePicker.Items.Add(PdfJsRenderEngineDisplayName(engine));
		}
		finally
		{
			_isUpdatingPdfJsRenderEnginePicker = false;
		}
	}

	private void OnPdfJsRenderEnginePickerSelectedIndexChanged(object sender, EventArgs e)
	{
		if (_isUpdatingPdfJsRenderEnginePicker)
			return;

		if (sender is not Picker picker)
			return;

		int index = picker.SelectedIndex;
		if (index < 0 || index >= _pdfJsRenderEngineOptions.Count)
			return;

		PdfJsRenderEngine newEngine = _pdfJsRenderEngineOptions[index];
		logger.Info("PdfJsRenderEngine changed to {0}", newEngine);
		ViewModel.PdfJsRenderEngine = newEngine;
	}

	private void UpdatePdfJsRenderEnginePickerSelection()
	{
		_isUpdatingPdfJsRenderEnginePicker = true;
		try
		{
			PdfJsRenderEngine current = ViewModel.PdfJsRenderEngine;

			int index = -1;
			for (int i = 0; i < _pdfJsRenderEngineOptions.Count; i++)
			{
				if (_pdfJsRenderEngineOptions[i] == current)
				{
					index = i;
					break;
				}
			}

			// 保存値がこの端末の選択肢に無い場合 (例: 既定値 V2Svg を iOS 13+ で表示、
			// あるいは新しい端末で設定した値が古い端末に同期された場合)。
			// Picker は未選択にし、現在の実効エンジンはラベルで明示する。
			PdfJsRenderEnginePicker.SelectedIndex = index;

			PdfJsRenderEngineStatusLabel.Text = index >= 0
				? string.Format(AppResources.Settings_PdfEngineCurrentFormat, PdfJsRenderEngineDisplayName(current))
				: string.Format(AppResources.Settings_PdfEngineCurrentFallbackFormat, PdfJsRenderEngineDisplayName(current));
		}
		finally
		{
			_isUpdatingPdfJsRenderEnginePicker = false;
		}
	}
}
