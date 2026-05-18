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

#if UI_TEST
		// UI_TEST-only determinism seam. The real log directory is an absolute
		// path that embeds the iOS simulator's Device UUID *and* the app's
		// per-install GUID, both of which differ on every CI runner — rendering
		// this label would make the Settings screenshot baseline impossible to
		// pixel-gate. Substituting a fixed placeholder keeps the entire Settings
		// screen deterministic (no screenshot mask needed). Production builds
		// compile this out entirely and show the real path.
		LogFilePathLabel.Text = "/UITEST/TRViS.InternalFiles/logs";
#else
		LogFilePathLabel.Text = DirectoryPathProvider.GeneralLogFileDirectory.FullName;
#endif

		// Initialize AppThemePicker selection based on ViewModel
		UpdateAppThemePickerSelection();

		// Initialize TimeProgressionRatePicker selection based on ViewModel
		UpdateTimeProgressionRatePickerSelection();

		// Initialize HorizontalTimetableButtonLabelPicker selection based on ViewModel
		UpdateHorizontalTimetableButtonLabelPickerSelection();

		// Populate the PDF render engine picker with the options available on this
		// device (iOS < 13 → v2 only; iOS 13–16.3 → v3; iOS 16.4+ → v3 + v5),
		// then sync the selection.
		PopulatePdfJsRenderEnginePicker();
		UpdatePdfJsRenderEnginePickerSelection();

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
			await DisplayAlertAsync("Error", "Failed to reload\n" + ex.Message, "OK");
		}
	}

	private async void OnSaveClicked(object sender, EventArgs e)
	{
		try
		{
			logger.Trace("Executing...");

			await ViewModel.SaveAsync();

			logger.Info("Saved");
			await DisplayAlertAsync("Success!", "Successfully saved", "OK");
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Failed to save");
			await DisplayAlertAsync("Error", "Failed to save\n" + ex.Message, "OK");
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
		=> engine switch
		{
			PdfJsRenderEngine.V2Svg => "pdf.js v2 (SVG描画)",
			PdfJsRenderEngine.V2Canvas => "pdf.js v2 (canvas描画)",
			PdfJsRenderEngine.V3Svg => "pdf.js v3 (SVG描画)",
			PdfJsRenderEngine.V3Canvas => "pdf.js v3 (canvas描画)",
			PdfJsRenderEngine.V5Canvas => "pdf.js v5 (canvas描画)",
			_ => engine.ToString()
		};

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
				? $"現在の描画エンジン: {PdfJsRenderEngineDisplayName(current)}"
				: $"現在の描画エンジン: {PdfJsRenderEngineDisplayName(current)} (変更するには上のリストから選択してください)";
		}
		finally
		{
			_isUpdatingPdfJsRenderEnginePicker = false;
		}
	}
}
