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

		// iOS simulator: swipe from left edge.
		try
		{
			var size = Driver.Manage().Window.Size;
			Driver.ExecuteScript("mobile: swipe", new Dictionary<string, object>
			{
				{ "startX", 5 },
				{ "startY", size.Height / 2 },
				{ "endX", size.Width / 2 },
				{ "endY", size.Height / 2 },
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
		FindByAutomationId(AutomationIds.Shell.Flyout.SelectTrain).Click();
		return new SelectTrainPageObject(Driver);
	}

	public DTACViewHostPageObject NavigateToDTAC()
	{
		OpenFlyout();
		FindByAutomationId(AutomationIds.Shell.Flyout.DTAC).Click();
		return new DTACViewHostPageObject(Driver);
	}

	public ThirdPartyLicensesPageObject NavigateToThirdPartyLicenses()
	{
		OpenFlyout();
		FindByAutomationId(AutomationIds.Shell.Flyout.ThirdPartyLicenses).Click();
		return new ThirdPartyLicensesPageObject(Driver);
	}

	public EasterEggPageObject NavigateToSettings()
	{
		OpenFlyout();
		FindByAutomationId(AutomationIds.Shell.Flyout.Settings).Click();
		return new EasterEggPageObject(Driver);
	}

	public FirebaseSettingPageObject NavigateToFirebase()
	{
		OpenFlyout();
		FindByAutomationId(AutomationIds.Shell.Flyout.Firebase).Click();
		return new FirebaseSettingPageObject(Driver);
	}
}
