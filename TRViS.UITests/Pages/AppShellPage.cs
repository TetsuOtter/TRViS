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

		// Windows: WinUI 3 NavigationView pane toggle button ("PaneToggleButton" is its
		// default x:Name / AutomationId). Only present when the pane is in LeftMinimal mode
		// (narrow window). Suppress the implicit wait so we don't block for 10 s if the
		// pane is already open in Left (always-visible) mode.
		// Note: reading ImplicitWait via GET /session/.../timeouts is not implemented by
		// the Windows Appium driver, so we restore to the known default (10 s) directly.
		try
		{
			Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
			try
			{
				Driver.FindElement(MobileBy.AccessibilityId("PaneToggleButton")).Click();
				Thread.Sleep(300); // Allow pane-open animation to complete
			}
			finally
			{
				Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
			}
			return;
		}
		catch { }

		// Last resort: if the sidebar is always visible on this platform, no action needed.
	}

	/// <summary>
	/// Waits up to 30 s for a flyout item to appear. Tries AccessibilityId first;
	/// falls back to Name (title text) in case the platform does not propagate
	/// AutomationId to the underlying navigation control (e.g. WinUI NavigationViewItem).
	/// The implicit wait is suppressed inside the loop so each probe is fast.
	/// </summary>
	/// <summary>
	/// Waits up to 30 s for a flyout item to appear. Tries AccessibilityId first;
	/// falls back to Name (title text) in case the platform does not propagate
	/// AutomationId to the underlying navigation control (e.g. WinUI NavigationViewItem).
	/// Sets implicit wait to zero inside the loop so each probe is fast.
	/// Note: reading ImplicitWait via GET /session/.../timeouts is not implemented by
	/// the Windows Appium driver, so we restore to the known default (10 s) directly.
	/// </summary>
	private AppiumElement WaitForFlyoutItem(string automationId, string title)
	{
		var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(Driver, TimeSpan.FromSeconds(30));
		Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
		try
		{
			return (AppiumElement)wait.Until(d =>
			{
				try
				{
					var el = d.FindElement(MobileBy.AccessibilityId(automationId));
					if (el.Displayed) return el;
				}
				catch { }

				try
				{
					var el = d.FindElement(By.Name(title));
					if (el.Displayed) return el;
				}
				catch { }

				return null!;
			});
		}
		finally
		{
			Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
		}
	}

	public SelectTrainPageObject NavigateToSelectTrain()
	{
		OpenFlyout();
		WaitForFlyoutItem(AutomationIds.Shell.Flyout.SelectTrain, "Select Train").Click();
		return new SelectTrainPageObject(Driver);
	}

	public DTACViewHostPageObject NavigateToDTAC()
	{
		OpenFlyout();
		WaitForFlyoutItem(AutomationIds.Shell.Flyout.DTAC, "D-TAC").Click();
		return new DTACViewHostPageObject(Driver);
	}

	public ThirdPartyLicensesPageObject NavigateToThirdPartyLicenses()
	{
		OpenFlyout();
		WaitForFlyoutItem(AutomationIds.Shell.Flyout.ThirdPartyLicenses, "Third Party Licenses").Click();
		return new ThirdPartyLicensesPageObject(Driver);
	}

	public EasterEggPageObject NavigateToSettings()
	{
		OpenFlyout();
		WaitForFlyoutItem(AutomationIds.Shell.Flyout.Settings, "Settings").Click();
		return new EasterEggPageObject(Driver);
	}

	public FirebaseSettingPageObject NavigateToFirebase()
	{
		OpenFlyout();
		WaitForFlyoutItem(AutomationIds.Shell.Flyout.Firebase, "Firebase Setting").Click();
		return new FirebaseSettingPageObject(Driver);
	}
}
