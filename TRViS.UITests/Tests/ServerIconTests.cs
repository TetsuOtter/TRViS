using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// E2E for the Home screen's server-icon display: once a ServerInfo message
/// delivers an icon, the LoaderInfoCard shows it in place of the loader-type
/// glyph (see HomeGridView.UpdateServerIconImage).
///
/// Injected via the UI_TEST-only <c>trvis://_test/serverinfo</c> deeplink,
/// mirroring how NotificationBannerTests injects notifications, so this
/// fixture needs no real WebSocket/reference server and stays deterministic
/// on CI.
/// </summary>
[TestFixture]
[Infrastructure.RetryAllTests(2)] // see AppLaunchTests for rationale
public class ServerIconTests : BaseUITest
{
	protected override bool ShareSessionAcrossTestsInFixture => true;

	private StartHomePageObject _startHomePage = null!;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		_startHomePage = new StartHomePageObject(Driver);

		// Shared-session recovery: a prior test may have left the Connect
		// dialog open.
		var dialog = new ConnectServerDialogPageObject(Driver);
		if (dialog.PollDisplayed(AutomationIds.ConnectServer.Title, timeoutSeconds: 1))
		{
			dialog.Close();
			Thread.Sleep(300);
		}

		if (!_startHomePage.PollDisplayed(AutomationIds.StartHome.Title, timeoutSeconds: 3))
		{
			new AppShellPage(Driver).NavigateToHome();
			_startHomePage = new StartHomePageObject(Driver);
		}
		_startHomePage.ClearLoaderForTesting();
		_startHomePage.AcceptPrivacyPolicyIfNeeded();

		Assert.That(_startHomePage.IsDisplayed(), Is.True,
			"StartHomePage should be displayed after recovery.");
	}

	// Tiny inline SVG data URIs, distinguished only by fill color, so a single
	// short Appium-typeable deeplink can carry both a light and a dark icon.
	private const string LightIcon = "data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciPjxyZWN0IHdpZHRoPSIxMCIgaGVpZ2h0PSIxMCIgZmlsbD0iI2ZmMDAwMCIvPjwvc3ZnPg==";
	private const string DarkIcon = "data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciPjxyZWN0IHdpZHRoPSIxMCIgaGVpZ2h0PSIxMCIgZmlsbD0iIzAwMDBmZiIvPjwvc3ZnPg==";

	[Test]
	public void ServerInfoWithIcon_ShowsIconInsteadOfGlyph()
	{
		Assume.That(_startHomePage.IsDisplayed(), Is.True);

		Assert.That(_startHomePage.IsServerIconImageVisible(timeoutSeconds: 1), Is.False,
			"No icon should be shown before any ServerInfo is received.");

		string deeplink = "trvis://_test/serverinfo?name=Iconic%20Server&iconimage=" + Uri.EscapeDataString(LightIcon);
		SubmitDeeplink(deeplink);

		Assert.That(_startHomePage.IsServerIconImageVisible(), Is.True,
			"Server icon should be shown once ServerInfo delivers an icon.");
	}

	/// <summary>
	/// Verifies HomeGridView.UpdateServerIconImage's dark/light selection
	/// (IconImageDark preferred while CurrentAppTheme is Dark, IconImage
	/// otherwise) actually reacts live to a theme flip, not just at first
	/// paint. Relies on ForceThemeForTesting also setting
	/// AppViewModel.CurrentAppTheme directly (StartHomePage.xaml.cs's
	/// TestForceDarkThemeButton_Clicked / TestForceLightThemeButton_Clicked) —
	/// UserAppTheme alone does not reliably propagate to CurrentAppTheme.
	/// </summary>
	[Test]
	public void ServerInfoWithDarkIcon_SwapsIconOnThemeChange()
	{
		Assume.That(_startHomePage.IsDisplayed(), Is.True);

		try
		{
			_startHomePage.ForceThemeForTesting(dark: false);

			string deeplink = "trvis://_test/serverinfo?name=Themed%20Server"
				+ "&iconimage=" + Uri.EscapeDataString(LightIcon)
				+ "&iconimagedark=" + Uri.EscapeDataString(DarkIcon);
			SubmitDeeplink(deeplink);

			Assert.That(_startHomePage.IsServerIconImageVisible(), Is.True,
				"Server icon should be shown in light mode.");

			_startHomePage.ForceThemeForTesting(dark: true);

			Assert.That(_startHomePage.IsServerIconImageVisible(), Is.True,
				"Server icon should remain shown after flipping to dark mode (IconImageDark should be used).");
		}
		finally
		{
			_startHomePage.ResetThemeForTesting();
		}
	}

	private void SubmitDeeplink(string deeplink)
	{
		var dialog = _startHomePage.OpenConnectServerDialog();
		Assert.That(dialog.IsDisplayed(), Is.True, "Connect dialog should open.");

		if (!dialog.IsNewConnectionFormVisible())
			dialog.OpenNewConnectionForm();

		dialog.TypeUrl(deeplink);
		dialog.ConnectButton.Click();
	}
}
