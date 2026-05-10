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
	/// Stage 1 of the SQLite-open reproduction: did the in-app seed seam manage
	/// to write a SQLite file at all? On every platform the seam runs the same
	/// sqlite-net write path that LoaderSQL reads from. If MAUI's linker/AOT
	/// stripped the SQLitePCLRaw provider registration (or Batteries_V2.Init
	/// was never called), the seam throws and no file appears — the dialog
	/// then renders empty state instead of the file list and this test fails.
	///
	/// netcore-based TRViS.IO.Tests cannot reach this failure mode because
	/// they don't go through MAUI's linker/AOT, so the repro must live here.
	/// </summary>
	[Test]
	public void SeededSqlite_AppearsInFileListView()
	{
		Assume.That(_startHomePage.IsDisplayed(), Is.True);

		_startHomePage.SeedSqliteForTesting();
		// Brief settle so the seed write completes before we open the dialog
		// (the dialog enumerates files synchronously in OnAppearing).
		Thread.Sleep(500);

		var dialog = _startHomePage.OpenSelectFileDialog();
		Assert.That(dialog.IsDisplayed(), Is.True, "Dialog should be displayed.");

		// File list visible ⇒ seed wrote a file ⇒ MAUI runtime can use sqlite-net.
		// Empty state visible ⇒ seed threw inside SQLiteConnection ctor ⇒ this is
		// the "SQLite open shows error" production bug — most common fix is
		// SQLitePCL.Batteries_V2.Init() in MauiProgram.CreateMauiApp().
		Assert.That(dialog.IsFileListVisible(), Is.True,
			"After seeding a SQLite fixture the dialog should render the file list, " +
			"but the empty state is showing. The in-app seed step likely threw — " +
			"this is the production bug. Apply SQLitePCL.Batteries_V2.Init() in " +
			"MauiProgram.CreateMauiApp() and re-run.");
	}

	/// <summary>
	/// Stage 2: card is reachable and tapping it loads the loader through
	/// LoaderSQL.CreateAsync without throwing. Excluded on Windows because
	/// MAUI does not expose the Border-based file card via UIA there
	/// (separate quirk; the file is on disk per the screenshot — it's the
	/// per-card AutomationId that's not queryable). Stage 1 covers the
	/// SQLite repro on every platform; this stage covers the read-side
	/// open-flag regression on platforms where the card is reachable.
	/// </summary>
	[Test]
	[Platform(Exclude = "Win", Reason = "Windows MAUI does not expose the dynamically-created Border (file card) via the UIA tree — the file is on disk and visually present (verified via screenshot artifact), but the per-card AutomationId cannot be looked up. The complementary SeededSqlite_AppearsInFileListView already covers the SQLite-init repro on Windows.")]
	public void SeededSqlite_TappingCard_LoadsAndDismissesDialog()
	{
		Assume.That(_startHomePage.IsDisplayed(), Is.True);

		_startHomePage.SeedSqliteForTesting();
		Thread.Sleep(500);

		var dialog = _startHomePage.OpenSelectFileDialog();
		Assert.That(dialog.IsDisplayed(), Is.True, "Dialog should be displayed.");

		// If this fails, Stage 1 (SeededSqlite_AppearsInFileListView) should
		// have failed first with a clearer message. Surfacing the seed-side
		// problem here only as a backup.
		var fileItem = dialog.FileItem(StartHomePageObject.UITestSqliteFixtureFileName);
		Assert.That(fileItem.Displayed, Is.True,
			$"Seeded SQLite '{StartHomePageObject.UITestSqliteFixtureFileName}' should appear as a card.");

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
			"threw inside the live MAUI runtime (open-flag / read-path issue).");
	}
}
