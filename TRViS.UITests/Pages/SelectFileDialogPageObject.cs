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

	// Always-visible footer actions
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
	/// </summary>
	public bool IsFileListVisible()
	{
		var prevWait = TimeSpan.FromSeconds(10);
		try
		{
			Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
			return FindByAutomationId(AutomationIds.SelectFile.FileList).Displayed;
		}
		catch
		{
			return false;
		}
		finally
		{
			Driver.Manage().Timeouts().ImplicitWait = prevWait;
		}
	}

	/// <summary>
	/// Returns true when the empty state is the active sub-view.
	/// </summary>
	public bool IsEmptyStateVisible()
	{
		var prevWait = TimeSpan.FromSeconds(10);
		try
		{
			Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
			return FindByAutomationId(AutomationIds.SelectFile.EmptyMessage).Displayed;
		}
		catch
		{
			return false;
		}
		finally
		{
			Driver.Manage().Timeouts().ImplicitWait = prevWait;
		}
	}

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
	/// Closes the dialog and returns to StartHomePage.
	/// </summary>
	public StartHomePageObject Close()
	{
		CloseButton.Click();
		return new StartHomePageObject(Driver);
	}
}
