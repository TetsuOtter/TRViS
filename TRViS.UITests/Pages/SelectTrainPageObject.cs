using OpenQA.Selenium.Appium;
using TRViS.UITests.Infrastructure;

namespace TRViS.UITests.Pages;

public class SelectTrainPageObject : PageObject
{
	public SelectTrainPageObject(AppiumDriver driver) : base(driver) { }

	public AppiumElement Title => WaitForElement(AutomationIds.SelectTrain.Title);
	public AppiumElement LoadSampleButton => FindByAutomationId(AutomationIds.SelectTrain.LoadSampleButton);
	public AppiumElement LoadFromWebButton => FindByAutomationId(AutomationIds.SelectTrain.LoadFromWebButton);
	public AppiumElement SelectDatabaseButton => FindByAutomationId(AutomationIds.SelectTrain.SelectDatabaseButton);
	public AppiumElement WorkGroupList => FindByAutomationId(AutomationIds.SelectTrain.WorkGroupList);
	public AppiumElement WorkList => FindByAutomationId(AutomationIds.SelectTrain.WorkList);

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
}
