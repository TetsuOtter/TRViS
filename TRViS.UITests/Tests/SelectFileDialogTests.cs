using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// Verifies the Select-File modal (replaces the direct OS FilePicker behaviour).
/// The dialog has two visual states — rich-card list and empty-state — and a
/// drill-down folder model with a breadcrumb. Tests here exercise the in-app
/// surface plus the OS-FilePicker fallback path through the
/// <c>FilePickerProvider</c> seam, which lets the post-pick load path run
/// without driving the system picker UI (out of Appium's reach on every
/// platform we target). SQLite fixtures are intentionally not seeded — the
/// JSON path covers the same dispatch shape inside <c>TryLoadFileAsync</c>.
///
/// Every test wipes <c>TimetableFileDirectory</c> + clears the FilePicker
/// override in SetUp because iOS <c>noReset:true</c> keeps the documents
/// folder warm across sessions and the override static survives Driver.Quit().
/// </summary>
[TestFixture]
[Infrastructure.RetryAllTests(2)] // see AppLaunchTests for rationale
public class SelectFileDialogTests : BaseUITest
{
	// Share one Appium session across all tests in this fixture (iOS only).
	// See BaseUITest.ShareSessionAcrossTestsInFixture for details.
	protected override bool ShareSessionAcrossTestsInFixture => true;

	private StartHomePageObject _startHomePage = null!;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		_startHomePage = new StartHomePageObject(Driver);

		// Shared-session recovery: a prior test in this fixture may have
		// left the SelectFile dialog open (some tests assert state and
		// don't bother closing — cheaper here than amending every test).
		// Also handles the case where a prior test loaded a file and
		// navigated to DTAC. Use a fast PollDisplayed instead of
		// dialog.IsDisplayed() — the latter internally waits 30 s for
		// the Title element, blocking the [SetUp] for the full timeout
		// on the common "dialog not open" path.
		var dialog = new SelectFileDialogPageObject(Driver);
		if (dialog.PollDisplayed(AutomationIds.SelectFile.Title, timeoutSeconds: 1))
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
		// TimetableFileDirectory persists across the shared session, and
		// the FilePickerProvider override is a static that survives
		// Driver.Quit(). Wipe both before every test so each starts from
		// a known-clean state. ClearSampleFilesForTesting also wipes
		// TimetableFileDirectory, so it covers the SQLite-seed tests'
		// need for a clean start as well.
		_startHomePage.ClearSampleFilesForTesting();
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
		// "保存場所を開く" is hidden on Android by design — TimetableFileDirectory lives in
		// internal storage that no Files-app can browse, and `file://` URIs throw
		// FileUriExposedException on API 24+. See SelectFileDialog ctor for rationale.
		if (!IsAndroid)
		{
			Assert.That(dialog.OpenStorageLocationButton.Displayed, Is.True,
				"The 'open storage location' button should be reachable so the user can drop files in.");
		}
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

		// FileList no longer findable ⇒ modal popped ⇒ load succeeded.
		// FileList still findable ⇒ TryLoadFileAsync caught an exception, the
		// "読み込めませんでした" alert is up (Util.DisplayAlertAsync), dialog hasn't
		// been popped. That's the open failure mode the user reported.
		//
		// We deliberately don't probe StartHome.Title here even though that's
		// what "StartHomePage shown again" semantically means: on iPhone XCUITest
		// reports that label as `visible="false"` once the layout shifts to Home
		// mode (the title is visually rendered, but XCUITest's visibility
		// classification differs from "rendered on screen") — so the
		// _startHomePage.IsDisplayed() probe times out for 60 s waiting for a
		// `Displayed=true` Title that never registers as such. Probing the
		// SelectFile dialog instead is platform-uniform.
		Assert.That(dialog.IsFileListVisible(), Is.False,
			"After tapping the seeded SQLite card, the SelectFile dialog should dismiss. " +
			"If the file list is still visible, the load failed — most likely LoaderSQL.CreateAsync " +
			"threw inside the live MAUI runtime (open-flag / read-path issue), surfacing the " +
			"'読み込めませんでした' alert and keeping the dialog open.");
	}

	/// <summary>
	/// Seeded fixture: root has both a file and a sub-folder. The seam writes
	/// <c>ui-test-root.json</c> at the root and <c>ui-test-folder/ui-test-nested.json</c>.
	/// At the root we expect to see both cards and no breadcrumb.
	/// </summary>
	[Test]
	[Platform(Exclude = "Win", Reason = "MAUI inner Border children are not surfaced via WinUI UIA — same limitation as ConnectServer history cards.")]
	public void OpenDialog_WithSeededFixtures_ShowsFolderAndFileAtRoot()
	{
		Assume.That(_startHomePage.IsDisplayed(), Is.True);
		_startHomePage.SeedSampleFilesForTesting();
		Thread.Sleep(300);

		var dialog = _startHomePage.OpenSelectFileDialog();

		Assert.That(dialog.IsFileListVisible(), Is.True,
			"Seeded fixtures should switch the dialog to the file-list state.");
		Assert.That(dialog.IsBreadcrumbVisible(), Is.False,
			"At the root directory the breadcrumb should be hidden.");
		Assert.That(dialog.IsFolderItemVisible(StartHomePageObject.SeededSubFolderName), Is.True,
			$"Sub-folder card '{StartHomePageObject.SeededSubFolderName}' should be reachable by AutomationId.");
		Assert.That(dialog.IsFileItemVisible(StartHomePageObject.SeededRootFileName), Is.True,
			$"Root file card '{StartHomePageObject.SeededRootFileName}' should be reachable by AutomationId.");
	}

	/// <summary>
	/// Tap the folder card → drill in. Inside the sub-folder the nested file
	/// should appear, the up-folder card should be present, and the breadcrumb
	/// should reflect the new relative path.
	/// </summary>
	[Test]
	[Platform(Exclude = "Win", Reason = "MAUI inner Border children are not surfaced via WinUI UIA.")]
	public void TapFolder_DrillsIntoSubFolder_ShowsBreadcrumbAndUpCard()
	{
		Assume.That(_startHomePage.IsDisplayed(), Is.True);
		_startHomePage.SeedSampleFilesForTesting();
		Thread.Sleep(300);

		var dialog = _startHomePage.OpenSelectFileDialog();
		Assume.That(dialog.IsFolderItemVisible(StartHomePageObject.SeededSubFolderName), Is.True);

		dialog.TapFolderItem(StartHomePageObject.SeededSubFolderName);
		Thread.Sleep(400);

		Assert.That(dialog.IsBreadcrumbVisible(), Is.True,
			"After drilling into a sub-folder the breadcrumb should be shown.");
		Assert.That(dialog.Breadcrumb.Text, Does.Contain(StartHomePageObject.SeededSubFolderName),
			"Breadcrumb text should include the sub-folder name.");
		Assert.That(dialog.IsFileItemVisible(StartHomePageObject.SeededNestedFileName), Is.True,
			$"Nested file card '{StartHomePageObject.SeededNestedFileName}' should be visible after drill-down.");
		Assert.That(dialog.UpFolderItem.Displayed, Is.True,
			"Up-folder card should be visible when not at the root.");
	}

	/// <summary>
	/// Drill into a sub-folder, tap the up-folder card, expect to be back at
	/// the root: nested file gone, breadcrumb hidden, sub-folder card visible.
	/// </summary>
	[Test]
	[Platform(Exclude = "Win", Reason = "MAUI inner Border children are not surfaced via WinUI UIA.")]
	public void TapUpFolder_ReturnsToRoot()
	{
		Assume.That(_startHomePage.IsDisplayed(), Is.True);
		_startHomePage.SeedSampleFilesForTesting();
		Thread.Sleep(300);

		var dialog = _startHomePage.OpenSelectFileDialog();
		dialog.TapFolderItem(StartHomePageObject.SeededSubFolderName);
		Thread.Sleep(400);
		Assume.That(dialog.IsBreadcrumbVisible(), Is.True);

		dialog.TapUpFolder();
		Thread.Sleep(400);

		Assert.That(dialog.IsBreadcrumbVisible(), Is.False,
			"Back at root the breadcrumb should be hidden again.");
		Assert.That(dialog.IsFolderItemVisible(StartHomePageObject.SeededSubFolderName), Is.True,
			"Sub-folder card should be visible again at the root.");
		Assert.That(dialog.IsFileItemVisible(StartHomePageObject.SeededNestedFileName), Is.False,
			"Nested file card should not be visible at the root.");
	}

	/// <summary>
	/// Tap a file card → real load path runs → modal dismisses. The seeded
	/// fixture has one WorkGroup so a successful load transitions
	/// StartHomePage to Home mode (WorkGroupList visible) — asserting on that
	/// catches a regression where the dialog merely dismissed without setting
	/// the Loader.
	/// </summary>
	[Test]
	[Platform(Exclude = "Win", Reason = "MAUI inner Border children are not surfaced via WinUI UIA.")]
	public void TapFileCard_LoadsFileAndDismissesModal()
	{
		Assume.That(_startHomePage.IsDisplayed(), Is.True);
		_startHomePage.SeedSampleFilesForTesting();
		Thread.Sleep(300);

		var dialog = _startHomePage.OpenSelectFileDialog();
		Assume.That(dialog.IsFileItemVisible(StartHomePageObject.SeededRootFileName), Is.True);

		dialog.TapFileItem(StartHomePageObject.SeededRootFileName);

		// Successful load transitions Start→Home mode (TRANSITION_MS=380ms in
		// StartHomePage.xaml.cs). Poll instead of fixed-sleep so slow CI runners
		// (iOS macos-26 has been observed multi-second slow on layout) don't
		// flake out under RetryAllTests(2).
		Assert.That(_startHomePage.IsWorkGroupListVisible(timeoutSeconds: 10), Is.True,
			"After a successful load the modal should dismiss and StartHome should be in Home mode (WorkGroupList visible).");
	}

	/// <summary>
	/// Browse → OS FilePicker fallback. The seam writes a JSON outside
	/// TimetableFileDirectory and installs an override that returns its path,
	/// so the in-app load path runs without driving the OS picker dialog.
	/// </summary>
	[Test]
	public void BrowseButton_FollowsFilePickerOverride_LoadsAndDismisses()
	{
		Assume.That(_startHomePage.IsDisplayed(), Is.True);
		// Set up the override on StartHome before opening the modal — the
		// FilePickerProvider is a static so it doesn't matter which page is
		// active when the seam fires.
		_startHomePage.SetupBrowseFallbackForTesting();
		Thread.Sleep(200);

		var dialog = _startHomePage.OpenSelectFileDialog();
		Assume.That(dialog.IsEmptyStateVisible(), Is.True,
			"Empty state expected: TimetableFileDirectory was wiped in SetUp and the fallback file lives outside of it.");

		dialog.TapBrowse();

		// Override returns a valid file synchronously, then the dialog runs the
		// real load+dismiss path. Poll for the post-load Home-mode state for the
		// same reason as TapFileCard_LoadsFileAndDismissesModal.
		Assert.That(_startHomePage.IsWorkGroupListVisible(timeoutSeconds: 10), Is.True,
			"After the override returns a valid file the modal should dismiss and StartHome should be in Home mode.");
	}

	/// <summary>
	/// "保存場所を開く" launches Files.app/Finder/Explorer (or the Android
	/// equivalent on platforms where the timetable directory is reachable),
	/// which Appium can't follow into and which would wedge the next test if
	/// we tapped it. Just verify the button is reachable so a missing-button
	/// regression still fails.
	///
	/// Skipped on Android: the button is intentionally hidden there (see
	/// SelectFileDialog ctor — TimetableFileDirectory lives in internal
	/// storage that no Files-app can browse, and a `file://` URI would
	/// throw FileUriExposedException on API 24+).
	/// </summary>
	[Test]
	public void OpenStorageLocationButton_IsReachable()
	{
		var dialog = _startHomePage.OpenSelectFileDialog();
		if (IsAndroid)
		{
			Assume.That(dialog.IsDisplayed(), Is.True,
				"On Android the button is hidden by design — assert dialog reachability instead.");
			return;
		}
		Assert.That(dialog.OpenStorageLocationButton.Displayed, Is.True,
			"The OpenStorageLocation button should be reachable in both file-list and empty states.");
	}
}
