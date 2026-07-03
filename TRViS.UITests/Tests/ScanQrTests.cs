using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// E2E for the in-app QR scanner. The camera can't be driven by Appium, so
/// these exercise the wiring that unit tests can't reach — CameraView detection
/// → "only TRViS AppLinks" gate → ScanQrPage close + HandleAppLinkUriAsync — via
/// hidden UI_TEST seam buttons that feed a canned payload through the exact same
/// acceptance path a live detection hits. The gate itself is unit-tested in
/// TRViS.IO.Tests (AppLinkInfo.IsTrvisAppLink).
///
/// Android-only: the scanner is phone-only (Android + iOS), the app's Appium
/// suite runs on Android (iOS moved to XCUITest), and Windows has no scanner.
/// </summary>
[TestFixture]
[Infrastructure.RetryAllTests(2)] // see AppLaunchTests for rationale
public class ScanQrTests : BaseUITest
{
	protected override bool ShareSessionAcrossTestsInFixture => true;

	private StartHomePageObject _startHome = null!;

	[SetUp]
	public override void SetUp()
	{
		if (!IsAndroid)
			Assert.Ignore("QR scanner is phone-only; the Appium suite exercises it on Android.");

		base.SetUp();

		_startHome = new StartHomePageObject(Driver);

		// Shared-session recovery: a prior fixture may have left a modal open or
		// the app in Home mode. Bring it back to a clean Start screen.
		if (!_startHome.PollDisplayed(AutomationIds.StartHome.Title, timeoutSeconds: 3))
		{
			new AppShellPage(Driver).NavigateToHome();
			_startHome = new StartHomePageObject(Driver);
		}
		_startHome.ClearLoaderForTesting();
		_startHome.AcceptPrivacyPolicyIfNeeded();

		// Start each test from an empty history so the accept-path assertion
		// (seeded URL appears) can't be satisfied by a leftover entry.
		_startHome.ClearUrlHistoryForTesting();

		Assert.That(_startHome.IsDisplayed(), Is.True,
			"StartHomePage should be displayed after recovery.");
	}

	[Test]
	public void ValidTrvisQr_ClosesScannerAndProcessesLink()
	{
		var scan = _startHome.OpenScanQrPage();
		Assert.That(scan.IsDisplayed(), Is.True, "Scanner page should open.");
		Assert.That(scan.IsTrademarkNoticeDisplayed(), Is.True,
			"The DENSO WAVE QR Code trademark notice must be shown on the scanner page.");

		scan.SimulateValidScan();

		// Accept path: the scanner closes back to StartHome.
		Assert.That(_startHome.PollDisplayed(AutomationIds.StartHome.Title, timeoutSeconds: 10), Is.True,
			"Scanner should close back to StartHome after a valid TRViS AppLink QR.");

		// ...and the AppLink was actually processed — the seeded URL is now in the
		// ConnectServer history list.
		var dialog = _startHome.OpenConnectServerDialog();
		Assert.That(dialog.PollDisplayed(
				AutomationIds.ConnectServer.HistoryItemPrefix + ScanQrPageObject.SeededUrl, timeoutSeconds: 10),
			Is.True,
			"The scanned TRViS AppLink should have been handed to HandleAppLinkUriAsync (URL seeded to history).");
		dialog.Close();
	}

	[Test]
	public void NonTrvisQr_IsIgnoredAndScannerStaysOpen()
	{
		var scan = _startHome.OpenScanQrPage();
		Assert.That(scan.IsDisplayed(), Is.True, "Scanner page should open.");

		scan.SimulateInvalidScan();

		// Reject path: a non-TRViS QR must be ignored — the scanner stays open,
		// nothing is loaded. Give the (non-)transition a moment to prove it does
		// NOT close.
		Thread.Sleep(1000);
		Assert.That(scan.IsDisplayed(timeoutSeconds: 2), Is.True,
			"A non-TRViS QR must be ignored; the scanner should stay open.");

		scan.Close();
	}
}
