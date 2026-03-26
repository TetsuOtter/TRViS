using OpenQA.Selenium.Appium;
using TRViS.UITests.Infrastructure;

namespace TRViS.UITests.Pages;

public class ThirdPartyLicensesPageObject : PageObject
{
	public ThirdPartyLicensesPageObject(AppiumDriver driver) : base(driver) { }

	public AppiumElement LicenseList => WaitForElement(AutomationIds.ThirdParty.LicenseList);

	public bool IsDisplayed()
	{
		try
		{
			return LicenseList.Displayed;
		}
		catch (OpenQA.Selenium.NoSuchElementException)
		{
			return false;
		}
	}
}
