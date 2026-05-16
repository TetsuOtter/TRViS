using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

using TRViS.UITests.Infrastructure;

namespace TRViS.UITests.Pages;

/// <summary>
/// Page object for the Select-File modal (replaces the direct OS FilePicker behaviour).
/// Two visual states: a rich-card list of files in the app's documents folder, and an
/// empty state shown when no JSON/SQLite files are present. Both states share the
/// "他の場所からファイルを開く" footer button which invokes the OS FilePicker.
/// </summary>
public class SelectFileDialogPageObject : PageObject
{
	public SelectFileDialogPageObject(AppiumDriver driver) : base(driver) { }

	public AppiumElement Title => WaitForElement(AutomationIds.SelectFile.Title);
	public AppiumElement CloseButton => FindByAutomationId(AutomationIds.SelectFile.CloseButton);

	// File list state
	public AppiumElement FileList => FindByAutomationId(AutomationIds.SelectFile.FileList);
	public AppiumElement Breadcrumb => FindByAutomationId(AutomationIds.SelectFile.Breadcrumb);
	public AppiumElement UpFolderItem => FindByAutomationId(AutomationIds.SelectFile.UpFolderItem);

	// Empty state
	public AppiumElement EmptyMessage => FindByAutomationId(AutomationIds.SelectFile.EmptyMessage);

	// Footer actions. BrowseButton is always present; OpenStorageLocationButton is
	// hidden on Android (internal-storage limitation) — guard accesses with !IsAndroid
	// or FindByAutomationId will throw NoSuchElementException.
	public AppiumElement BrowseButton => FindByAutomationId(AutomationIds.SelectFile.BrowseButton);
	public AppiumElement OpenStorageLocationButton => FindByAutomationId(AutomationIds.SelectFile.OpenStorageLocationButton);

	public bool IsDisplayed()
	{
		try
		{
			return Title.Displayed;
		}
		catch (NoSuchElementException)
		{
			return false;
		}
	}

	/// <summary>
	/// Returns true when the file list is the active sub-view (i.e. at least one
	/// supported file exists in the app's documents folder).
	///
	/// Probes the <see cref="AutomationIds.SelectFile.FileListHint"/> Label
	/// rather than the ScrollView's <see cref="AutomationIds.SelectFile.FileList"/>:
	/// Android's UiAutomator2 doesn't surface a ScrollView's AutomationId
	/// reliably, so probing FileList directly returns false even when the
	/// list is on screen. Labels surface consistently on every platform.
	///
	/// Polls (rather than zero-wait probes) so a probe that fires immediately
	/// after the modal-push race doesn't get a false negative before the
	/// dialog finishes rendering.
	/// </summary>
	public bool IsFileListVisible()
		=> PollDisplayed(AutomationIds.SelectFile.FileListHint);

	/// <summary>
	/// Returns true when the empty state is the active sub-view. Polls for the
	/// same modal-push-race reason as <see cref="IsFileListVisible"/>.
	/// </summary>
	public bool IsEmptyStateVisible()
		=> PollDisplayed(AutomationIds.SelectFile.EmptyMessage);

	/// <summary>
	/// Returns true when the breadcrumb (current relative path) is shown — i.e.
	/// the dialog is drilled into a sub-folder. Returns false at the root or if
	/// the dialog isn't yet rendered. Uses a short timeout so a "still at root"
	/// assertion doesn't pay the full poll budget waiting for an element that
	/// is intentionally hidden.
	/// </summary>
	public bool IsBreadcrumbVisible(double timeoutSeconds = 1)
		=> PollDisplayed(AutomationIds.SelectFile.Breadcrumb, timeoutSeconds);

	/// <summary>
	/// Returns true when a sub-folder card with <paramref name="folderName"/> is
	/// visible. Polls so post-navigation re-renders aren't lost to the layout
	/// race, and short timeout so a "no longer present" branch is quick.
	/// </summary>
	public bool IsFolderItemVisible(string folderName, double timeoutSeconds = 1)
		=> PollDisplayed(AutomationIds.SelectFile.FolderItemPrefix + folderName, timeoutSeconds);

	/// <summary>
	/// Returns true when a file card with <paramref name="fileName"/> is
	/// visible. Same polling rationale as <see cref="IsFolderItemVisible"/>.
	/// </summary>
	public bool IsFileItemVisible(string fileName, double timeoutSeconds = 1)
		=> PollDisplayed(AutomationIds.SelectFile.FileItemPrefix + fileName, timeoutSeconds);

	/// <summary>
	/// Returns the per-row card element for <paramref name="fileName"/>. The whole
	/// card is tappable — selecting it loads the file and dismisses the dialog on
	/// success.
	/// </summary>
	public AppiumElement FileItem(string fileName)
		=> WaitForElement(AutomationIds.SelectFile.FileItemPrefix + fileName);

	public void TapFileItem(string fileName) => FileItem(fileName).Click();

	/// <summary>
	/// Returns the per-row card element for a sub-folder. Tapping it drills into
	/// the folder (replaces the list with that folder's contents).
	/// </summary>
	public AppiumElement FolderItem(string folderName)
		=> WaitForElement(AutomationIds.SelectFile.FolderItemPrefix + folderName);

	public void TapFolderItem(string folderName) => FolderItem(folderName).Click();

	/// <summary>
	/// Taps the "上の階層へ" card. Only present when not at the root directory.
	/// </summary>
	public void TapUpFolder() => UpFolderItem.Click();

	/// <summary>
	/// Taps the "他の場所からファイルを開く" button which invokes the OS FilePicker.
	/// The system FilePicker is out of Appium's reach on most platforms, so tests
	/// generally tap this only to verify the button is reachable / firing.
	/// </summary>
	public void TapBrowse() => BrowseButton.Click();

	/// <summary>
	/// Taps the "保存場所を開く" button which launches the OS file manager
	/// at the app's documents folder. The launched app is system UI and out of
	/// Appium's reach, so tests generally only assert reachability.
	/// </summary>
	public void TapOpenStorageLocation() => OpenStorageLocationButton.Click();

	/// <summary>
	/// Polls for the friendly load-error alert (issue #49) and dismisses it,
	/// returning <c>true</c> if one was found within <paramref name="timeoutSeconds"/>.
	/// The alert is raised asynchronously after JsonSerializer throws, so a
	/// fixed sleep races on slow CI — poll instead. Mirrors the cross-platform
	/// accept pattern used by StartHomePageObject / FirebaseSettingPageObject:
	/// the W3C alert endpoint first, then the iOS/Mac sheet/alert OK button.
	///
	/// Returning a bool lets the test positively assert the alert appeared
	/// (proving the #49 friendly-error path) without scraping platform-specific
	/// alert text — the exact wording is covered by TRViS.IO.Tests. Dismissing
	/// it here also unwedges the shared Appium session so a later assertion
	/// failure can't leave a modal alert stacked over the dialog.
	/// </summary>
	public bool DismissErrorAlert(double timeoutSeconds = 10)
	{
		DateTime deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
		while (DateTime.UtcNow < deadline)
		{
			try
			{
				Driver.SwitchTo().Alert().Accept();
				return true;
			}
			catch (NoAlertPresentException) { }
			catch (WebDriverException)
			{
				// Driver without the W3C alert endpoint (e.g. mac2): the
				// DisplayAlert renders as a sheet/alert element instead.
				try
				{
					Driver.FindElement(By.XPath(
						"//XCUIElementTypeSheet//XCUIElementTypeButton[@label='OK']" +
						" | //XCUIElementTypeAlert//XCUIElementTypeButton[@label='OK']"
					)).Click();
					return true;
				}
				catch (NoSuchElementException) { }
				catch (WebDriverException) { }
			}
			Thread.Sleep(300);
		}
		return false;
	}

	/// <summary>
	/// Closes the dialog and returns to StartHomePage.
	/// </summary>
	public StartHomePageObject Close()
	{
		CloseButton.Click();
		return new StartHomePageObject(Driver);
	}
}
