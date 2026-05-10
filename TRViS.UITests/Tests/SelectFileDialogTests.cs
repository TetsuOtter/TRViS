using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// Verifies the Select-File modal (replaces the direct OS FilePicker behaviour).
/// On a clean install the app's documents folder is empty, so the dialog renders
/// the empty state with the OS-FilePicker button as the primary action. Tests
/// here only verify the in-app surface — the OS FilePicker itself is system UI
/// and out of Appium's reach.
/// </summary>
[TestFixture]
[Infrastructure.RetryAllTests(2)] // see AppLaunchTests for rationale
public class SelectFileDialogTests : BaseUITest
{
	private StartHomePageObject _startHomePage = null!;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		_startHomePage = new StartHomePageObject(Driver);
		_startHomePage.AcceptPrivacyPolicyIfNeeded();

		// Mac Catalyst / iOS keep the app documents folder across noReset:true
		// sessions, so a SQLite seed left by an earlier test can pollute the
		// "clean install" assertions below. Wipe explicitly via the in-app seam
		// — portable across all platforms, no need for platform-specific path
		// knowledge in ResetAppState.
		_startHomePage.ClearTimetablesForTesting();
	}

	[Test]
	public void OpenDialog_OnCleanInstall_ShowsEmptyStateWithBrowseButton()
	{
		// Clean install: TimetableFileDirectory has no JSON/SQLite files. The dialog
		// should render the empty-state message and keep the OS-FilePicker button as
		// the primary action.
		Assume.That(_startHomePage.IsDisplayed(), Is.True);

		var dialog = _startHomePage.OpenSelectFileDialog();

		Assert.That(dialog.IsDisplayed(), Is.True, "Dialog should be displayed.");
		Assert.That(dialog.IsEmptyStateVisible(), Is.True,
			"With no files, the dialog should default to the empty state.");
		Assert.That(dialog.BrowseButton.Displayed, Is.True,
			"The browse button should remain visible in the empty state.");
		Assert.That(dialog.OpenStorageLocationButton.Displayed, Is.True,
			"The 'open storage location' button should be reachable so the user can drop files in.");
	}

	[Test]
	public void Close_ReturnsToStartHomePage()
	{
		var dialog = _startHomePage.OpenSelectFileDialog();
		Assert.That(dialog.IsDisplayed(), Is.True);

		var back = dialog.Close();
		Thread.Sleep(300);
		Assert.That(back.IsDisplayed(), Is.True,
			"After Close the StartHomePage should be visible again.");
	}

	/// <summary>
	/// Reproduction + regression test for the SQLite-open path in the live MAUI
	/// runtime. Two failure modes this catches:
	///
	///   (1) sqlite-net write fails inside the seed seam — usually because the
	///       SQLitePCLRaw bundle_green provider registration was stripped by the
	///       linker or never initialized (missing SQLitePCL.Batteries_V2.Init
	///       in MauiProgram). No file ends up in TimetableFileDirectory, so the
	///       dialog renders empty state and the FileItem lookup throws
	///       NoSuchElementException — this assertion fails clearly.
	///
	///   (2) Seed succeeds but LoaderSQL.CreateAsync fails on the open — e.g.
	///       a regression in the open-flag combination. Card appears, tap fires
	///       the load, the catch in TryLoadFileAsync raises a "読み込めませんでした"
	///       alert, and the dialog stays open. The post-tap assertion that the
	///       dialog has dismissed catches this.
	///
	/// netcore-based TRViS.IO.Tests cannot reach either failure mode because
	/// they don't go through MAUI's linker/AOT — the test must live here.
	/// </summary>
	[Test]
	public void SeededSqlite_TappingCard_LoadsAndDismissesDialog()
	{
		Assume.That(_startHomePage.IsDisplayed(), Is.True);

		_startHomePage.SeedSqliteForTesting();
		// Brief settle so the seed write completes before we open the dialog
		// (the dialog enumerates files synchronously in OnAppearing).
		Thread.Sleep(500);

		var dialog = _startHomePage.OpenSelectFileDialog();
		Assert.That(dialog.IsDisplayed(), Is.True, "Dialog should be displayed.");

		// Card visible ⇒ seed succeeded ⇒ MAUI runtime can write SQLite.
		// Card missing ⇒ seed threw inside SQLiteConnection ctor ⇒ this is
		// almost certainly the "SQLite open shows error" production bug.
		var fileItem = dialog.FileItem(StartHomePageObject.UITestSqliteFixtureFileName);
		Assert.That(fileItem.Displayed, Is.True,
			$"Seeded SQLite '{StartHomePageObject.UITestSqliteFixtureFileName}' should appear as a card. " +
			"If this assertion fails, the seed step inside the app could not write the file — " +
			"check device logs for an exception in TestSeedSqliteButton_Clicked.");

		fileItem.Click();
		// Dialog dismisses on successful load. Generous wait because the load is
		// async (Task.Run on the threadpool) plus the modal pop animation.
		Thread.Sleep(1500);

		// StartHomePage visible again ⇒ modal popped ⇒ load succeeded.
		// StartHomePage NOT visible ⇒ TryLoadFileAsync caught an exception, the
		// "読み込めませんでした" alert is up (Util.DisplayAlertAsync), dialog hasn't
		// been popped. That's the open failure mode the user reported.
		Assert.That(_startHomePage.IsDisplayed(), Is.True,
			"After tapping the seeded SQLite card, the dialog should dismiss back to StartHomePage. " +
			"If StartHomePage is not visible, the load failed — most likely LoaderSQL.CreateAsync " +
			"threw inside the live MAUI runtime (provider registration / open-flag issue).");
	}
}
