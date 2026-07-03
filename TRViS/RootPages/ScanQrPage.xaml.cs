using BarcodeScanning;

using TRViS.IO.RequestInfo;
using TRViS.Localization;
using TRViS.Services;
using TRViS.Utils;

namespace TRViS.RootPages;

/// <summary>
/// In-app QR scanner. Opened from the Start screen's "Scan QR" button (phone
/// only — see <c>IsMobileTarget</c> in the csproj). It reads QR codes with the
/// device camera and <b>only</b> acts on TRViS AppLinks (<c>trvis://…</c>);
/// every other QR payload is ignored so the camera keeps scanning. An accepted
/// link is handed to the same pipeline as an OS-delivered deep link
/// (<see cref="ViewModels.AppViewModel.HandleAppLinkUriAsync(string, System.Threading.CancellationToken)"/>).
/// </summary>
public partial class ScanQrPage : ContentPage
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	// One-shot guard. OnDetectionFinished is always raised on the main thread
	// (the library marshals via MainThread.BeginInvokeOnMainThread), and it
	// fires repeatedly while a code stays in frame, so a plain bool is enough
	// to ensure we handle the first accepted TRViS link exactly once.
	private bool _handled;

	public ScanQrPage()
	{
		logger.Trace("Creating");
		InitializeComponent();

#if UI_TEST
		// The CI emulator has no usable camera, and initializing the live
		// CameraView there stalls the GL/surface pipeline (10 s+ frame times) so
		// the page never renders for Appium. The E2E drives the "only TRViS
		// AppLinks" gate through the hidden seam buttons instead of a real
		// detection, so drop the camera preview entirely (and never request
		// camera permission — see OnAppearing). Every other Barcode access is
		// compiled out under UI_TEST for the same reason.
		RootLayout.Children.Remove(Barcode);
		BuildTestSeamButtons();
#endif
	}

#if UI_TEST
	// Two hidden-in-plain-sight buttons pinned to the bottom edge so Appium can
	// drive the accept / reject paths without a real camera. Production builds
	// never call this, so no "ScanQr.TestSimulate*" AutomationIds ship.
	void BuildTestSeamButtons()
	{
		var valid = new Button
		{
			AutomationId = "ScanQr.TestSimulateValidButton",
			Text = "sim-valid",
			HeightRequest = 32,
			Opacity = 0.01,
		};
		valid.Clicked += async (_, __) => await SimulateDetectionForTest(TestValidPayload);
		AbsoluteLayout.SetLayoutFlags(valid, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.PositionProportional | Microsoft.Maui.Layouts.AbsoluteLayoutFlags.WidthProportional);
		AbsoluteLayout.SetLayoutBounds(valid, new Rect(0, 1, 0.5, 32));

		var invalid = new Button
		{
			AutomationId = "ScanQr.TestSimulateInvalidButton",
			Text = "sim-invalid",
			HeightRequest = 32,
			Opacity = 0.01,
		};
		invalid.Clicked += async (_, __) => await SimulateDetectionForTest(TestInvalidPayload);
		AbsoluteLayout.SetLayoutFlags(invalid, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.PositionProportional | Microsoft.Maui.Layouts.AbsoluteLayoutFlags.WidthProportional);
		AbsoluteLayout.SetLayoutBounds(invalid, new Rect(1, 1, 0.5, 32));

		RootLayout.Children.Add(valid);
		RootLayout.Children.Add(invalid);
	}
#endif

	protected override async void OnAppearing()
	{
		base.OnAppearing();

#if UI_TEST
		// Seam-driven under test: no camera, no permission prompt. The page stays
		// open so the hidden seam buttons can exercise the gate (see constructor).
		await Task.CompletedTask;
		return;
#else
		// The library owns the platform permission request (camera, and on
		// Android the auto-granted VIBRATE). Ask before enabling the preview.
		bool granted;
		try
		{
			granted = await Methods.AskForRequiredPermissionAsync();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "AskForRequiredPermissionAsync failed");
			InstanceManager.CrashlyticsWrapper.Log(ex, "ScanQrPage.OnAppearing (permission)");
			granted = false;
		}

		if (!granted)
		{
			logger.Warn("Camera permission not granted -> closing scanner");
			await Util.DisplayAlertAsync(
				AppResources.ScanQr_PermissionDeniedTitle,
				AppResources.ScanQr_PermissionDeniedBody,
				AppResources.Common_OK);
			await CloseAsync();
			return;
		}

		Barcode.CameraEnabled = true;
#endif
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
#if !UI_TEST
		// Release the camera whenever the page leaves the screen (successful
		// scan, close button, OS-level dismissal). Handler disconnection is
		// automatic from .NET MAUI 9, so nothing else is required.
		Barcode.CameraEnabled = false;
#endif
	}

	private async void OnDetectionFinished(object? sender, OnDetectionFinishedEventArg e)
	{
		if (_handled)
			return;

		foreach (BarcodeResult barcode in e.BarcodeResults)
		{
			string? value = !string.IsNullOrEmpty(barcode.RawValue) ? barcode.RawValue : barcode.DisplayValue;
			if (await TryHandleCandidateAsync(value))
				return;
		}
	}

	/// <summary>
	/// Applies the "only process TRViS AppLinks" gate to a single decoded QR
	/// payload. Returns <c>true</c> when it was an accepted TRViS AppLink (and
	/// handling has started); <c>false</c> for anything else, so the caller
	/// keeps scanning. Shared by the live detection loop and the UI_TEST seam so
	/// both go through the exact same acceptance path.
	/// </summary>
	private async Task<bool> TryHandleCandidateAsync(string? value)
	{
		if (_handled)
			return false;

		// A QR pointing at an arbitrary https/ws URL or plain text is ignored.
		if (!AppLinkInfo.IsTrvisAppLink(value))
			return false;

		await HandleAcceptedLinkAsync(value!.Trim());
		return true;
	}

	private async Task HandleAcceptedLinkAsync(string trvisLink)
	{
		// Runs on the main thread (see _handled note). Latch the guard and stop
		// the camera before doing any awaited work so repeat detections of the
		// same code are dropped.
		if (_handled)
			return;
		_handled = true;

		logger.Info("Accepted TRViS AppLink from QR: {0}", trvisLink);
#if !UI_TEST
		Barcode.PauseScanning = true;
		Barcode.CameraEnabled = false;
#endif

		// Haptic confirmation, fired only once a TRViS AppLink is accepted (the
		// library's built-in VibrationOnDetected is disabled so ignored QR codes
		// don't buzz). Best-effort: not all devices support it.
		try
		{
			HapticFeedback.Default.Perform(HapticFeedbackType.Click);
		}
		catch (Exception ex)
		{
			logger.Trace(ex, "HapticFeedback not available");
		}

		// Close the scanner first so the AppLink pipeline's confirmation dialogs
		// / result alerts and the loaded timetable appear over the Start screen,
		// not on top of a torn-down camera page.
		await CloseAsync();

		try
		{
			await InstanceManager.AppViewModel.HandleAppLinkUriAsync(trvisLink, CancellationToken.None);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "HandleAppLinkUriAsync failed for scanned link");
			InstanceManager.CrashlyticsWrapper.Log(ex, "ScanQrPage.HandleAcceptedLinkAsync");
		}
	}

	private async void OnCloseClicked(object sender, EventArgs e)
	{
		logger.Trace("Close clicked");
#if !UI_TEST
		Barcode.CameraEnabled = false;
#endif
		await CloseAsync();
	}

	private void OnTorchClicked(object sender, EventArgs e)
	{
#if !UI_TEST
		Barcode.TorchOn = !Barcode.TorchOn;
		logger.Trace("Torch toggled -> {0}", Barcode.TorchOn);
#endif
	}

	private async Task CloseAsync()
	{
		try
		{
			await Navigation.PopModalAsync();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "PopModalAsync failed");
		}
	}

#if UI_TEST
	// Test seam: a real camera can't be driven by Appium, so these feed a canned
	// payload through the *exact* same gate as a live detection (TryHandleCandidateAsync)
	// — a valid TRViS _test AppLink (accepted -> closes + loads) and a plain https
	// URL (rejected -> page stays open). Invoked from hidden UI_TEST buttons built
	// in the constructor. Mirrors the trvis://_test/... seams in AppViewModel.AppLink.cs.

	// A trvis:// _test link that HandleAppLinkUriAsync resolves without any
	// network, so the accept path is observable (it seeds the URL history).
	internal const string TestValidPayload =
		"trvis://_test/seed-url-history?urls=https%3A%2F%2Fe2e.example%2Fscanned.json";
	// A non-TRViS payload the gate must reject.
	internal const string TestInvalidPayload = "https://e2e.example/not-a-trvis-link";

	// Returns true when the payload was accepted as a TRViS AppLink.
	internal Task<bool> SimulateDetectionForTest(string value)
		=> TryHandleCandidateAsync(value);
#endif
}
