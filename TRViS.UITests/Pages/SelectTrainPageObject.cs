using OpenQA.Selenium.Appium;
using TRViS.UITests.Infrastructure;

namespace TRViS.UITests.Pages;

public class SelectTrainPageObject : PageObject
{
	public SelectTrainPageObject(AppiumDriver driver) : base(driver) { }

	// iPhone iOS simulator on macos-26 is markedly slower than the iPad variants —
	// the post-SaveAndAccept navigation from FirebaseSettingPage to SelectTrainPage
	// has been observed to take >30 s, so give Title's wait extra headroom.
	public AppiumElement Title => WaitForElement(AutomationIds.SelectTrain.Title, TimeSpan.FromSeconds(60));
	public AppiumElement LoadSampleButton => FindByAutomationId(AutomationIds.SelectTrain.LoadSampleButton);
	// Use WaitForElement for LoadFromWebButton because it may be temporarily not
	// findable while the popup is still dismissing back to this page.
	public AppiumElement LoadFromWebButton => WaitForElement(AutomationIds.SelectTrain.LoadFromWebButton);
	public AppiumElement SelectDatabaseButton => FindByAutomationId(AutomationIds.SelectTrain.SelectDatabaseButton);
	public AppiumElement WorkGroupList => FindByAutomationId(AutomationIds.SelectTrain.WorkGroupList);
	public AppiumElement WorkList => FindByAutomationId(AutomationIds.SelectTrain.WorkList);
	public AppiumElement TestSeedButton => FindByAutomationId(AutomationIds.SelectTrain.TestSeedButton);
	public AppiumElement TestSeedGpsButton => FindByAutomationId(AutomationIds.SelectTrain.TestSeedGpsButton);
	public AppiumElement TestSeedNextTrainSelectionButton => FindByAutomationId(AutomationIds.SelectTrain.TestSeedNextTrainSelectionButton);

	public bool IsDisplayed()
	{
		try
		{
			return Title.Displayed;
		}
		catch (OpenQA.Selenium.NoSuchElementException)
		{
			return false;
		}
	}

	public void LoadSample()
	{
		LoadSampleButton.Click();
	}

	/// <summary>
	/// Taps "Load from Web" and returns the popup page object.
	/// </summary>
	public SelectOnlineResourcePopupPageObject OpenLoadFromWebPopup()
	{
		LoadFromWebButton.Click();
		return new SelectOnlineResourcePopupPageObject(Driver);
	}

	/// <summary>
	/// Taps the DEBUG-only test-seed button so tests can populate URL history
	/// without typing through Appium SendKeys (flaky on iOS XCUITest).
	/// </summary>
	public void SeedUrlHistoryForTesting() => TestSeedButton.Click();

	/// <summary>
	/// Taps the DEBUG-only GPS-seed button so tests can push a fake GPS coord
	/// without typing through Appium SendKeys.
	/// </summary>
	public void SeedGpsLocationForTesting() => TestSeedGpsButton.Click();

	/// <summary>
	/// Taps the DEBUG-only seed button that selects a sample-data train whose
	/// NextTrainId is non-empty (linear-train-1 → NextTrainId = linear-train-2).
	/// </summary>
	public void SeedTrainSelectionWithNextTrain() => TestSeedNextTrainSelectionButton.Click();

	/// <summary>
	/// URLs that <see cref="SeedUrlHistoryForTesting"/> writes into history.
	/// Mirrors the literals in SelectTrainPage.xaml.cs so tests can assert against them.
	/// </summary>
	public static readonly string[] SeededHistoryUrls =
	{
		"https://example.com/timetable-a.json",
		"https://example.com/timetable-b.json",
	};

	/// <summary>
	/// Returns the count of WorkGroup rows after a sample load. Useful to verify
	/// "表示件数" against the known sample-data fixture.
	/// </summary>
	public int CountWorkGroups()
	{
		var list = WaitForElement(AutomationIds.SelectTrain.WorkGroupList);
		var children = list.FindElements(OpenQA.Selenium.By.XPath(".//*"));
		// On most platforms each row's outer cell wraps a Label with the work-group
		// name. Filter to elements whose text is non-empty so we don't double-count
		// child layout primitives (Grid/StackLayout).
		int count = 0;
		foreach (var el in children)
		{
			try
			{
				if (!string.IsNullOrEmpty(el.Text))
					count++;
			}
			catch { /* element went stale or text unavailable */ }
		}
		return count;
	}
}
