using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// E2E for the "unknown destination" confirmation dialog in
/// AppViewModel.AppLink.cs: connecting to an http/https destination never seen
/// before shows a distinct "Connect?" prompt instead of the regular "Open
/// external file?" prompt, so first-time destinations get an explicit
/// confirmation instead of connecting silently.
///
/// Android-only: this is the only platform where the suite has a working
/// locator for a native AlertDialog's buttons (PageObject.WaitForNativeAlertButton
/// uses the platform's standard android:id/button1 / button2 resource-ids).
/// </summary>
[TestFixture]
[Infrastructure.RetryAllTests(2)] // see AppLaunchTests for rationale
public class AppLinkUnknownDestinationTests : BaseUITest
{
	protected override bool ShareSessionAcrossTestsInFixture => true;

	private StartHomePageObject _startHome = null!;

	[SetUp]
	public override void SetUp()
	{
		if (!IsAndroid)
			Assert.Ignore("Native AlertDialog button locators are Android-only; see class doc comment.");

		base.SetUp();

		_startHome = new StartHomePageObject(Driver);

		// Shared-session recovery: close any dialog a prior fixture left open,
		// then get back to a clean Start screen (mirrors ConnectServerDialogTests).
		var dialog = new ConnectServerDialogPageObject(Driver);
		if (dialog.PollDisplayed(AutomationIds.ConnectServer.Title, timeoutSeconds: 1))
		{
			dialog.Close();
			Thread.Sleep(300);
		}
		if (!_startHome.PollDisplayed(AutomationIds.StartHome.Title, timeoutSeconds: 3))
		{
			new AppShellPage(Driver).NavigateToHome();
			_startHome = new StartHomePageObject(Driver);
		}
		_startHome.ClearLoaderForTesting();
		_startHome.AcceptPrivacyPolicyIfNeeded();

		// Start each test from an empty history so "never connected before" is
		// guaranteed rather than depending on leftover state from another fixture.
		_startHome.ClearUrlHistoryForTesting();

		Assert.That(_startHome.IsDisplayed(), Is.True,
			"StartHomePage should be displayed after recovery.");
	}

	[Test]
	public void FirstTimeHttpsDestination_ShowsUnknownDestinationTitle_DeclineDoesNotConnect()
	{
		var dialog = _startHome.OpenConnectServerDialog();
		Assert.That(dialog.IsNewConnectionFormVisible(), Is.True,
			"With empty history, the dialog should default to the new-connection form.");

		dialog.TypeUrl("https://e2e.example/unknown-destination.json");
		dialog.TapConnect();

		string title = dialog.ReadNativeAlertTitle();
		Assert.That(title, Is.EqualTo("Connect?"),
			"A destination never connected to before should show the unknown-destination prompt.");

		// Decline: the connect must not proceed (no network call, no history entry).
		dialog.WaitForNativeAlertButton(positive: false).Click();

		Assert.That(dialog.IsDisplayed(), Is.True,
			"Declining the unknown-destination prompt should leave the ConnectServer dialog open.");
		Assert.That(dialog.IsNewConnectionFormVisible(), Is.True,
			"Declining should not have added the destination to history (which would flip the dialog to the history-list state on reopen).");
	}

	/// <summary>
	/// Connect-to-Server's typed-URL and history-card-tap paths both build an
	/// AppLinkInfo directly (ConnectServerDialog.TryLoadAsync) rather than going
	/// through HandleAppLinkUriAsync(string, ...) — so unlike an OS-level https
	/// deep link, a *known* destination here skips confirmation entirely instead
	/// of falling back to the older "open external file?" prompt (that prompt is
	/// only reachable via the string-URI overload's own http/https branch). This
	/// test asserts the skip: no "Connect?" prompt for a destination already in
	/// history, and the app proceeds straight to attempting the connection.
	/// </summary>
	[Test]
	public void KnownHttpsDestination_SkipsUnknownDestinationConfirmation()
	{
		// Seed history with a fixed URL via the UI_TEST seam button, then retype
		// the same URL — this exercises the "isKnownDestination" branch without
		// needing a real network round-trip first.
		_startHome.SeedUrlHistoryForTesting();
		string knownUrl = StartHomePageObject.SeededHistoryUrls[0];

		var dialog = _startHome.OpenConnectServerDialog();
		Assert.That(dialog.IsHistoryViewVisible(), Is.True,
			"With seeded history, the dialog should default to the history-list state.");
		dialog.OpenNewConnectionForm();

		dialog.TypeUrl(knownUrl);
		dialog.TapConnect();

		// No unknown-destination confirmation for a known destination: the next
		// alert is the connection-failure alert (example.com resolves but 404s
		// on this made-up path). Give the real network round-trip a generous budget.
		string title = dialog.ReadNativeAlertTitle(TimeSpan.FromSeconds(30));
		Assert.That(title, Is.Not.EqualTo("Connect?"),
			"A previously-connected destination must not show the unknown-destination confirmation.");

		// Dismiss the resulting "could not connect" alert (single-button
		// DisplayAlertAsync(title, message, cancel) renders its one button as the
		// *negative* slot android:id/button2, not button1) to leave the app clean.
		dialog.WaitForNativeAlertButton(positive: false).Click();
	}
}
