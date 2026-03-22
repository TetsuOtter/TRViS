using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Support.UI;
using TRViS.UITests.Infrastructure;

namespace TRViS.UITests.Pages;

public class AppShellPage : PageObject
{
	public AppShellPage(AppiumDriver driver) : base(driver) { }

	public AppiumElement VersionLabel => FindByAutomationId(AutomationIds.Shell.VersionLabel);

	public void OpenFlyout()
	{
		// Suppress implicit wait for all fast probes in this method so we don't
		// block for 10 s on platforms that don't have a particular button.
		// Note: reading ImplicitWait via GET /session/.../timeouts is not implemented
		// by the Windows Appium driver, so we restore to the known default (10 s) directly.
		Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
		try
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
			try
			{
				Driver.FindElement(By.XPath(
					"//XCUIElementTypeNavigationBar//XCUIElementTypeButton[1]")).Click();
				return;
			}
			catch { }

			// Windows: WinUI 3 NavigationView is in Left mode when the window is wide
			// enough (≥ExpandedModeThresholdWidth = 1008 px). In Left mode, the pane is
			// always visible and no toggle action is needed.
			// If the pane is in LeftMinimal mode (window narrower than 641 px), we need
			// to click PaneToggleButton. We detect this by checking whether any flyout
			// item is already visible; if not, we click the toggle button.
			try
			{
				bool paneOpen = false;
				foreach (string selector in new[] {
					AutomationIds.Shell.Flyout.SelectTrain,
					AutomationIds.Shell.Flyout.DTAC })
				{
					try
					{
						var el = Driver.FindElement(MobileBy.AccessibilityId(selector));
						if (el.Displayed) { paneOpen = true; break; }
					}
					catch { }
				}

				if (!paneOpen)
				{
					Driver.FindElement(MobileBy.AccessibilityId("PaneToggleButton")).Click();
					Thread.Sleep(400); // Allow pane-open animation to complete
				}
				return;
			}
			catch { }
		}
		finally
		{
			Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
		}
	}

	/// <summary>
	/// Waits up to 30 s for a flyout item to appear.
	/// Tries AccessibilityId, then By.Name, then XPath (Windows ListItem fallback).
	/// Implicit wait is suppressed inside the loop so each probe is fast.
	/// Note: reading ImplicitWait via GET /session/.../timeouts is not implemented by
	/// the Windows Appium driver, so we restore to the known default (10 s) directly.
	/// On timeout, dumps the page source to test output for diagnosis.
	/// </summary>
	private AppiumElement WaitForFlyoutItem(string automationId, string title)
	{
		var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(30));
		Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
		try
		{
			return (AppiumElement)wait.Until(d =>
			{
				// AccessibilityId (iOS, macOS; Windows if MAUI propagates AutomationId)
				try
				{
					var el = d.FindElement(MobileBy.AccessibilityId(automationId));
					if (el.Displayed) return el;
				}
				catch { }

				// Name / UIA Name (all platforms; Windows NavigationViewItem content)
				try
				{
					var el = d.FindElement(By.Name(title));
					if (el.Displayed) return el;
				}
				catch { }

				// Windows XPath fallback: WinUI 3 NavigationViewItem maps to UIA ListItem
				try
				{
					var el = d.FindElement(By.XPath($"//ListItem[@Name='{title}']"));
					if (el.Displayed) return el;
				}
				catch { }

				// Broadest XPath fallback: any element with matching Name
				try
				{
					var el = d.FindElement(By.XPath($"//*[@Name='{title}']"));
					if (el.Displayed) return el;
				}
				catch { }

				return null!;
			});
		}
		catch (WebDriverTimeoutException)
		{
			// Dump page source so the accessibility tree is visible in CI logs.
			try
			{
				NUnit.Framework.TestContext.Out.WriteLine(
					$"[WaitForFlyoutItem] Timed out waiting for '{title}' ({automationId}). Page source:");
				NUnit.Framework.TestContext.Out.WriteLine(Driver.PageSource);
			}
			catch { }
			throw;
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
