using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Support.UI;
using TRViS.UITests.Infrastructure;

namespace TRViS.UITests.Pages;

public class ThirdPartyLicensesPageObject : PageObject
{
	public ThirdPartyLicensesPageObject(AppiumDriver driver) : base(driver) { }

	public AppiumElement LicenseList => WaitForElement(AutomationIds.ThirdParty.LicenseList);

	// Visible only when hosted as a modal (StartHomePage footer link). The
	// flyout entry was removed once Home gained the footer link, so this is
	// now the only way TPL is reached.
	public AppiumElement ModalCloseButton => WaitForElement(AutomationIds.ThirdParty.ModalCloseButton);

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

	/// <summary>
	/// Taps the modal close (X) icon and waits until the license list is gone,
	/// returning to StartHomePage.
	/// </summary>
	public StartHomePageObject CloseModal()
	{
		ModalCloseButton.Click();
		new WebDriverWait(Driver, TimeSpan.FromSeconds(10))
			.Until(d => d.FindElements(AutomationIdLocator(AutomationIds.ThirdParty.LicenseList)).Count == 0);
		return new StartHomePageObject(Driver);
	}
}
