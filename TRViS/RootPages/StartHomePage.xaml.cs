using TRViS.DTAC;
using TRViS.IO;
using TRViS.Services;
using TRViS.Utils;
using TRViS.ViewModels;

namespace TRViS.RootPages;

public partial class StartHomePage : ContentPage
{
	public static readonly string NameOfThisClass = nameof(StartHomePage);
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	AppViewModel viewModel { get; }

	// Mode tracks whether the body shows Start (no Loader) or Home (Loader present).
	enum PageMode { Start, Home }
	PageMode _currentMode = PageMode.Start;

	// Animation tunables. Header is centered slightly above middle in Start mode;
	// pinned to top in Home mode.
	// START_HEADER_CENTER_FRACTION places the *center* of the header at this fraction
	// of the page height, so the visual "centeredness" stays consistent regardless of
	// header size (icon bigger/smaller, accessibility font scale).
	const double START_HEADER_CENTER_FRACTION = 0.32; // 0.5 = middle. 0.32 = "slightly above middle".
	// AppHeader has AnchorY=0 so Scale shrinks downward, keeping the visual top at the same y.
	// HOME_HEADER_SCALE controls the visual size of the Home-mode header band; smaller = more
	// vertical space for the WorkGroup/Work selection lists below.
	const double HOME_HEADER_SCALE = 0.55;
	const uint TRANSITION_MS = 380;

	double ComputeStartHeaderTranslationY()
	{
		double pageHeight = Height > 0 ? Height : InstanceManager.AppViewModel.WindowHeight;
		double headerHeight = AppHeader.Height > 0 ? AppHeader.Height : 220;
		// AppHeader has VerticalOptions=Start, so its natural top sits at y=0; offset to
		// place its center at START_HEADER_CENTER_FRACTION of page height.
		return Math.Max(0, (pageHeight * START_HEADER_CENTER_FRACTION) - (headerHeight / 2));
	}

	public StartHomePage()
	{
		logger.Trace("Creating");

		InitializeComponent();

		viewModel = InstanceManager.AppViewModel;
		BindingContext = viewModel;

		// Apply initial header layout once we have a measured size.
		SizeChanged += OnSizeChangedFirstLayout;
		AppHeader.SizeChanged += (_, __) => UpdateHomeBodyTopSpacer();

		logger.Trace("Created");
	}

	void UpdateHomeBodyTopSpacer()
	{
		// Reserve enough vertical space in HomeBody for the scaled-down header.
		// Falls back to a sane default before AppHeader has been measured.
		double measured = AppHeader.Height > 0 ? AppHeader.Height : 220;
		HomeBodyTopSpacer.HeightRequest = (measured * HOME_HEADER_SCALE) + 12;
	}

	void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(AppViewModel.Loader))
		{
			logger.Debug("Loader changed -> evaluate page mode");
			_ = ApplyModeForCurrentLoaderAsync();
		}
	}

	void OnSizeChangedFirstLayout(object? sender, EventArgs e)
	{
		if (Width <= 0 || Height <= 0)
			return;

		// Apply initial state without animation. We do this on every size change in case the
		// window resizes (desktop) or device rotates — but only animate when the *mode* changes;
		// pure size changes get a snap-update.
		ApplyHeaderLayoutInstant(_currentMode);
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		// Subscribe here (not in ctor) so each appearance pairs with an OnDisappearing
		// unsubscribe — avoids accumulating handlers if Shell recreates the page.
		viewModel.PropertyChanged -= OnViewModelPropertyChanged;
		viewModel.PropertyChanged += OnViewModelPropertyChanged;

#if UI_TEST
		// Make the test seed buttons findable by Appium.
		if (TestSeedButton is not null)
			TestSeedButton.IsVisible = true;
		if (TestSeedGpsButton is not null)
			TestSeedGpsButton.IsVisible = true;
#endif

		UpdatePrivacyDependentControls();
		UpdateHomeBodyTopSpacer();

		// First-appearing default-timetable load (preserves SelectTrainPage behavior).
		// Only run when we don't yet have a Loader.
		if (viewModel.Loader is null)
		{
			logger.Info("Loader is null -> attempt default timetable load");

			try
			{
				(bool success, bool requiresFileSelection, string? selectedFilePath, string? errorMessage) =
					await viewModel.TryLoadDefaultTimetableAsync();

				if (success && !requiresFileSelection)
				{
					logger.Info("Default timetable loaded -> navigate to DTAC");
					await NavigateToDTACAsync();
					return;
				}

				if (success && requiresFileSelection)
				{
					logger.Info("Multiple JSON files found - showing default-file selection sheet");
					await ShowFileSelectionDialogAsync();
					return;
				}

				if (errorMessage == "PrivacyPolicyNotAccepted")
				{
					logger.Info("Privacy policy not accepted -> stay on Start screen until user opens dialog");
					// Do NOT auto-load sample data here. The user must open the privacy dialog first.
					await ApplyModeForCurrentLoaderAsync();
					return;
				}

				// No default file. Stay on Start screen — do not auto-load sample data.
				logger.Info("No default timetable found -> stay on Start screen");
			}
			catch (Exception ex)
			{
				logger.Error(ex, "Error loading default timetable");
				InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.OnAppearing (TryLoadDefaultTimetableAsync failed)");
			}
		}

		await ApplyModeForCurrentLoaderAsync();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		viewModel.PropertyChanged -= OnViewModelPropertyChanged;
	}

	void UpdatePrivacyDependentControls()
	{
		// The data-loading buttons are gated entirely on privacy acceptance — disabled
		// rather than just refusing on click, so the user can see at a glance that
		// acceptance unlocks them. The reconfirm banner doubles as both a status
		// indicator and a tappable affordance to open the dialog.
		bool accepted = InstanceManager.FirebaseSettingViewModel.IsPrivacyPolicyAccepted;
		PrivacyReconfirmBanner.IsVisible = !accepted;
		ConnectServerButton.IsEnabled = accepted;
		SelectFileButton.IsEnabled = accepted;
		LoadDemoButton.IsEnabled = accepted;
	}

	// ----- Mode / animation -----

	Task ApplyModeForCurrentLoaderAsync()
	{
		PageMode target = viewModel.Loader is null ? PageMode.Start : PageMode.Home;
		if (target == _currentMode)
		{
			// Refresh derived UI in case Loader was swapped to a new instance.
			UpdateLoaderInfoLabels();
			return Task.CompletedTask;
		}
		return TransitionToAsync(target);
	}

	void ApplyHeaderLayoutInstant(PageMode mode)
	{
		if (mode == PageMode.Start)
		{
			AppHeader.TranslationY = ComputeStartHeaderTranslationY();
			AppHeader.Scale = 1.0;
			StartBody.IsVisible = true;
			StartBody.Opacity = 1;
			HomeBody.IsVisible = false;
			HomeBody.Opacity = 0;
		}
		else
		{
			AppHeader.TranslationY = 0;
			AppHeader.Scale = HOME_HEADER_SCALE;
			StartBody.IsVisible = false;
			StartBody.Opacity = 0;
			HomeBody.IsVisible = true;
			HomeBody.Opacity = 1;
		}
		_currentMode = mode;
		UpdateLoaderInfoLabels();
	}

	async Task TransitionToAsync(PageMode target)
	{
		logger.Info("Transitioning page mode {0} -> {1}", _currentMode, target);

		double centeredOffset = ComputeStartHeaderTranslationY();

		double headerFromY, headerToY, headerFromScale, headerToScale;
		double startBodyFrom, startBodyTo, homeBodyFrom, homeBodyTo;

		if (target == PageMode.Home)
		{
			headerFromY = AppHeader.TranslationY;
			headerToY = 0;
			headerFromScale = AppHeader.Scale;
			headerToScale = HOME_HEADER_SCALE;
			startBodyFrom = StartBody.Opacity;
			startBodyTo = 0;
			homeBodyFrom = 0;
			homeBodyTo = 1;

			HomeBody.Opacity = 0;
			HomeBody.IsVisible = true;
		}
		else
		{
			headerFromY = AppHeader.TranslationY;
			headerToY = centeredOffset;
			headerFromScale = AppHeader.Scale;
			headerToScale = 1.0;
			startBodyFrom = StartBody.Opacity;
			startBodyTo = 1;
			homeBodyFrom = HomeBody.Opacity;
			homeBodyTo = 0;

			StartBody.Opacity = startBodyFrom;
			StartBody.IsVisible = true;
		}

		_currentMode = target;
		UpdateLoaderInfoLabels();

		var animation = new Animation
		{
			{ 0, 1, new Animation(v => AppHeader.TranslationY = v, headerFromY, headerToY, Easing.CubicInOut) },
			{ 0, 1, new Animation(v => AppHeader.Scale = v, headerFromScale, headerToScale, Easing.CubicInOut) },
			{ 0, 1, new Animation(v => StartBody.Opacity = v, startBodyFrom, startBodyTo, Easing.CubicOut) },
			{ 0, 1, new Animation(v => HomeBody.Opacity = v, homeBodyFrom, homeBodyTo, Easing.CubicOut) },
		};

		var tcs = new TaskCompletionSource<bool>();
		this.Animate(
			"StartHomePage.ModeTransition",
			animation,
			length: TRANSITION_MS,
			// finished signature: (final-progress, was-cancelled). When cancelled the
			// follow-up visibility flip below is skipped — but the *next* TransitionToAsync
			// runs ApplyHeaderLayoutInstant via fall-through if it's the same target,
			// or sets the right visibility on its own animation completion if different.
			finished: (_, cancelled) => tcs.TrySetResult(cancelled));
		bool wasCancelled = await tcs.Task;

		if (wasCancelled)
		{
			// A newer transition superseded us. Don't flip visibility here — the new
			// transition is already animating with its own from/to values.
			return;
		}

		if (target == PageMode.Home)
		{
			StartBody.IsVisible = false;
		}
		else
		{
			HomeBody.IsVisible = false;
		}
	}

	void UpdateLoaderInfoLabels()
	{
		ILoader? loader = viewModel.Loader;
		if (loader is null)
		{
			LoaderInfoTitleLabel.Text = "読み込み済みデータ";
			LoaderInfoDetailLabel.Text = "";
			return;
		}

		// Phase 1 placeholder: show the loader type. Phase 4 will populate with
		// connection address / file metadata.
		string typeName = loader.GetType().Name;
		LoaderInfoTitleLabel.Text = typeName switch
		{
			"SampleDataLoader" => "デモデータ",
			"LoaderJson" => "JSON ファイル",
			"LoaderSQL" => "SQLite ファイル",
			"WebSocketNetworkSyncService" => "サーバー接続中",
			_ => typeName,
		};
		LoaderInfoDetailLabel.Text = "";
	}

	// ----- Button handlers -----

	async void OnConnectServerClicked(object sender, EventArgs e)
	{
		logger.Info("Connect Server clicked");

		try
		{
			await Navigation.PushModalAsync(new ConnectServerDialog());
		}
		catch (Exception ex)
		{
			InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.OnConnectServerClicked (PushModalAsync failed)");
			logger.Error(ex, "PushModalAsync failed");
			await Util.DisplayAlertAsync(this, "Open Popup Failed", ex.ToString(), "OK");
		}
	}

	async void OnSelectFileClicked(object sender, EventArgs e)
	{
		logger.Info("Select File clicked");

		ILoader? lastLoader = viewModel.Loader;
		try
		{
			var result = await FilePicker.Default.PickAsync();
			if (result is null)
			{
				logger.Info("File picker cancelled");
				return;
			}

			logger.Info("File Selected: {0}", result.FullPath);
			if (result.FullPath.EndsWith(".json"))
			{
				viewModel.Loader = await LoaderJson.InitFromFileAsync(result.FullPath);
			}
			else if (result.FullPath.EndsWith(".sqlite") || result.FullPath.EndsWith(".db") || result.FullPath.EndsWith(".sqlite3"))
			{
				viewModel.Loader = new LoaderSQL(result.FullPath);
			}
			else
			{
				logger.Warn("Unknown file type");
				await Util.DisplayAlertAsync(this, "Unknown File Type", "The selected file is not a supported file type.", "OK");
				return;
			}

			if (!ReferenceEquals(lastLoader, viewModel.Loader))
				lastLoader?.Dispose();
		}
		catch (Exception ex)
		{
			if (!ReferenceEquals(lastLoader, viewModel.Loader))
			{
				viewModel.Loader?.Dispose();
				viewModel.Loader = lastLoader;
			}
			InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.OnSelectFileClicked (PickAsync failed)");
			logger.Error(ex, "File selection failed");
			await Util.DisplayAlertAsync(this, "Cannot Open File", ex.ToString(), "OK");
		}
	}

	async void OnLoadDemoClicked(object sender, EventArgs e)
	{
		logger.Info("Load Demo clicked");

		try
		{
			viewModel.Loader?.Dispose();
			viewModel.Loader = await SampleDataLoader.CreateAsync();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Load demo failed");
			InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.OnLoadDemoClicked (CreateAsync failed)");
			await Util.DisplayAlertAsync(this, "エラー", $"サンプルデータの読み込みに失敗しました: {ex.Message}", "OK");
		}
	}

	async void OnPrivacyPolicyClicked(object sender, EventArgs e)
	{
		logger.Info("Privacy Policy clicked");
		try
		{
			await Navigation.PushModalAsync(new PrivacyPolicyDialog());
			// After modal closes, banner + button enabled-state may have changed.
			UpdatePrivacyDependentControls();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "PushModalAsync(PrivacyPolicyDialog) failed");
			InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.OnPrivacyPolicyClicked");
		}
	}

	async void OnThirdPartyLicensesClicked(object sender, EventArgs e)
	{
		logger.Info("Third Party Licenses clicked");
		try
		{
			// asModal:true makes the in-page Close header visible so the modal can be
			// dismissed reliably across platforms (NavigationPage toolbar items render
			// inconsistently on Mac Catalyst).
			var page = new ThirdPartyLicenses(asModal: true);
			await Navigation.PushModalAsync(page);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "PushModalAsync(ThirdPartyLicenses) failed");
			InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.OnThirdPartyLicensesClicked");
		}
	}

	async void OnOpenClicked(object sender, EventArgs e)
	{
		logger.Info("Open clicked");
		await NavigateToDTACAsync();
	}

	async void OnDisconnectClicked(object sender, EventArgs e)
	{
		logger.Info("Disconnect/Close clicked");
		var loader = viewModel.Loader;
		if (loader is null)
			return;

		bool confirm = await Util.DisplayAlertAsync(this, "確認", "現在のデータを閉じますか？", "閉じる", "キャンセル");
		if (!confirm)
			return;

		viewModel.Loader = null;
		loader.Dispose();
		// Loader change triggers OnViewModelPropertyChanged -> animate back to Start mode.
	}

	// ----- Helpers -----

	private static async Task NavigateToDTACAsync()
	{
		try
		{
			logger.Info("Navigating to DTAC page");
			await Shell.Current.GoToAsync($"//{ViewHost.NameOfThisClass}");
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Error navigating to DTAC page");
			InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.NavigateToDTACAsync failed");
		}
	}

	private async Task ShowFileSelectionDialogAsync()
	{
		try
		{
			var jsonFiles = DefaultTimetableFileLoader.GetAvailableJsonFiles();
			if (jsonFiles.Length == 0)
			{
				logger.Warn("No JSON files found for default selection");
				return;
			}

			var fileNames = jsonFiles.Select(f => f.Name).ToArray();
			var filePaths = jsonFiles.Select(f => f.FullName).ToArray();

			string? selected = await DisplayActionSheetAsync(
				"どのファイルを開きますか？",
				"キャンセル",
				null,
				fileNames);

			if (string.IsNullOrEmpty(selected) || selected == "キャンセル")
				return;

			int idx = Array.IndexOf(fileNames, selected);
			if (idx < 0 || idx >= filePaths.Length)
				return;

			bool loaded = await viewModel.LoadSelectedTimetableFileAsync(filePaths[idx]);
			if (loaded)
				await NavigateToDTACAsync();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "ShowFileSelectionDialogAsync failed");
			InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.ShowFileSelectionDialogAsync");
		}
	}

	// ----- Test seams -----

	void TestSeedButton_Clicked(object sender, EventArgs e)
	{
#if UI_TEST
		logger.Info("TestSeedButton clicked: seeding URL history fixtures");
		viewModel.SeedUrlHistoryForTesting(new[]
		{
			"https://example.com/timetable-a.json",
			"https://example.com/timetable-b.json",
		});
#endif
	}

	void TestSeedGpsButton_Clicked(object sender, EventArgs e)
	{
#if UI_TEST
		logger.Info("TestSeedGpsButton clicked: pushing fixture GPS coord");
		try
		{
			var locationService = InstanceManager.LocationService;
			locationService.SetLonLatLocationService();
			locationService.SetGpsLocation(longitude: 139.790, latitude: 35.700, accuracy: 10.0, useAverageDistance: false);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "TestSeedGpsButton failed");
		}
#endif
	}
}

