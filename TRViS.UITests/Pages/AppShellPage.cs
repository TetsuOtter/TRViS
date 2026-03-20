using OpenQA.Selenium.Appium;
using TRViS.UITests.Infrastructure;

namespace TRViS.UITests.Pages;

public class AppShellPage : PageObject
{
	public AppShellPage(AppiumDriver driver) : base(driver) { }

	public AppiumElement VersionLabel => FindByAutomationId(AutomationIds.Shell.VersionLabel);

	public void OpenFlyout()
	{
		// On most platforms, the flyout opens via swipe or a hamburger button.
		// Try to find the flyout toggle button; fall back to swipe.
		try
		{
			Driver.FindElement(MobileBy.AccessibilityId("Open navigation drawer")).Click();
		}
		catch (OpenQA.Selenium.NoSuchElementException)
		{
			// Swipe from left edge to open flyout
			var size = Driver.Manage().Window.Size;
			var startX = 5;
			var startY = size.Height / 2;
			var endX = size.Width / 2;
			Driver.ExecuteScript("mobile: swipe", new Dictionary<string, object>
			{
				{ "startX", startX },
				{ "startY", startY },
				{ "endX", endX },
				{ "endY", startY },
			});
		}
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
