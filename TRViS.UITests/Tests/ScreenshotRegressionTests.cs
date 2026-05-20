using TRViS.UITests.Infrastructure;
using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// Screenshot-regression gate + Apple-review capture pass.
///
/// Walks every reachable screen of the app in Light then Dark, in Japanese
/// then English, and diffs each frame against a committed baseline
/// (<c>TRViS.UITests/Screenshots/&lt;deviceClass&gt;/&lt;theme&gt;/&lt;lang&gt;/&lt;screen&gt;.png</c>).
/// The same run also writes every frame as a test attachment so the images
/// can be reviewed against Apple's HIG / App Review Guidelines straight
/// from the CI artifacts.
///
/// Determinism levers (so the same UI produces the same pixels run-to-run):
///  * <c>StartHome.TestFreezeClockButton</c> pins the app clock to
///    09:41:00 — the DTAC AppBar shows a live HH:mm:ss otherwise.
///  * <c>StartHome.TestForceLightThemeButton</c> /
///    <c>...ForceDarkThemeButton</c> force the app-wide theme so a single
///    shared Appium session captures both palettes regardless of the
///    simulator's system appearance.
///  * <c>StartHome.TestSetLanguageEnglishButton</c> /
///    <c>...JapaneseButton</c> (the #40 seam, reused) pin the app-wide UI
///    language so the <c>{loc:Translate}</c>-bound captions render a fixed
///    string instead of resolving against the CI device locale.
///  * <c>run-ui-tests.sh</c> applies <c>xcrun simctl status_bar override</c>
///    (time 9:41, full battery, wifi) before the session so the iOS status
///    bar in the screenshot is constant.
///  * <c>EasterEggPage</c>'s <c>#if UI_TEST</c> seam substitutes a fixed
///    placeholder for the machine-specific log-file path, so the Settings
///    screen is deterministic without any per-region mask.
///  * <see cref="ScreenshotComparer"/> tolerates AA / 1px reflow noise.
///
/// Regenerate baselines with <c>./update-screenshots.sh</c> (sets
/// <c>SCREENSHOT_UPDATE=1</c>, which makes every comparison overwrite the
/// baseline and pass).
///
/// iOS-only: the Apple-guideline deliverable is iOS-specific, and the
/// freeze/theme seams + status-bar override are tuned for the iOS
/// simulator. The fixture <see cref="NUnit.Framework.IgnoreAttribute"/>s
/// itself on every other platform.
///
/// <see cref="NUnit.Framework.OrderAttribute"/> 3 runs this right after the
/// privacy-banner-dependent fixtures (AppLaunchTests = 1,
/// FirebaseSettingTests = 2) and before the bulk of the suite. That matters
/// on the iPad mini A17 matrix entry, whose WDA is documented to die
/// mid-suite (see <c>ui-test.yml</c>): capturing early maximises the chance
/// the whole walk completes before WDA wears out.
/// </summary>
[TestFixture]
[Order(3)]
public class ScreenshotRegressionTests : BaseUITest
{
	// One Appium session for the whole fixture (no app restart between the
	// light and dark test cases) — the theme/clock seams recover state
	// idempotently, a process restart is not needed.
	protected override bool ShareSessionAcrossTestsInFixture => true;

	// Device classes whose baselines actually gate the build. iPhone 16 and
	// iPad mini (A17 Pro) are the canonical regression devices; iPad mini
	// (5th gen) is captured for review but excluded from the pixel gate
	// because its macos-26 + iPadOS-17 WDA path is too flaky to be a
	// blocking signal (it is continue-on-error in ui-test.yml too).
	private static readonly string[] GatedDeviceClasses = { "iphone", "ipad-mini-a17" };

	private StartHomePageObject _start = null!;
	private AppShellPage _shell = null!;
	private string _deviceClass = "iphone";
	private bool _updateMode;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		var platform = (TestContext.Parameters["platform"] ?? "").ToLowerInvariant();
		if (platform != "ios")
			Assert.Ignore($"ScreenshotRegressionTests is iOS-only (platform='{platform}').");

		_deviceClass = TestContext.Parameters["deviceClass"] ?? "iphone";
		_updateMode = Environment.GetEnvironmentVariable("SCREENSHOT_UPDATE") == "1";

		_start = new StartHomePageObject(Driver);
		_shell = new AppShellPage(Driver);

		// A prior test in this fixture (or the previous fixture) may have
		// left the app on DTAC / Settings / a modal. Get back to StartHome
		// in Start mode. Both calls are idempotent.
		if (!_start.PollDisplayed(AutomationIds.StartHome.Title, timeoutSeconds: 3))
		{
			_shell.NavigateToHome();
			_start = new StartHomePageObject(Driver);
		}
		_start.ClearLoaderForTesting();
		_start.AcceptPrivacyPolicyIfNeeded();
	}

	// Explicit theme×language combinatorics (not [Values] cross-product) so
	// the case names — and the baseline directory each writes — are obvious
	// from the source. All three seams are app-wide and idempotent in the
	// shared session, so the cases are order-independent: each sets its own
	// clock/language/theme at the top and does not depend on the prior case.
	[TestCase("light", "ja")]
	[TestCase("light", "en")]
	[TestCase("dark", "ja")]
	[TestCase("dark", "en")]
	public void CaptureAndDiffAllScreens(string theme, string lang)
	{
		bool dark = theme == "dark";
		var failures = new List<string>();

		// Pin clock + language + theme on StartHome before navigating
		// anywhere; all three are app-wide and persist across navigation for
		// the rest of the walk.
		_start.FreezeClockForTesting();
		if (lang == "en")
			_start.SetLanguageEnglishForTesting();
		else
			_start.SetLanguageJapaneseForTesting();
		_start.ForceThemeForTesting(dark);
		// A language switch rebinds every {loc:Translate} caption and a theme
		// flip repaints the whole visual tree — give both a generous beat
		// before the first capture.
		Thread.Sleep(1200);

		// 1. StartHome — Start mode (no loader).
		Capture("startHome-start", theme, lang, failures);

		// 2. Privacy-policy dialog (footer link → read-only dialog, since
		//    AppLaunchTests already accepted the first-launch reconfirm).
		_start.OpenPrivacyPolicyDialog();
		Settle();
		Capture("privacyPolicy", theme, lang, failures);
		_start.ClosePrivacyPolicyDialog();

		// 3. Connect-to-server dialog.
		var connect = _start.OpenConnectServerDialog();
		// Connect-server modal is the known flaky capture: its open
		// animation + async history population can outlast a bare Settle().
		SettleUntilVisuallyStable();
		Capture("connectServer", theme, lang, failures);
		connect.Close();

		// 4. Select-file dialog.
		var selectFile = _start.OpenSelectFileDialog();
		Settle();
		Capture("selectFile", theme, lang, failures);
		selectFile.Close();

		// 5. Third-party licenses (modal page).
		var tpl = _start.OpenThirdPartyLicenses();
		Settle();
		Capture("thirdPartyLicenses", theme, lang, failures);
		tpl.CloseModal();

		// 6. StartHome — Home mode (sample data loaded, work-group list).
		_start.LoadSample();
		_start.WaitForElement(AutomationIds.StartHome.WorkGroupList);
		Settle();
		Capture("startHome-home", theme, lang, failures);

		// 7-9. DTAC. Use the horizontal-timetable seam so the same flow
		//       also surfaces the 横型時刻表 button for the next screen; it
		//       only swaps the Work for an E-train-enabled clone and does
		//       not change the timetable/hako rendering.
		//
		//       The 行路添付 (WorkAffix) tab is intentionally NOT captured:
		//       DTAC.TabWorkAffix is hard-coded IsEnabled="False" in
		//       ViewHost.xaml (placeholder for an unimplemented feature) and
		//       has no dynamic enabler anywhere in the app, so tapping it is
		//       a no-op that would only re-screenshot the previous tab.
		var dtac = _start.SeedHorizontalTimetableAndOpenForTesting();
		dtac.SwitchToTimetableTab();
		Settle();
		Capture("dtac-timetable", theme, lang, failures);

		dtac.TabHako.Click();
		Thread.Sleep(500);
		Capture("dtac-hako", theme, lang, failures);

		// 10. Horizontal (E-train) timetable page.
		dtac.SwitchToTimetableTab();
		if (dtac.IsHorizontalTimetableButtonVisible(timeoutSeconds: 5))
		{
			dtac.TapHorizontalTimetableButton();
			var ht = new HorizontalTimetablePageObject(Driver);
			ht.WaitForElement(AutomationIds.HorizontalTimetable.WebView, TimeSpan.FromSeconds(30));
			// WebView first-paint is slower than a native page swap.
			Thread.Sleep(1500);
			Capture("horizontalTimetable", theme, lang, failures);
			// HorizontalTimetable is a Shell-pushed page, not a flyout root:
			// the Shell flyout is unreachable here, so NavigateToSettings'
			// OpenFlyout would silently no-op (the iOS edge-drag does nothing)
			// and WaitForFlyoutItem('Shell.Flyout.Settings') would time out.
			// Pop back to DTAC, whose in-page MenuButton is OpenFlyout's
			// first and most reliable probe.
			ht.TapBack();
			Settle();
		}
		else
		{
			TestContext.Out.WriteLine(
				"horizontalTimetable: 横型時刻表 button not visible — screen skipped this run.");
		}

		// Reset the iOS interface-orientation mask before leaving DTAC.
		// ViewHost.UpdateOrientation() locks the process-wide mask to
		// Landscape while the timetable (VerticalView) tab is shown and never
		// resets it when navigating away (app bug — tracked separately). Both
		// the HT-visible and HT-skipped branches above leave DTAC on the
		// timetable tab, so without this every subsequent iPhone screen
		// (settings, and the next test case's StartHome) would be captured
		// rotated. Re-tapping the Hako tab drives the same code path that
		// flips the mask back to Portrait. Once the app bug is fixed this
		// reverts to a harmless redundant tab tap. ~900ms: the iOS16+
		// RequestGeometryUpdate → InvalidateMAUILayout round-trip is async.
		dtac.TabHako.Click();
		Thread.Sleep(900);

		// 11. Settings (EasterEgg) page. Reached from the DTAC view, which is
		//     flyout-aware (OpenFlyout's first probe is DTAC.MenuButton).
		_shell.NavigateToSettings();
		Settle();
		Capture("settings", theme, lang, failures);

		// Leave the app recoverable for the next test case's SetUp.
		_shell.NavigateToHome();

		// Home-mode leak guard: the walk above calls _start.LoadSample(),
		// which loads a loader and puts StartHome into Home mode. On iOS the
		// Appium session is shared assembly-wide (noReset:true), so an
		// uncleared loader leaks into every later fixture — in Home mode the
		// Start-mode StartHome.Title / ConnectServerButton are absent from the
		// accessibility tree, which is exactly why ConnectServerDialogTests
		// and LanguageSettingsTests would otherwise hit a 60 s Title
		// WaitForElement timeout downstream. Clear it here at the leak source
		// (seams are reachable on StartHome in both update and diff modes);
		// the downstream fixtures keep their own defensive ClearLoaderForTesting
		// as a belt-and-braces net for the failure path.
		_start.ClearLoaderForTesting();

		// Happy-path determinism reset: this fixture runs at Order(3) — very
		// early — so dozens of later fixtures in the assembly-shared iOS
		// session would otherwise inherit this case's English language,
		// frozen 09:41:00 clock and forced Light/Dark theme (some later
		// fixtures assert hard-coded Japanese captions or the system
		// appearance). Pin all three back to the app default here on
		// StartHome (seams reachable) for both update and diff modes;
		// TearDown carries a best-effort net for the failure path.
		_start.SetLanguageJapaneseForTesting();
		_start.UnfreezeClockForTesting();
		_start.ResetThemeForTesting();

		if (_updateMode)
		{
			TestContext.Out.WriteLine($"[{theme}/{lang}] baseline update complete.");
			return;
		}

		if (!GatedDeviceClasses.Contains(_deviceClass))
		{
			Assert.Ignore(
				$"deviceClass '{_deviceClass}' is captured for review but excluded " +
				$"from the pixel-diff gate (known WDA/runtime flakiness). " +
				$"{failures.Count} screen(s) differed (informational).");
		}

		Assert.That(failures, Is.Empty,
			$"[{_deviceClass}/{theme}/{lang}] {failures.Count} screen(s) differ from baseline:\n  "
			+ string.Join("\n  ", failures));
	}

	// Failure-path safety net for the Order(3) determinism leaks described at
	// the happy-path reset above: if a case throws after switching language /
	// freezing the clock / forcing a theme (before reaching the in-body
	// reset), pin all three back so a later fixture cannot inherit them. Each
	// in its own try/catch so one failing recovery tap does not skip the
	// others. Best-effort and after base.TearDown() so the real failure's
	// screenshot / page-source dump is captured first and a recovery-tap
	// exception never masks the result.
	public override void TearDown()
	{
		base.TearDown();

		try
		{
			_start?.SetLanguageJapaneseForTesting();
		}
		catch (Exception ex)
		{
			TestContext.Out.WriteLine(
				$"[ScreenshotRegression] TearDown language reset skipped: {ex.Message}");
		}

		try
		{
			_start?.UnfreezeClockForTesting();
		}
		catch (Exception ex)
		{
			TestContext.Out.WriteLine(
				$"[ScreenshotRegression] TearDown clock unfreeze skipped: {ex.Message}");
		}

		try
		{
			_start?.ResetThemeForTesting();
		}
		catch (Exception ex)
		{
			TestContext.Out.WriteLine(
				$"[ScreenshotRegression] TearDown theme reset skipped: {ex.Message}");
		}
	}

	// Per-screen settle for native page/modal swaps. Animations (modal
	// slide-in, tab content swap) finish well within this; the comparer
	// tolerance absorbs any residual jitter.
	private static void Settle() => Thread.Sleep(700);

	// Like Settle(), but additionally blocks until the framebuffer stops
	// changing (consecutive screenshots an interval apart are
	// byte-identical) or a hard cap elapses. The connect-server modal's
	// open animation + async PopulateHistory occasionally outlasts the
	// fixed Settle() window, so a capture can race a mid-animation frame
	// and produce a flaky ~29% diff (vs ~0.1% when settled). The simulator
	// clock + theme are frozen and the status bar is pinned via simctl
	// status_bar override, so a quiescent screen yields pixel-identical
	// frames; if PNG encoding were ever non-deterministic this degrades to
	// the bounded maxWait — still strictly better than the bare 700 ms.
	private void SettleUntilVisuallyStable(
		int maxWaitMs = 6000, int probeIntervalMs = 250, int requiredStableComparisons = 2)
	{
		Settle();
		byte[]? prev = null;
		int stable = 0;
		DateTime deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
		while (DateTime.UtcNow < deadline)
		{
			byte[] cur = Driver.GetScreenshot().AsByteArray;
			if (prev is not null && cur.AsSpan().SequenceEqual(prev))
			{
				if (++stable >= requiredStableComparisons)
					return;
			}
			else
			{
				stable = 0;
			}
			prev = cur;
			Thread.Sleep(probeIntervalMs);
		}
	}

	private void Capture(string screen, string theme, string lang, List<string> failures)
	{
		byte[] png = Driver.GetScreenshot().AsByteArray;

		string baseline = Path.Combine(
			ScreenshotsRoot(), _deviceClass, theme, lang, screen + ".png");
		string artifactDir = TestContext.CurrentContext.WorkDirectory;
		string artifactName = $"{_deviceClass}_{theme}_{lang}_{screen}";

		// Always persist + attach the frame (pass or fail) so the
		// Apple-guideline review has every screen from the CI artifacts.
		try
		{
			var path = Path.Combine(artifactDir, artifactName + ".png");
			File.WriteAllBytes(path, png);
			TestContext.AddTestAttachment(path, artifactName);
		}
		catch (Exception ex)
		{
			TestContext.Out.WriteLine($"{artifactName}: attach failed: {ex.Message}");
		}

		var result = ScreenshotComparer.CompareOrUpdate(
			png, baseline, artifactDir, artifactName, _updateMode);
		TestContext.Out.WriteLine(result.Message);
		if (!result.Match)
		{
			failures.Add(result.Message);
			// The comparer drops <name>.actual.png / <name>.diff.png in
			// artifactDir on a mismatch. NUnit's WorkDirectory is the
			// assembly dir, not TestResults — only *attached* files are
			// copied next to the .trx and picked up by the CI
			// **/TestResults/**/*.png upload glob, so attach them
			// explicitly or they never leave the runner.
			AttachIfPresent(Path.Combine(artifactDir, artifactName + ".diff.png"),
				artifactName + ".diff");
			AttachIfPresent(Path.Combine(artifactDir, artifactName + ".actual.png"),
				artifactName + ".actual");
		}
	}

	private static void AttachIfPresent(string path, string description)
	{
		try
		{
			if (File.Exists(path))
				TestContext.AddTestAttachment(path, description);
		}
		catch (Exception ex)
		{
			TestContext.Out.WriteLine($"attach '{description}' failed: {ex.Message}");
		}
	}

	// The test runs from TRViS.UITests/bin/<cfg>/<tfm>/; walk up to the
	// project directory (the one holding TRViS.UITests.csproj) and anchor
	// the committed baselines at <project>/Screenshots so update mode
	// writes them where git tracks them.
	private static string ScreenshotsRoot()
	{
		var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
		while (dir is not null &&
			!File.Exists(Path.Combine(dir.FullName, "TRViS.UITests.csproj")))
		{
			dir = dir.Parent;
		}
		if (dir is null)
			throw new InvalidOperationException(
				"Could not locate TRViS.UITests.csproj above the test directory.");
		return Path.Combine(dir.FullName, "Screenshots");
	}
}
