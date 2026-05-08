using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using TRViS.UITests.Infrastructure;

namespace TRViS.UITests.Pages;

public class SelectOnlineResourcePopupPageObject : PageObject
{
	public SelectOnlineResourcePopupPageObject(AppiumDriver driver) : base(driver) { }

	public AppiumElement CloseButton => WaitForElement(AutomationIds.SelectOnlineResource.CloseButton);
	public AppiumElement LoadButton => FindByAutomationId(AutomationIds.SelectOnlineResource.LoadButton);
	public AppiumElement UrlInput => FindByAutomationId(AutomationIds.SelectOnlineResource.UrlInput);
	public AppiumElement UrlHistoryList => FindByAutomationId(AutomationIds.SelectOnlineResource.UrlHistoryList);
	public AppiumElement AdviceLabel => FindByAutomationId(AutomationIds.SelectOnlineResource.AdviceLabel);

	public bool IsDisplayed()
	{
		try
		{
			return CloseButton.Displayed;
		}
		catch (NoSuchElementException)
		{
			return false;
		}
	}

	/// <summary>
	/// Returns the AutomationId-tagged history-row element matching <paramref name="url"/>,
	/// waiting up to 30 s for it to appear. Not supported on Windows — see
	/// <see cref="Tests.SelectOnlineResourcePopupTests"/> for the [Platform] skip.
	/// </summary>
	public AppiumElement HistoryItem(string url)
		=> WaitForElement(AutomationIds.SelectOnlineResource.UrlHistoryItemPrefix + url);

	/// <summary>
	/// Reads the value from the URL input. Across platforms the Entry's text appears
	/// either in the standard "value"/"text" attributes or as the element's Text.
	/// Returns an empty string if no value can be retrieved.
	/// </summary>
	public string ReadUrlInputText()
	{
		var input = UrlInput;
		// Try Selenium's Text first, then common platform attributes.
		var text = input.Text ?? string.Empty;
		if (!string.IsNullOrEmpty(text))
			return text;
		foreach (var attr in new[] { "value", "text" })
		{
			try
			{
				var v = input.GetAttribute(attr);
				if (!string.IsNullOrEmpty(v))
					return v;
			}
			catch { /* attribute not supported on this platform */ }
		}
		return string.Empty;
	}

	/// <summary>
	/// Types into the URL Entry. Clears prior text first to avoid concatenation.
	/// </summary>
	public void TypeUrl(string url)
	{
		var input = UrlInput;
		try { input.Clear(); } catch { /* not supported on some drivers */ }
		input.SendKeys(url);
	}

	/// <summary>
	/// Taps the Load button. Triggers HandleAppLinkUriAsync — including the
	/// DEBUG-only seed-url-history path used by tests.
	/// </summary>
	public void TapLoad() => LoadButton.Click();

	/// <summary>
	/// Taps a history row by URL.
	/// </summary>
	public void TapHistoryItem(string url) => HistoryItem(url).Click();

	/// <summary>
	/// Closes the popup and returns to StartHomePage.
	/// </summary>
	public StartHomePageObject Close()
	{
		CloseButton.Click();
		return new StartHomePageObject(Driver);
	}

	/// <summary>
	/// Convenience: seed the URL history list using the DEBUG-only deeplink,
	/// then ensure the popup is dismissed back to StartHomePage. Pass an empty
	/// array to just close the popup (no-op seed).
	/// </summary>
	public void SeedHistoryAndClose(IEnumerable<string> urls)
	{
		var joined = string.Join('|', urls);
		if (!string.IsNullOrEmpty(joined))
		{
			TypeUrl("trvis://_test/seed-url-history?urls=" + Uri.EscapeDataString(joined));
			TapLoad();
			// On success, DoLoad awaits Navigation.PopModalAsync. On platforms
			// where the modal isn't dismissed reliably (Mac Catalyst sometimes
			// keeps the input disabled after the await), fall through and let
			// the explicit Close click below handle dismissal.
			Thread.Sleep(500);
		}

		// If the popup is still showing, tap Close explicitly. We do not raise
		// on the "find" itself because the popup may already have dismissed.
		try
		{
			var close = FindByAutomationId(AutomationIds.SelectOnlineResource.CloseButton);
			if (close.Displayed && close.Enabled)
				close.Click();
		}
		catch (NoSuchElementException) { /* popup already gone */ }
		catch (StaleElementReferenceException) { /* popup torn down mid-find */ }

		// Block until StartHomePage controls are reachable again so the next
		// test step doesn't race the modal-dismiss animation.
		new StartHomePageObject(Driver).WaitForElement(AutomationIds.StartHome.ConnectServerButton);
	}
}
