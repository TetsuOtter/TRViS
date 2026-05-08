using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using TRViS.UITests.Infrastructure;

namespace TRViS.UITests.Pages;

/// <summary>
/// Page object for the Connect-to-Server modal (replaces SelectOnlineResourcePopup).
/// The dialog has two states: history list (rich tappable cards) and a new-connection
/// form. <see cref="OpenNewConnectionForm"/> switches from list → form when needed;
/// <see cref="GoBackToHistory"/> returns from form → list (only available when there
/// is at least one history entry).
/// </summary>
public class ConnectServerDialogPageObject : PageObject
{
	public ConnectServerDialogPageObject(AppiumDriver driver) : base(driver) { }

	public AppiumElement Title => WaitForElement(AutomationIds.ConnectServer.Title);
	public AppiumElement CloseButton => FindByAutomationId(AutomationIds.ConnectServer.CloseButton);

	// History list state
	public AppiumElement HistoryList => FindByAutomationId(AutomationIds.ConnectServer.HistoryList);
	public AppiumElement NewConnectionButton => FindByAutomationId(AutomationIds.ConnectServer.NewConnectionButton);

	// New-connection form state
	public AppiumElement BackToHistoryButton => FindByAutomationId(AutomationIds.ConnectServer.BackToHistoryButton);
	public AppiumElement UrlInput => FindByAutomationId(AutomationIds.ConnectServer.UrlInput);
	public AppiumElement SaveConnectionSwitch => FindByAutomationId(AutomationIds.ConnectServer.SaveConnectionSwitch);
	public AppiumElement ConnectButton => FindByAutomationId(AutomationIds.ConnectServer.ConnectButton);

	public bool IsDisplayed()
	{
		try
		{
			return Title.Displayed;
		}
		catch (NoSuchElementException)
		{
			return false;
		}
	}

	/// <summary>
	/// Returns true when the history list is the active sub-view.
	/// </summary>
	public bool IsHistoryViewVisible()
	{
		var prevWait = TimeSpan.FromSeconds(10);
		try
		{
			Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
			return FindByAutomationId(AutomationIds.ConnectServer.HistoryList).Displayed;
		}
		catch
		{
			return false;
		}
		finally
		{
			Driver.Manage().Timeouts().ImplicitWait = prevWait;
		}
	}

	/// <summary>
	/// Returns true when the new-connection form is the active sub-view.
	/// </summary>
	public bool IsNewConnectionFormVisible()
	{
		var prevWait = TimeSpan.FromSeconds(10);
		try
		{
			Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
			return FindByAutomationId(AutomationIds.ConnectServer.UrlInput).Displayed;
		}
		catch
		{
			return false;
		}
		finally
		{
			Driver.Manage().Timeouts().ImplicitWait = prevWait;
		}
	}

	/// <summary>
	/// Returns the per-row card element for <paramref name="url"/>, waiting up to 30s
	/// for it to appear. The whole card is tappable — selecting it triggers a load
	/// using the existing addToHistory:true semantics (history tap always re-saves).
	/// </summary>
	public AppiumElement HistoryItem(string url)
		=> WaitForElement(AutomationIds.ConnectServer.HistoryItemPrefix + url);

	/// <summary>
	/// Taps a history card by URL. The card-tap loads the URL directly and dismisses
	/// the dialog on success.
	/// </summary>
	public void TapHistoryItem(string url) => HistoryItem(url).Click();

	/// <summary>
	/// Switches from the history list to the new-connection form by tapping
	/// "+ 新規接続". Available only when at least one history entry exists.
	/// </summary>
	public void OpenNewConnectionForm() => NewConnectionButton.Click();

	/// <summary>
	/// Returns from the new-connection form to the history list. Only present when
	/// history was non-empty when the dialog opened.
	/// </summary>
	public void GoBackToHistory() => BackToHistoryButton.Click();

	/// <summary>
	/// Types into the URL Entry. Clears prior text first so re-typing doesn't
	/// concatenate.
	/// </summary>
	public void TypeUrl(string url)
	{
		var input = UrlInput;
		try { input.Clear(); } catch { /* not supported on some drivers */ }
		input.SendKeys(url);
	}

	/// <summary>
	/// Reads the value out of the URL Entry. Mirrors the cross-platform attribute
	/// fallback used elsewhere because XCUITest, UIAutomator2, and WinAppDriver
	/// surface Entry.Text via different attributes.
	/// </summary>
	public string ReadUrlInputText()
	{
		var input = UrlInput;
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

	public void TapConnect() => ConnectButton.Click();

	/// <summary>
	/// Closes the dialog and returns to StartHomePage.
	/// </summary>
	public StartHomePageObject Close()
	{
		CloseButton.Click();
		return new StartHomePageObject(Driver);
	}
}
