using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Support.UI;
using TRViS.UITests.Infrastructure;

namespace TRViS.UITests.Pages;

public class AppShellPage : PageObject
{
	private readonly bool _isWindows;

	public AppShellPage(AppiumDriver driver) : base(driver)
	{
		_isWindows = driver is WindowsDriver;
	}

	public AppiumElement VersionLabel => FindByAutomationId(AutomationIds.Shell.VersionLabel);

	/// <summary>
	/// Ordered list of flyout item titles as they appear in AppShell.xaml.
	/// Used by Windows keyboard navigation to calculate arrow-key presses.
	/// </summary>
	// Must mirror the FlyoutItem order in AppShell.xaml exactly: the Windows
	// keyboard-nav path derives the ArrowDown count from this index. Privacy /
	// Firebase / TPL flyout entries were removed (now reached from Home), so a
	// stale entry here would overshoot the target on Windows.
	private static readonly string[] FlyoutItemOrder = [
		"Home",
		"D-TAC",
		"Settings",
	];

	public void OpenFlyout()
	{
		// Suppress implicit wait for all fast probes in this method so we don't
		// block for 10 s on platforms that don't have a particular button.
		// Note: reading ImplicitWait via GET /session/.../timeouts is not implemented
		// by the Windows Appium driver, so we restore to the known default (10 s) directly.
		Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
		try
		{
			// DTAC.MenuButton (any platform): when we're on the DTAC view,
			// the in-page MenuButton toggles Shell.Current.FlyoutIsPresented
			// directly. Try this FIRST regardless of platform — on Android,
			// the standard "Open navigation drawer" probe matches an AppBar
			// button in DTAC that does NOT actually open the Shell flyout
			// (CI run 25686784110 / Android log surfaced a 30 s flyout-item
			// timeout because the wrong button was clicked). DTAC.MenuButton
			// is the only reliable flyout-toggle from DTAC on every platform.
			// Pages that don't host DTAC.MenuButton fall through to the
			// platform-specific probes below.
			try
			{
				var menu = Driver.FindElement(MobileBy.AccessibilityId(AutomationIds.DTAC.MenuButton));
				if (menu.Displayed)
				{
					menu.Click();
					return;
				}
			}
			catch { }

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

			// Windows: MAUI Shell forces NavigationView into LeftMinimal mode (pane
			// always collapsed behind a hamburger) regardless of window width.
			// The PaneToggleButton's UIA Name is "Open Navigation" when the pane is
			// closed (AutomationId is "OK" — not "PaneToggleButton" as expected).
			// Click the toggle to open the pane; if absent the pane is already open.
			try
			{
				var paneToggle = Driver.FindElement(By.XPath("//*[@Name='Open Navigation']"));
				if (paneToggle.Displayed)
				{
					paneToggle.Click();
					Thread.Sleep(400); // Allow pane-open animation to complete
				}
			}
			catch { }
			return;
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
				// AutomationId: AccessibilityId on iOS/macOS/Windows, resource-id on Android
				try
				{
					var el = d.FindElement(AutomationIdLocator(automationId));
					if (el.Displayed) return el;
				}
				catch { }

				// Name / UIA Name (all platforms; Windows NavigationViewItem content)
				try
				{
					var el = d.FindElement(MobileBy.Name(title));
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

	/// <summary>
	/// Sends key presses using the Windows Appium driver's native extension.
	/// The W3C Actions API is not supported by the Windows driver, so we use
	/// the 'windows: keys' extension command with virtual key codes.
	/// </summary>
	private void SendWindowsKeys(params int[] virtualKeyCodes)
	{
		var actions = new List<Dictionary<string, object>>();
		foreach (var vk in virtualKeyCodes)
		{
			actions.Add(new Dictionary<string, object>
			{
				{ "virtualKeyCode", vk },
				{ "down", true },
			});
			actions.Add(new Dictionary<string, object>
			{
				{ "pause", 50 },
			});
			actions.Add(new Dictionary<string, object>
			{
				{ "virtualKeyCode", vk },
				{ "down", false },
			});
			actions.Add(new Dictionary<string, object>
			{
				{ "pause", 100 },
			});
		}
		Driver.ExecuteScript("windows: keys", new Dictionary<string, object>
		{
			{ "actions", actions },
		});
	}

	// Virtual key codes for keyboard navigation
	private const int VK_TAB = 0x09;
	private const int VK_DOWN = 0x28;
	private const int VK_RETURN = 0x0D;

	/// <summary>
	/// Windows-specific: navigates to a flyout item using keyboard input.
	/// WinUI 3's NavigationView overlay pane auto-dismisses when the UIA driver
	/// performs tree traversal (FindElement calls). Keyboard navigation avoids
	/// this by sending input directly without UIA queries while the pane is open.
	/// Uses the 'windows: keys' extension instead of W3C Actions (not supported).
	/// </summary>
	private void NavigateViaKeyboard(string title)
	{
		int targetIndex = Array.IndexOf(FlyoutItemOrder, title);
		if (targetIndex < 0)
			throw new ArgumentException($"Unknown flyout item title: '{title}'");

		// The currently selected item is "Home" (index 0) after launch.
		// When the pane opens, the selected item gets keyboard focus.
		// ArrowDown moves through items sequentially.
		int arrowDownCount = targetIndex; // 0-based: Home=0, D-TAC=1, etc.

		for (int attempt = 0; attempt < 3; attempt++)
		{
			Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
			try
			{
				var toggle = Driver.FindElement(By.XPath("//*[@Name='Open Navigation']"));
				if (toggle.Displayed)
				{
					toggle.Click();
					Thread.Sleep(800); // Wait for pane-open animation

					// Build key sequence: Tab into pane, ArrowDown to target, Enter to select.
					var keys = new List<int> { VK_TAB };
					for (int i = 0; i < arrowDownCount; i++)
						keys.Add(VK_DOWN);
					keys.Add(VK_RETURN);

					SendWindowsKeys(keys.ToArray());
					Thread.Sleep(500); // Wait for navigation to complete
					return;
				}
			}
			catch (Exception ex)
			{
				NUnit.Framework.TestContext.Out.WriteLine(
					$"[NavigateViaKeyboard] Attempt {attempt + 1} failed: {ex.Message}");
			}
			finally
			{
				Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
			}
			Thread.Sleep(1000);
		}
		throw new InvalidOperationException($"Failed to navigate to '{title}' via keyboard after 3 attempts");
	}

	public DTACViewHostPageObject NavigateToDTAC()
	{
		if (_isWindows)
			NavigateViaKeyboard("D-TAC");
		else
		{
			OpenFlyout();
			WaitForFlyoutItem(AutomationIds.Shell.Flyout.DTAC, "D-TAC").Click();
		}
		return new DTACViewHostPageObject(Driver);
	}

	/// <summary>
	/// Navigates back to the StartHome page. Used between tests in fixtures
	/// that share a single Appium session — earlier tests may have left the
	/// app on DTAC, Settings, etc., and the next test expects to start from
	/// StartHome.
	///
	/// Tries the DTAC UI_TEST seam button first: when the app is on the DTAC
	/// view it issues Shell.Current.GoToAsync("//StartHomePage") directly,
	/// bypassing the flyout. The flyout was observed to be unreliable on
	/// Android when DTAC's VerticalView tab had locked orientation to
	/// Landscape (CI run 25727806170: MenuButton click dispatched 200 OK but
	/// the NavigationView never attached to the DrawerLayout, so
	/// WaitForFlyoutItem timed out 30 s later). For pages that don't host
	/// the seam, falls through to the flyout path (StartHome / Settings /
	/// ThirdParty all open the flyout fine since they don't lock orientation).
	///
	/// The seam probe uses the same plural-FindElements polling shape as
	/// PollDisplayed because UIAutomator2's accessibility tree needs ~1-5 s
	/// to repopulate after the previous test's TearDown (CI run 25729263553:
	/// a single FindElement with implicit=0 returned 404 at T+122 ms even
	/// though the seam was clearly in the page-source dump 30 s later; the
	/// tree was just stale at probe time). AutomationIdLocator is used
	/// instead of MobileBy.AccessibilityId so on Android the search goes
	/// through UiSelector.resourceId() rather than description() — code-added
	/// MAUI buttons get resource-id set but leave contentDescription null,
	/// which trips the description-first matcher.
	/// </summary>
	public StartHomePageObject NavigateToHome()
	{
		var seamLocator = AutomationIdLocator(AutomationIds.DTAC.TestNavigateHomeButton);
		Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
		try
		{
			var deadline = DateTime.UtcNow.AddSeconds(5);
			while (DateTime.UtcNow < deadline)
			{
				var elements = Driver.FindElements(seamLocator);
				if (elements.Count > 0)
				{
					try
					{
						if (elements[0].Displayed)
						{
							elements[0].Click();
							// Click returns when the tap dispatches, not when GoToAsync
							// completes. Wait for StartHome to appear so the next test's
							// PollDisplayed / ClearLoaderForTesting calls don't race
							// the navigation.
							new WebDriverWait(Driver, TimeSpan.FromSeconds(10))
								.Until(d => d.FindElements(AutomationIdLocator(AutomationIds.StartHome.Title)).Count > 0);
							return new StartHomePageObject(Driver);
						}
					}
					catch (StaleElementReferenceException) { /* retry */ }
				}
				Thread.Sleep(250);
			}
		}
		catch (WebDriverTimeoutException) { /* fall through to flyout */ }
		finally
		{
			Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
		}

		if (_isWindows)
			NavigateViaKeyboard("Home");
		else
		{
			OpenFlyout();
			WaitForFlyoutItem(AutomationIds.Shell.Flyout.StartHome, "Home").Click();
		}
		return new StartHomePageObject(Driver);
	}

	public EasterEggPageObject NavigateToSettings()
	{
		if (_isWindows)
			NavigateViaKeyboard("Settings");
		else
		{
			OpenFlyout();
			WaitForFlyoutItem(AutomationIds.Shell.Flyout.Settings, "Settings").Click();
		}
		return new EasterEggPageObject(Driver);
	}

}
