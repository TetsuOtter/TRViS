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
	// The icon sits to the side of the label (ContentLayout Left/Right — see
	// ApplyIconContentLayout), so it doesn't add its own row of height; what
	// drives these constants is the label. Three buttons on phone-width
	// screens wrap the (previously single-line) PrimaryActionButton labels
	// onto two lines, and the old 80/56 heights were sized for one line only,
	// so the wrapped second line spilled past the button bounds and
	// overlapped whatever sat below it. These add ~8px slack over a 2-line
	// label at each tier's font size.
	const double PRIMARY_BUTTON_HEIGHT = 84.0;
	const double PRIMARY_BUTTON_FONT_SIZE = 20.0;
	const double PRIMARY_BUTTON_ICON_SIZE = 28.0;
	const double PRIMARY_BUTTON_HEIGHT_COMPACT = 68.0;
	const double PRIMARY_BUTTON_FONT_SIZE_COMPACT = 17.0;
	const double PRIMARY_BUTTON_ICON_SIZE_COMPACT = 22.0;
	// The wide-layout Scan label ("Scan"/short) sits under a small icon in a
	// ~120pt-wide column, tighter than ConnectServer/SelectFile's full labels;
	// the standard font size crowds it, so it gets a dedicated smaller size.
	const double SCAN_BUTTON_WIDE_FONT_SIZE = 15.0;
	const double SCAN_BUTTON_WIDE_FONT_SIZE_COMPACT = 13.0;
	const double DEMO_BUTTON_HEIGHT = 44.0;
	const double DEMO_BUTTON_HEIGHT_COMPACT = 36.0;

	// ----- ScanQrButton shape, driven by PrimaryButtons' measured width -----
	// Below the threshold, ConnectServerButton + SelectFileButton alone already
	// wrap onto their own rows (phone-width "stacked" layout); a 3rd full-size
	// labelled button never fits beside ConnectServerButton, so ScanQrButton
	// collapses to an icon-only square sized to match the row so it sits on
	// ConnectServerButton's row instead of wrapping to its own. At/above the
	// threshold (iPad / wide windows) all three fit on one row, so ScanQrButton
	// keeps a short "Scan" label stacked under its icon instead of the long
	// "Scan QR Code" label the other two-line phone layout would need.
	const double WIDE_SCAN_BUTTON_LAYOUT_MIN_WIDTH = 640.0;
	const double SCAN_BUTTON_WIDE_BASIS = 120.0;

	bool _isScanButtonWideLayout;
	bool _scanButtonLayoutApplied;

	// Raised when the privacy banner is tapped. The page handles navigation
	// (PushModalAsync PrivacyPolicyDialog) and post-modal refresh
	// (UpdatePrivacyDependentControls, FlyoutBehavior).
	public event EventHandler? PrivacyPolicyRequested;

	public StartGridView()
	{
		InitializeComponent();

		if (ConnectServerButton.ImageSource is FontImageSource connectIcon)
			connectIcon.Glyph = MaterialIcons.Dns;
		if (SelectFileButton.ImageSource is FontImageSource selectIcon)
			selectIcon.Glyph = MaterialIcons.Description;
		ScanQrButtonIcon.Text = MaterialIcons.QrCodeScanner;

		ApplyIconContentLayout();
		PrimaryButtons.SizeChanged += (_, _) => ApplyScanButtonLayoutForWidth(PrimaryButtons.Width);
		// The app currently only ships ja/en (both LTR), but flip
		// ConnectServerButton/SelectFileButton's icon to the trailing (right)
		// side if a future language is RTL, and re-apply if the user switches
		// language at runtime without restarting.
		// ScanQrButtonLabel.Text is set imperatively (not via {loc:Translate})
		// by ApplyScanButtonLayoutForWidth, so it needs its own re-localization
		// here instead of picking up the change through a binding.
		LocalizationResourceManager.Current.CultureChanged += (_, _) =>
			MainThread.BeginInvokeOnMainThread(() =>
			{
				ApplyIconContentLayout();
				if (_isScanButtonWideLayout)
					ScanQrButtonLabel.Text = AppResources.StartHome_ScanQrShort;
				SemanticProperties.SetDescription(ScanQrButton, AppResources.StartHome_ScanQr);
			});

		// The in-app QR scanner (ScanQrPage / BarcodeScanning) is compiled on
		// phone TFMs only. Android's MLKit backend needs Android 24. On iOS
		// 15.1+ BarcodeScanning.Native.Maui is used; iOS 12.2-15.0 uses the
		// lazily-created AVFoundation fallback in ScanQrPage.
#if ANDROID
		if (!OperatingSystem.IsAndroidVersionAtLeast(24))
			ScanQrButton.IsVisible = false;
#elif !IOS
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
			ScanQrButtonLabel.FontSize = _isScanButtonWideLayout ? SCAN_BUTTON_WIDE_FONT_SIZE_COMPACT : PRIMARY_BUTTON_FONT_SIZE_COMPACT;
			SelectFileButton.HeightRequest = PRIMARY_BUTTON_HEIGHT_COMPACT;
			SelectFileButton.FontSize = PRIMARY_BUTTON_FONT_SIZE_COMPACT;
			SetPrimaryButtonIconSize(PRIMARY_BUTTON_ICON_SIZE_COMPACT);
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
			ScanQrButtonLabel.FontSize = _isScanButtonWideLayout ? SCAN_BUTTON_WIDE_FONT_SIZE : PRIMARY_BUTTON_FONT_SIZE;
			SelectFileButton.HeightRequest = PRIMARY_BUTTON_HEIGHT;
			SelectFileButton.FontSize = PRIMARY_BUTTON_FONT_SIZE;
			SetPrimaryButtonIconSize(PRIMARY_BUTTON_ICON_SIZE);
			LoadDemoButton.HeightRequest = DEMO_BUTTON_HEIGHT;
			StartBody.Padding = new Thickness(24, 8, 24, 24);
			StartBody.RowSpacing = 8;
		}

		// HeightRequest just changed above; keep the icon-only square in sync
		// with it (no-op when the wide icon-over-label layout is active).
		SyncNarrowScanButtonSquareSize();
	}

	/// <summary>
	/// Puts ConnectServerButton/SelectFileButton's icon on the leading side of
	/// the label (left for LTR, right for RTL locales), driven by the current
	/// culture rather than a static XAML value. ScanQrButton is excluded: it's
	/// a Border-based composite (icon Label stacked over a text Label, always
	/// top/bottom - see StartGridView.xaml) rather than a real Button with a
	/// ContentLayout, so there's no leading/trailing side to flip here.
	/// </summary>
	void ApplyIconContentLayout()
	{
		var position = LocalizationResourceManager.Current.CurrentCulture.TextInfo.IsRightToLeft
			? Button.ButtonContentLayout.ImagePosition.Right
			: Button.ButtonContentLayout.ImagePosition.Left;
		var layout = new Button.ButtonContentLayout(position, 8);
		ConnectServerButton.ContentLayout = layout;
		SelectFileButton.ContentLayout = layout;
	}

	/// <summary>
	/// Switches ScanQrButton between an icon-only square (narrow/phone: sits
	/// beside ConnectServerButton on row 0, with SelectFileButton spanning
	/// both columns on row 1 below) and a short icon-over-label button
	/// (wide/tablet: all three buttons fit on one row already, so the long
	/// "Scan QR Code" label isn't needed). Driven by PrimaryButtons' measured
	/// width rather than device idiom, so it also behaves correctly in
	/// split-view / resized desktop windows.
	///
	/// Both shapes re-lay PrimaryButtons out as an explicit Grid (row/column
	/// definitions + each button's Grid.Row/Column/ColumnSpan) rather than
	/// relying on FlexLayout's grow/justify arithmetic: FlexLayout wraps each
	/// line independently with no way to align a 2-item line's edges with a
	/// 1-item line below it, which previously left ConnectServerButton and
	/// SelectFileButton's edges a few points apart instead of flush.
	/// </summary>
	void ApplyScanButtonLayoutForWidth(double availableWidth)
	{
		if (availableWidth <= 0)
			return;
		bool isWide = availableWidth >= WIDE_SCAN_BUTTON_LAYOUT_MIN_WIDTH;
		if (isWide == _isScanButtonWideLayout && _scanButtonLayoutApplied)
			return;
		_isScanButtonWideLayout = isWide;
		_scanButtonLayoutApplied = true;

		if (isWide)
		{
			ScanQrButtonLabel.Text = AppResources.StartHome_ScanQrShort;
			ScanQrButtonLabel.IsVisible = true;
			// HeightRequest already reflects the last ApplyCompactStyling pass,
			// so use it to pick the matching wide-layout font tier.
			ScanQrButtonLabel.FontSize = ScanQrButton.HeightRequest <= PRIMARY_BUTTON_HEIGHT_COMPACT
				? SCAN_BUTTON_WIDE_FONT_SIZE_COMPACT
				: SCAN_BUTTON_WIDE_FONT_SIZE;
			ScanQrButton.WidthRequest = -1;
			ScanQrButton.MinimumWidthRequest = SCAN_BUTTON_WIDE_BASIS;
			// Unlike a real Button, ScanQrButton (a Border) isn't automatically
			// exposed to VoiceOver/accessibility-tree tooling (idb, XCUITest)
			// just by having child Labels with visible text - it needs its own
			// SemanticProperties.Description to be picked up as an element at
			// all, in both the icon-only (narrow) and icon+label (wide) shapes.
			SemanticProperties.SetDescription(ScanQrButton, AppResources.StartHome_ScanQr);

			// All three buttons share row 0. ConnectServerButton/SelectFileButton
			// sit in the two Star columns (splitting the remaining space evenly),
			// ScanQrButton in the fixed Auto column between them.
			PrimaryButtons.RowDefinitions.Clear();
			PrimaryButtons.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
			PrimaryButtons.ColumnDefinitions.Clear();
			PrimaryButtons.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
			PrimaryButtons.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
			PrimaryButtons.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

			Grid.SetRow(ConnectServerButton, 0);
			Grid.SetColumn(ConnectServerButton, 0);
			Grid.SetColumnSpan(ConnectServerButton, 1);
			Grid.SetRow(ScanQrButton, 0);
			Grid.SetColumn(ScanQrButton, 1);
			Grid.SetRow(SelectFileButton, 0);
			Grid.SetColumn(SelectFileButton, 2);
			Grid.SetColumnSpan(SelectFileButton, 1);
		}
		else
		{
			ScanQrButtonLabel.Text = string.Empty;
			ScanQrButtonLabel.IsVisible = false;
			SyncNarrowScanButtonSquareSize();
			SemanticProperties.SetDescription(ScanQrButton, AppResources.StartHome_ScanQr);

			// ConnectServerButton + ScanQrButton share row 0 (ScanQrButton pinned
			// to an Auto column sized to its own square width);
			// SelectFileButton spans both columns on row 1, landing its left and
			// right edges on the exact same column boundaries as row 0 above it.
			PrimaryButtons.RowDefinitions.Clear();
			PrimaryButtons.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
			PrimaryButtons.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
			PrimaryButtons.ColumnDefinitions.Clear();
			PrimaryButtons.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
			PrimaryButtons.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

			Grid.SetRow(ConnectServerButton, 0);
			Grid.SetColumn(ConnectServerButton, 0);
			Grid.SetColumnSpan(ConnectServerButton, 1);
			Grid.SetRow(ScanQrButton, 0);
			Grid.SetColumn(ScanQrButton, 1);
			Grid.SetRow(SelectFileButton, 1);
			Grid.SetColumn(SelectFileButton, 0);
			Grid.SetColumnSpan(SelectFileButton, 2);
		}
	}

	/// <summary>
	/// Keeps ScanQrButton's icon-only square sized to match its current
	/// HeightRequest (which ApplyCompactStyling toggles independently of the
	/// narrow/wide width mode). No-op when the wide icon-over-label layout is
	/// active, where width is driven by SCAN_BUTTON_WIDE_BASIS instead.
	/// </summary>
	void SyncNarrowScanButtonSquareSize()
	{
		if (_isScanButtonWideLayout || !_scanButtonLayoutApplied)
			return;
		ScanQrButton.MinimumWidthRequest = ScanQrButton.HeightRequest;
		ScanQrButton.WidthRequest = ScanQrButton.HeightRequest;
	}

	void SetPrimaryButtonIconSize(double size)
	{
		if (ConnectServerButton.ImageSource is FontImageSource connectIcon)
			connectIcon.Size = size;
		ScanQrButtonIcon.FontSize = size;
		if (SelectFileButton.ImageSource is FontImageSource selectIcon)
			selectIcon.Size = size;
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

	// ScanQrButton is a Border+TapGestureRecognizer, not a Button (see
	// StartGridView.xaml), so its Tapped event is EventHandler<TappedEventArgs>
	// rather than Button's Clicked (EventHandler). This just forwards into the
	// same handler both other buttons already use.
	//
	// The explicit IsEnabled guard (a real Button ignores taps on its own once
	// disabled; a Border+TapGestureRecognizer doesn't) is defense in depth:
	// PrivacyReconfirmBanner already physically overlays this button while
	// privacy isn't accepted, but SetPrivacyAccepted also sets IsEnabled like
	// it does for the other two (real) buttons, so this keeps that consistent
	// even if the overlay ever stops fully covering it.
	void OnScanQrButtonTapped(object? sender, TappedEventArgs e)
	{
		if (!ScanQrButton.IsEnabled)
			return;
		OnScanQrClicked(sender!, e);
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
			await Util.DisplayAlertAsync(AppResources.Common_Error, AppResources.ScanQr_OpenFailedMessage, AppResources.Common_OK);
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
