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
}
