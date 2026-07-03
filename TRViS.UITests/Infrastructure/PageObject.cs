using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace TRViS.UITests.Infrastructure;

public abstract class PageObject
{
	protected AppiumDriver Driver { get; }

	/// <summary>
	/// True when running on Android. On Android, MAUI maps AutomationId to
	/// the view's resource-id (found via By.Id), not to content-description
	/// (found via MobileBy.AccessibilityId).
	/// </summary>
	protected bool IsAndroid { get; }

	/// <summary>
	/// True when running on Windows. WinUI 3 surfaces a MAUI <c>ContentView</c>'s
	/// AutomationId as a non-control Pane element that Appium's AccessibilityId
	/// search does not always reach — callers may need an XPath/Name fallback.
	/// </summary>
	protected bool IsWindows { get; }

	protected PageObject(AppiumDriver driver)
	{
		Driver = driver;
		IsAndroid = driver is AndroidDriver;
		IsWindows = driver is WindowsDriver;
	}

	/// <summary>
	/// Finds an element by MAUI AutomationId. Uses By.Id on Android
	/// (resource-id) and MobileBy.AccessibilityId on all other platforms.
	/// </summary>
	protected AppiumElement FindByAutomationId(string automationId)
		=> (AppiumElement)Driver.FindElement(AutomationIdLocator(automationId));

	/// <summary>
	/// Returns the correct locator strategy for the current platform.
	/// </summary>
	protected By AutomationIdLocator(string automationId)
		=> IsAndroid ? By.Id(automationId) : MobileBy.AccessibilityId(automationId);

	public AppiumElement WaitForElement(string automationId, TimeSpan? timeout = null)
	{
		var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(
			Driver,
			timeout ?? TimeSpan.FromSeconds(30));

		var locator = AutomationIdLocator(automationId);

		// Suppress implicit wait so the polling loop fires every ~500 ms instead
		// of every 10 s. Restoring in finally keeps the session state consistent.
		Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
		try
		{
			// FindElements (plural) returns an empty list on no-match — so
			// every poll iteration that hasn't found the element yet completes
			// as a 200 OK on the wire instead of a NoSuchElement 404. Same
			// observable behaviour as the previous FindElement+catch form, but
			// the Appium server log stays clean while the element is loading.
			return (AppiumElement)wait.Until(d =>
			{
				var elements = d.FindElements(locator);
				if (elements.Count == 0)
					return null!;
				try
				{
					return elements[0].Displayed ? elements[0] : null!;
				}
				catch (StaleElementReferenceException)
				{
					return null!;
				}
			});
		}
		finally
		{
			Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
		}
	}

	/// <summary>
	/// Polls for an element to be findable AND <c>Displayed=true</c> within
	/// <paramref name="timeoutSeconds"/>. Returns false on timeout or any error.
	/// Use for boolean visibility probes where the element may not yet be on
	/// screen (e.g. just after a modal push) — a bare zero-wait <c>FindByAutomationId</c>
	/// races the layout pass and reports a false negative.
	/// </summary>
	public bool PollDisplayed(string automationId, double timeoutSeconds = 5)
	{
		var prevWait = TimeSpan.FromSeconds(10);
		var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
		var locator = AutomationIdLocator(automationId);
		try
		{
			Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
			while (DateTime.UtcNow < deadline)
			{
				// FindElements (plural) on no-match returns an empty list with a
				// 200 OK response — the previous FindElement form returned a 404
				// + NoSuchElement that the Appium server logged on every poll
				// iteration. Same observable behaviour, far less log spam on
				// the common "modal not yet on screen / not present" paths.
				var elements = Driver.FindElements(locator);
				if (elements.Count > 0)
				{
					try
					{
						if (elements[0].Displayed)
							return true;
					}
					catch { }
				}
				Thread.Sleep(100);
			}
			return false;
		}
		finally
		{
			Driver.Manage().Timeouts().ImplicitWait = prevWait;
		}
	}

	/// <summary>
	/// Android-only: locates the positive ("accept", e.g. the AppResources.Common_Yes
	/// button) or negative ("cancel" — Common_No) button of a native AlertDialog
	/// raised via Util.DisplayAlertAsync. MAUI's Android DisplayAlert renders an
	/// AndroidX AlertDialog whose buttons keep the platform's standard resource-ids
	/// (android:id/button1 = positive, android:id/button2 = negative) regardless of
	/// locale, so this is more robust than matching the (localized) button text.
	/// Note: the single-button DisplayAlertAsync(title, message, cancel) overload
	/// (e.g. Util.DisplayLoadErrorAsync) has no "accept" side — its one button
	/// renders in the *negative* slot (button2), so callers dismissing that kind
	/// of alert must pass positive: false.
	/// </summary>
	public AppiumElement WaitForNativeAlertButton(bool positive, TimeSpan? timeout = null)
	{
		var locator = By.Id(positive ? "android:id/button1" : "android:id/button2");
		var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(Driver, timeout ?? TimeSpan.FromSeconds(15));
		Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
		try
		{
			return (AppiumElement)wait.Until(d =>
			{
				var elements = d.FindElements(locator);
				if (elements.Count == 0)
					return null!;
				try
				{
					return elements[0].Displayed ? elements[0] : null!;
				}
				catch (StaleElementReferenceException)
				{
					return null!;
				}
			});
		}
		finally
		{
			Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
		}
	}

	/// <summary>
	/// Android-only: reads the title text of a currently-showing native AlertDialog.
	/// Its title TextView keeps the app-scoped resource-id
	/// ("&lt;package&gt;:id/alertTitle", not "android:id/alertTitle" — this
	/// AlertDialog is built with the app's theme rather than the stock AppCompat
	/// one) while its buttons keep the platform-standard "android:id/button1"/
	/// "button2" ids (see WaitForNativeAlertButton). Used to distinguish which of
	/// several possible Util.DisplayAlertAsync prompts is on screen.
	/// </summary>
	public string ReadNativeAlertTitle(TimeSpan? timeout = null)
	{
		var locator = By.Id($"{Config.AppiumConfig.AppPackage}:id/alertTitle");
		var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(Driver, timeout ?? TimeSpan.FromSeconds(15));
		Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
		try
		{
			var element = (AppiumElement)wait.Until(d =>
			{
				var elements = d.FindElements(locator);
				if (elements.Count == 0)
					return null!;
				try
				{
					return elements[0].Displayed ? elements[0] : null!;
				}
				catch (StaleElementReferenceException)
				{
					return null!;
				}
			});
			return element.Text ?? string.Empty;
		}
		finally
		{
			Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
		}
	}

	/// <summary>
	/// Windows-specific helper: locate an element by its visible text via UIA's
	/// <c>Name</c> property. Used as a fallback for MAUI custom controls
	/// (<c>ContentView</c>, <c>ToggleButton</c>) whose AutomationId is exposed
	/// as a non-control Pane that <c>MobileBy.AccessibilityId</c> doesn't match.
	/// Pass one or more candidate texts (e.g. for buttons whose label changes
	/// between toggled states) and the first match wins.
	/// </summary>
	protected AppiumElement WaitForElementByVisibleText(TimeSpan timeout, params string[] candidateTexts)
	{
		if (candidateTexts.Length == 0)
			throw new ArgumentException("At least one candidate text is required", nameof(candidateTexts));

		// Build "//*[@Name='a' or @Name='b' or ...]"
		string predicate = string.Join(" or ",
			candidateTexts.Select(t => $"@Name='{t}'"));
		var xpath = By.XPath($"//*[{predicate}]");

		var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(Driver, timeout);
		Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
		try
		{
			// Same FindElements (plural) rationale as WaitForElement — keeps the
			// driver log free of 404s while the element is still rendering.
			return (AppiumElement)wait.Until(d =>
			{
				var elements = d.FindElements(xpath);
				if (elements.Count == 0)
					return null!;
				try
				{
					return elements[0].Displayed ? elements[0] : null!;
				}
				catch (StaleElementReferenceException)
				{
					return null!;
				}
			});
		}
		finally
		{
			Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
		}
	}
}
