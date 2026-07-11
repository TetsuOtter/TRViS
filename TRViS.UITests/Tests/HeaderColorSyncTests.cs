using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// E2E for #310: a server HeaderColor override (or its ResetToDefault) must be
/// reflected identically on the Settings screen's native title bar and on
/// DTAC's custom AppBar. Before the fix, AppShell severed its
/// Shell.BackgroundColor binding when applying an override (RemoveBinding +
/// literal assignment), so only DTAC — bound directly to
/// EasterEggPageViewModel.ShellBackgroundColor — tracked the color, while
/// Settings' own title bar stayed stuck at whatever it last showed.
///
/// Drives the override deterministically via the UI_TEST-only
/// StartHome.TestSetHeaderColorOverrideButton / TestResetHeaderColorOverrideButton
/// seams (set AppViewModel.HeaderColorOverride_RGB directly, mirroring what a
/// real server HeaderColor command would do), so this fixture needs no real
/// WebSocket server and stays deterministic on CI. Reads the resulting color
/// via a pair of invisible mirror Labels (AppBar.HeaderColorSeam /
/// Settings.HeaderColorSeam) that both expose EasterEggPageViewModel.
/// EffectiveShellBackgroundColor as "#RRGGBB" text — the same "always
/// non-empty sentinel-prefixed text" pattern already used by
/// AppBar.ConnectionStatus (#266), since colors aren't reliably readable
/// straight off a native title bar / BoxView via Appium.
/// </summary>
[TestFixture]
[Infrastructure.RetryAllTests(2)] // see AppLaunchTests for rationale
public class HeaderColorSyncTests : BaseUITest
{
	// Share one Appium session across the fixture (iOS only); see
	// BaseUITest.ShareSessionAcrossTestsInFixture. Mirrors WebSocketReconnectTests.
	protected override bool ShareSessionAcrossTestsInFixture => true;

	// Fixed test color set by the TestSetHeaderColorOverrideButton seam
	// (StartHomePage.xaml.cs: UiTestHeaderColorOverrideRgb). Distinct from the
	// app's default title color (#558833) so it is unambiguous in assertions.
	private const string OverrideColorHex = "#336699";

	private StartHomePageObject _startHomePage = null!;
	private AppShellPage _shell = null!;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		_startHomePage = new StartHomePageObject(Driver);
		if (!_startHomePage.PollDisplayed(AutomationIds.StartHome.Title, timeoutSeconds: 3))
		{
			new AppShellPage(Driver).NavigateToHome();
			_startHomePage = new StartHomePageObject(Driver);
		}
		_startHomePage.AcceptPrivacyPolicyIfNeeded();
		_startHomePage.ClearLoaderForTesting();

		// Each test starts from "no override": a prior test in this shared
		// session may have left HeaderColorOverride_RGB set.
		_startHomePage.ResetHeaderColorOverrideForTesting();

		_shell = new AppShellPage(Driver);

		Assert.That(_startHomePage.IsDisplayed(), Is.True,
			"StartHomePage should be displayed after recovery.");
	}

	[TearDown]
	public override void TearDown()
	{
		// Don't leak an active override into later fixtures sharing this session.
		try { _startHomePage.ResetHeaderColorOverrideForTesting(); }
		catch { /* best-effort cleanup */ }

		base.TearDown();
	}

	[Test]
	public void HeaderColorOverride_AppliesToBothSettingsAndDTAC()
	{
		var settings = _shell.NavigateToSettings();
		string beforeColor = settings.ReadHeaderColorViaSeam();
		Assert.That(beforeColor, Is.Not.EqualTo(OverrideColorHex),
			"precondition: the fixed override color must differ from whatever color is active before the override is applied.");

		_shell.NavigateToHome();
		_startHomePage.SetHeaderColorOverrideForTesting();

		settings = _shell.NavigateToSettings();
		Assert.That(settings.WaitForHeaderColor(OverrideColorHex), Is.True,
			"Settings screen's title bar must reflect the HeaderColor override (#310) — " +
			"this is the exact regression reported in #310 (DTAC changed but Settings didn't).");

		if (IsAndroid)
			Assert.Ignore("Android: DTAC flyout item removed (MAUI #16927); DTAC reachable only via relative route after Work commit.");

		var dtac = _shell.NavigateToDTAC();
		Assert.That(dtac.WaitForHeaderColor(OverrideColorHex), Is.True,
			"DTAC's AppBar must reflect the same HeaderColor override as Settings (#310).");
	}

	[Test]
	public void HeaderColorOverride_ResetToDefault_RevertsBothToTheSameColor()
	{
		_startHomePage.SetHeaderColorOverrideForTesting();
		var settings = _shell.NavigateToSettings();
		Assert.That(settings.WaitForHeaderColor(OverrideColorHex), Is.True,
			"precondition: the override must be applied before testing ResetToDefault.");

		_shell.NavigateToHome();
		_startHomePage.ResetHeaderColorOverrideForTesting();

		settings = _shell.NavigateToSettings();
		// Poll away from the override color rather than toward a specific
		// literal: the reverted color is whatever the user's device has
		// picked (default #558833 on a fresh install, but a shared CI session
		// must not assume no other fixture ever touches it).
		bool revertedOnSettings = WaitUntil(() => settings.ReadHeaderColorViaSeam() != OverrideColorHex, 8);
		Assert.That(revertedOnSettings, Is.True,
			"Settings screen must revert away from the override color once reset (#310).");
		string afterReset = settings.ReadHeaderColorViaSeam();

		if (IsAndroid)
			Assert.Ignore("Android: DTAC flyout item removed (MAUI #16927); DTAC reachable only via relative route after Work commit.");

		var dtac = _shell.NavigateToDTAC();
		Assert.That(dtac.WaitForHeaderColor(afterReset), Is.True,
			"DTAC's AppBar must revert to the same (user-picked/default) color as Settings after reset (#310).");
	}

	private static bool WaitUntil(Func<bool> condition, double timeoutSeconds)
	{
		var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
		do
		{
			try
			{
				if (condition())
					return true;
			}
			catch (OpenQA.Selenium.WebDriverException)
			{
				// element momentarily not in tree mid-transition — keep polling
			}
			Thread.Sleep(200);
		} while (DateTime.UtcNow < deadline);
		return false;
	}
}
