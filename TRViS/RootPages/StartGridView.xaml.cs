using TRViS.IO;
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

	// Raised when the privacy banner is tapped. The page handles navigation
	// (PushModalAsync PrivacyPolicyDialog) and post-modal refresh
	// (UpdatePrivacyDependentControls, FlyoutBehavior).
	public event EventHandler? PrivacyPolicyRequested;

	public StartGridView()
	{
		InitializeComponent();
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
			ConnectServerButton.HeightRequest = 56;
			ConnectServerButton.FontSize = 17;
			SelectFileButton.HeightRequest = 56;
			SelectFileButton.FontSize = 17;
			LoadDemoButton.HeightRequest = 36;
			// Landscape body sits in the narrow right column — drop the
			// horizontal padding too so wrapped buttons get a touch more width.
			StartBody.Padding = isLandscapePhone
				? new Thickness(12, 4, 12, 8)
				: new Thickness(24, 4, 24, 8);
			StartBody.RowSpacing = 4;
		}
		else
		{
			ConnectServerButton.HeightRequest = 80;
			ConnectServerButton.FontSize = 20;
			SelectFileButton.HeightRequest = 80;
			SelectFileButton.FontSize = 20;
			LoadDemoButton.HeightRequest = 44;
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
			await Util.DisplayAlertAsync("Open Popup Failed", ex.ToString(), "OK");
		}
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
			await Util.DisplayAlertAsync("Open Dialog Failed", ex.ToString(), "OK");
		}
	}

	async void OnLoadDemoClicked(object sender, EventArgs e)
	{
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
			await Util.DisplayAlertAsync("エラー", $"サンプルデータの読み込みに失敗しました: {ex.Message}", "OK");
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
#endif
}
