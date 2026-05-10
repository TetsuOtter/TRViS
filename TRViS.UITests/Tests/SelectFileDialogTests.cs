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
	private StartHomePageObject _startHomePage = null!;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		_startHomePage = new StartHomePageObject(Driver);
		_startHomePage.AcceptPrivacyPolicyIfNeeded();
		// iOS noReset:true means TimetableFileDirectory persists across sessions,
		// and the FilePickerProvider override is a static that survives Driver.Quit().
		// Wipe both before every test so each starts from a known-clean state.
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
	/// "保存場所を開く" launches Files.app/Finder/Explorer/Android-launcher,
	/// which Appium can't follow into and which would wedge the next test if
	/// we tapped it. Just verify the button is reachable so a missing-button
	/// regression still fails.
	/// </summary>
	[Test]
	public void OpenStorageLocationButton_IsReachable()
	{
		var dialog = _startHomePage.OpenSelectFileDialog();
		Assert.That(dialog.OpenStorageLocationButton.Displayed, Is.True,
			"The OpenStorageLocation button should be reachable in both file-list and empty states.");
	}
}
