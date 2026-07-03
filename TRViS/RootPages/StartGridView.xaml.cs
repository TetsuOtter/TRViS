using TRViS.IO;
using TRViS.Localization;
using TRViS.Services;
using TRViS.Utils;
using TRViS.ViewModels;

namespace TRViS.RootPages;

// Start-mode body extracted from StartHomePage. Owns the primary action buttons
// (Connect / SelectFile / Demo) and the privacy reconfirm banner that overlays
// them until the privacy policy is accepted. Sizing of icon/buttons/padding is
// driven by the parent page via ApplyCompactStyling — this view only owns the
// interaction surface.
public partial class StartGridView : Grid
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	// Re-entrancy guard for the demo load. OnLoadDemoClicked is an async void
	// UI-thread handler that does not hide LoadDemoButton while
	// SampleDataLoader.CreateAsync is awaited, so a second tap arriving before
	// the first completes would kick off a concurrent SetLoader + dispose
	// race. A plain bool is sufficient because all access is on the MAUI UI
	// thread.
	bool _isLoadingDemo;

	// ----- Primary / Demo button sizing applied by ApplyCompactStyling -----
	// Two tiers: full-size for non-compact portrait + tablet windows, and
	// compact for narrow-tall windows / landscape-phone where the natural
	// 80pt primary buttons + 44pt demo button would overlap. LoadDemo shrinks
	// independently because it sits beneath the two primary actions.
	const double PRIMARY_BUTTON_HEIGHT = 80.0;
	const double PRIMARY_BUTTON_FONT_SIZE = 20.0;
	const double PRIMARY_BUTTON_HEIGHT_COMPACT = 56.0;
	const double PRIMARY_BUTTON_FONT_SIZE_COMPACT = 17.0;
	const double DEMO_BUTTON_HEIGHT = 44.0;
	const double DEMO_BUTTON_HEIGHT_COMPACT = 36.0;

	// Raised when the privacy banner is tapped. The page handles navigation
	// (PushModalAsync PrivacyPolicyDialog) and post-modal refresh
	// (UpdatePrivacyDependentControls, FlyoutBehavior).
	public event EventHandler? PrivacyPolicyRequested;

	public StartGridView()
	{
		InitializeComponent();

		// The in-app QR scanner (ScanQrPage / BarcodeScanning) is compiled on
		// phone TFMs only, and its native backend has a higher OS floor than the
		// app itself (MLKit needs Android 24, Vision/AVFoundation needs iOS 15.1;
		// the app supports Android 23 / iOS 12.2). Hide the entry button where
		// the scanner isn't available so those users never reach a CameraView
		// that would crash. Everything else keeps the manual URL / file flow.
#if ANDROID
		if (!OperatingSystem.IsAndroidVersionAtLeast(24))
			ScanQrButton.IsVisible = false;
#elif IOS
		if (!OperatingSystem.IsIOSVersionAtLeast(15, 1))
			ScanQrButton.IsVisible = false;
#else
		ScanQrButton.IsVisible = false;
#endif
	}

	/// <summary>
	/// Toggles the privacy banner / primary action buttons depending on whether
	/// the privacy policy has been accepted. Called by the page from
	/// UpdatePrivacyDependentControls.
	/// </summary>
	public void SetPrivacyAccepted(bool accepted)
	{
		PrivacyReconfirmBanner.IsVisible = !accepted;
		ConnectServerButton.IsEnabled = accepted;
		ScanQrButton.IsEnabled = accepted;
		SelectFileButton.IsEnabled = accepted;
		LoadDemoButton.IsEnabled = accepted;
		LoadDemoButton.IsVisible = accepted;
	}

	/// <summary>
	/// Applies the compact-portrait / landscape-phone styling owned by the page.
	/// The page passes in the orientation flag so this view doesn't have to
	/// re-derive it. AppHeader sizing is owned by the page (left-column layout
	/// in landscape) and is NOT touched here.
	/// </summary>
	public void ApplyCompactStyling(bool isCompact, bool isLandscapePhone)
	{
		if (isCompact)
		{
			ConnectServerButton.HeightRequest = PRIMARY_BUTTON_HEIGHT_COMPACT;
			ConnectServerButton.FontSize = PRIMARY_BUTTON_FONT_SIZE_COMPACT;
			ScanQrButton.HeightRequest = PRIMARY_BUTTON_HEIGHT_COMPACT;
			ScanQrButton.FontSize = PRIMARY_BUTTON_FONT_SIZE_COMPACT;
			SelectFileButton.HeightRequest = PRIMARY_BUTTON_HEIGHT_COMPACT;
			SelectFileButton.FontSize = PRIMARY_BUTTON_FONT_SIZE_COMPACT;
			LoadDemoButton.HeightRequest = DEMO_BUTTON_HEIGHT_COMPACT;
			// Landscape body sits in the narrow right column — drop the
			// horizontal padding too so wrapped buttons get a touch more width.
			StartBody.Padding = isLandscapePhone
				? new Thickness(12, 4, 12, 8)
				: new Thickness(24, 4, 24, 8);
			StartBody.RowSpacing = 4;
		}
		else
		{
			ConnectServerButton.HeightRequest = PRIMARY_BUTTON_HEIGHT;
			ConnectServerButton.FontSize = PRIMARY_BUTTON_FONT_SIZE;
			ScanQrButton.HeightRequest = PRIMARY_BUTTON_HEIGHT;
			ScanQrButton.FontSize = PRIMARY_BUTTON_FONT_SIZE;
			SelectFileButton.HeightRequest = PRIMARY_BUTTON_HEIGHT;
			SelectFileButton.FontSize = PRIMARY_BUTTON_FONT_SIZE;
			LoadDemoButton.HeightRequest = DEMO_BUTTON_HEIGHT;
			StartBody.Padding = new Thickness(24, 8, 24, 24);
			StartBody.RowSpacing = 8;
		}
	}

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
			await Util.DisplayAlertAsync("Open Popup Failed", ex.ToString(), AppResources.Common_OK);
		}
	}

	async void OnScanQrClicked(object sender, EventArgs e)
	{
		logger.Info("Scan QR clicked");

#if ANDROID || IOS
		try
		{
			await Navigation.PushModalAsync(new ScanQrPage());
		}
		catch (Exception ex)
		{
			InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.OnScanQrClicked (PushModalAsync failed)");
			logger.Error(ex, "PushModalAsync failed");
			await Util.DisplayAlertAsync("Open Scanner Failed", ex.ToString(), AppResources.Common_OK);
		}
#else
		// The scanner is phone-only and ScanQrButton is hidden on desktop, so
		// this handler is unreachable there — but it must still compile for all
		// TFMs since the Clicked binding is in shared XAML.
		await Task.CompletedTask;
#endif
	}

	async void OnSelectFileClicked(object sender, EventArgs e)
	{
		logger.Info("Select File clicked");

		try
		{
			await Navigation.PushModalAsync(new SelectFileDialog());
		}
		catch (Exception ex)
		{
			InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.OnSelectFileClicked (PushModalAsync failed)");
			logger.Error(ex, "PushModalAsync failed");
			await Util.DisplayAlertAsync("Open Dialog Failed", ex.ToString(), AppResources.Common_OK);
		}
	}

	async void OnLoadDemoClicked(object sender, EventArgs e)
	{
		if (_isLoadingDemo)
		{
			logger.Info("Load Demo ignored: a demo load is already in flight");
			return;
		}
		_isLoadingDemo = true;
		logger.Info("Load Demo clicked");

		var viewModel = InstanceManager.AppViewModel;
		try
		{
			// Dispose AFTER the new loader is built so any in-flight property
			// reads on viewModel.Loader during the await don't hit a disposed
			// instance. SetLoader swaps atomically; we then dispose what was
			// previously installed.
			ILoader? previous = viewModel.Loader;
			var newLoader = await SampleDataLoader.CreateAsync();
			viewModel.SetLoader(newLoader, null);
			if (!ReferenceEquals(previous, viewModel.Loader))
				previous?.Dispose();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Load demo failed");
			InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.OnLoadDemoClicked (CreateAsync failed)");
			await Util.DisplayAlertAsync(AppResources.Common_Error, string.Format(AppResources.StartHome_SampleLoadFailedFormat, ex.Message), AppResources.Common_OK);
		}
		finally
		{
			_isLoadingDemo = false;
		}
	}

	void OnPrivacyBannerTapped(object? sender, TappedEventArgs e)
	{
		PrivacyPolicyRequested?.Invoke(this, EventArgs.Empty);
	}

#if UI_TEST
	// Routes the UI_TEST select-file seam (declared in StartHomePage code-behind)
	// through this view's actual SelectFile handler so the seam tracks any future
	// shape change to OnSelectFileClicked without per-test rewrites.
	internal void InvokeSelectFileForTest(object sender, EventArgs e)
		=> OnSelectFileClicked(sender, e);

	// Same idea for the Scan-QR seam: Appium's ACTION_CLICK against the styled
	// PrimaryActionButton is unreliable on Android (see OpenScanQrPage /
	// OpenSelectFileDialog in the page objects), so the E2E taps a plain seam
	// button that routes through the real OnScanQrClicked here.
	internal void InvokeScanQrForTest(object sender, EventArgs e)
		=> OnScanQrClicked(sender, e);
#endif
}
