using OpenQA.Selenium.Appium;
using TRViS.UITests.Infrastructure;

namespace TRViS.UITests.Pages;

public class EasterEggPageObject : PageObject
{
	public EasterEggPageObject(AppiumDriver driver) : base(driver) { }

	public AppiumElement ReloadSavedButton => WaitForElement(AutomationIds.Settings.ReloadSavedButton);
	public AppiumElement SaveButton => FindByAutomationId(AutomationIds.Settings.SaveButton);

	// Invisible mirror Label reflecting EasterEggPageViewModel.
	// EffectiveShellBackgroundColor as "#RRGGBB" text (#310). This is the
	// Settings-screen side of the color the native title bar actually shows
	// (AppShell.BackgroundColor binds to the same ViewModel property), which
	// Appium cannot read directly off the native Shell chrome.
	public AppiumElement HeaderColorSeam => WaitForElement(AutomationIds.Settings.HeaderColorSeam);

	/// <summary>
	/// Current Settings-screen title-bar color ("#RRGGBB") as reflected by the
	/// UI_TEST mirror Label. Compare against
	/// <see cref="DTACViewHostPageObject.ReadHeaderColorViaSeam"/> to assert
	/// the two screens always show the same color (#310).
	/// </summary>
	public string ReadHeaderColorViaSeam()
	{
		string raw = HeaderColorSeam.Text ?? string.Empty;
		return raw.StartsWith(AutomationIds.Settings.HeaderColorSeamPrefix)
			? raw.Substring(AutomationIds.Settings.HeaderColorSeamPrefix.Length)
			: raw;
	}

	/// <summary>
	/// Polls the header-color mirror until it equals <paramref name="expected"/>
	/// (or times out). HeaderColorOverride_RGB changes are dispatched via
	/// MainThread.BeginInvokeOnMainThread (the property may originate off the
	/// UI thread from a WebSocket message), so a short poll absorbs that latency.
	/// </summary>
	public bool WaitForHeaderColor(string expected, double timeoutSeconds = 8)
	{
		var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
		do
		{
			try
			{
				if (ReadHeaderColorViaSeam() == expected)
					return true;
			}
			catch (OpenQA.Selenium.WebDriverException)
			{
				// element momentarily not in tree mid-transition — keep polling
			}
			Thread.Sleep(200);
		} while (DateTime.UtcNow < deadline);
		return false;
	}

	public bool IsDisplayed()
	{
		try
		{
			return ReloadSavedButton.Displayed;
		}
		catch (OpenQA.Selenium.NoSuchElementException)
		{
			return false;
		}
	}
}
