using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using TRViS.UITests.Infrastructure;

namespace TRViS.UITests.Pages;

public class AppShellPage : PageObject
{
	public AppShellPage(AppiumDriver driver) : base(driver) { }

	public AppiumElement VersionLabel => FindByAutomationId(AutomationIds.Shell.VersionLabel);

	public void OpenFlyout()
	{
		// Android: standard hamburger "navigation drawer" button.
		try
		{
			Driver.FindElement(MobileBy.AccessibilityId("Open navigation drawer")).Click();
			return;
		}
		catch (OpenQA.Selenium.NoSuchElementException) { }

		// iOS simulator: drag from left edge to open flyout.
		// Appium 3.x XCUITest driver replaced the old startX/endX swipe API with
		// mobile: dragFromToForDuration which takes fromX/fromY/toX/toY/duration.
		try
		{
			var size = Driver.Manage().Window.Size;
			Driver.ExecuteScript("mobile: dragFromToForDuration", new Dictionary<string, object>
			{
				{ "fromX", 5.0 },
				{ "fromY", (double)size.Height / 2 },
				{ "toX", (double)size.Width / 2 },
				{ "toY", (double)size.Height / 2 },
				{ "duration", 0.5 },
			});
			return;
		}
		catch { }

		// Mac Catalyst (mac2): the flyout toggle is a button in the navigation bar.
		// Try finding it by XPath type, then by its accessibility ID set on the Shell.
		try
		{
			Driver.FindElement(By.XPath(
				"//XCUIElementTypeNavigationBar//XCUIElementTypeButton[1]")).Click();
			return;
		}
		catch { }

		// Last resort: if the sidebar is always visible on this platform, no action needed.
	}

	public SelectTrainPageObject NavigateToSelectTrain()
	{
		OpenFlyout();
		WaitForElement(AutomationIds.Shell.Flyout.SelectTrain).Click();
		return new SelectTrainPageObject(Driver);
	}

	public DTACViewHostPageObject NavigateToDTAC()
	{
		OpenFlyout();
		WaitForElement(AutomationIds.Shell.Flyout.DTAC).Click();
		return new DTACViewHostPageObject(Driver);
	}

	public ThirdPartyLicensesPageObject NavigateToThirdPartyLicenses()
	{
		OpenFlyout();
		WaitForElement(AutomationIds.Shell.Flyout.ThirdPartyLicenses).Click();
		return new ThirdPartyLicensesPageObject(Driver);
	}

	public EasterEggPageObject NavigateToSettings()
	{
		OpenFlyout();
		WaitForElement(AutomationIds.Shell.Flyout.Settings).Click();
		return new EasterEggPageObject(Driver);
	}

	public FirebaseSettingPageObject NavigateToFirebase()
	{
		OpenFlyout();
		WaitForElement(AutomationIds.Shell.Flyout.Firebase).Click();
		return new FirebaseSettingPageObject(Driver);
	}
}
