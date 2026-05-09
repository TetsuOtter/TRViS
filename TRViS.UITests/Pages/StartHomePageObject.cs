using OpenQA.Selenium.Appium;
using TRViS.UITests.Infrastructure;

namespace TRViS.UITests.Pages;

/// <summary>
/// Page object for the unified Start/Home page (replaces the legacy SelectTrainPage).
/// In Start mode the Connect/SelectFile/LoadDemo buttons are visible; in Home mode
/// the WorkGroup/Work lists and Open/Disconnect buttons are visible. The animated
/// header toggles between "centered" (Start) and "top-anchored" (Home).
/// </summary>
public class StartHomePageObject : PageObject
{
	public StartHomePageObject(AppiumDriver driver) : base(driver) { }

	// Header — present in both modes. Use a generous timeout because the post-FirebaseSettings
	// navigation on slow simulators (iOS macos-26 iPhone) can take >30 s.
	public AppiumElement Title => WaitForElement(AutomationIds.StartHome.Title, TimeSpan.FromSeconds(60));
	public AppiumElement AppIcon => FindByAutomationId(AutomationIds.StartHome.AppIcon);

	// Start mode — primary buttons.
	// Use WaitForElement for buttons that may be temporarily not findable while a popup
	// is dismissing back to this page.
	public AppiumElement ConnectServerButton => WaitForElement(AutomationIds.StartHome.ConnectServerButton);
	public AppiumElement SelectFileButton => FindByAutomationId(AutomationIds.StartHome.SelectFileButton);
	public AppiumElement LoadDemoButton => FindByAutomationId(AutomationIds.StartHome.LoadDemoButton);

	// Start mode — privacy / footer.
	public AppiumElement PrivacyReconfirmBanner => FindByAutomationId(AutomationIds.StartHome.PrivacyReconfirmBanner);
	public AppiumElement PrivacyPolicyButton => FindByAutomationId(AutomationIds.StartHome.PrivacyPolicyButton);
	public AppiumElement ThirdPartyLicensesButton => FindByAutomationId(AutomationIds.StartHome.ThirdPartyLicensesButton);

	// Home mode. Two-step picker: each step has either a list (full picker) or a
	// chip (compact summary, shown after selection). Only one of {List, Chip} per
	// step is visible at any time; assertions should branch on which is reachable.
	public AppiumElement WorkGroupList => FindByAutomationId(AutomationIds.StartHome.WorkGroupList);
	public AppiumElement WorkGroupChip => FindByAutomationId(AutomationIds.StartHome.WorkGroupChip);
	public AppiumElement WorkList => FindByAutomationId(AutomationIds.StartHome.WorkList);
	public AppiumElement WorkChip => FindByAutomationId(AutomationIds.StartHome.WorkChip);
	public AppiumElement WorkPendingHint => FindByAutomationId(AutomationIds.StartHome.WorkPendingHint);
	public AppiumElement OpenButton => FindByAutomationId(AutomationIds.StartHome.OpenButton);
	public AppiumElement DisconnectButton => FindByAutomationId(AutomationIds.StartHome.DisconnectButton);

	/// <summary>
	/// Returns true when the WorkGroupChip is currently visible (i.e. a tentative
	/// WorkGroup has been selected). Returns false on any lookup error so callers
	/// can treat "absent" as "no tentative selection".
	/// Mirrors <see cref="IsPrivacyReconfirmBannerVisible"/>'s zero-implicit-wait pattern.
	/// </summary>
	public bool IsWorkGroupChipVisible()
	{
		var prevWait = TimeSpan.FromSeconds(10);
		try
		{
			Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
			return FindByAutomationId(AutomationIds.StartHome.WorkGroupChip).Displayed;
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

	// UI_TEST seed seams.
	public AppiumElement TestSeedButton => FindByAutomationId(AutomationIds.StartHome.TestSeedButton);
	public AppiumElement TestSeedGpsButton => FindByAutomationId(AutomationIds.StartHome.TestSeedGpsButton);
	public AppiumElement TestAutoOpenButton => FindByAutomationId(AutomationIds.StartHome.TestAutoOpenButton);

	public bool IsDisplayed()
	{
		try
		{
			return Title.Displayed;
		}
		catch (OpenQA.Selenium.NoSuchElementException)
		{
			return false;
		}
	}

	/// <summary>
	/// Returns true when the privacy-policy reconfirm banner is currently visible
	/// (i.e. the user has not yet accepted the current PRIVACY_POLICY_REVISION).
	/// Returns false on any lookup error so callers can treat "absent" as "accepted".
	/// </summary>
	public bool IsPrivacyReconfirmBannerVisible()
	{
		var prevWait = TimeSpan.FromSeconds(10);
		try
		{
			Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
			return FindByAutomationId(AutomationIds.StartHome.PrivacyReconfirmBanner).Displayed;
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
	/// If the privacy banner is visible, opens the privacy dialog and taps Save.
	/// On a clean install the app launches into Start with the banner shown — tests
	/// that need any feature button (Connect, SelectFile, LoadDemo) must call this
	/// first because those buttons gate on privacy acceptance.
	/// </summary>
	public void AcceptPrivacyPolicyIfNeeded()
	{
		if (!IsPrivacyReconfirmBannerVisible())
			return;

		PrivacyPolicyButton.Click();
		// Wait for the dialog's Save button (acts as ready-signal that the modal is up).
		var save = WaitForElement(AutomationIds.PrivacyDialog.SaveButton);
		save.Click();

		// Accept any "Success!" alert dialog the save handler raises (DISABLE_FIREBASE
		// builds skip it, so missing-alert is fine).
		try
		{
			Driver.SwitchTo().Alert().Accept();
		}
		catch (OpenQA.Selenium.NoAlertPresentException) { }
		catch
		{
			try
			{
				Driver.FindElement(OpenQA.Selenium.By.XPath(
					"//XCUIElementTypeSheet//XCUIElementTypeButton[@label='OK']" +
					" | //XCUIElementTypeAlert//XCUIElementTypeButton[@label='OK']"
				)).Click();
			}
			catch { /* no alert/sheet — DISABLE_FIREBASE build */ }
		}

		// Wait for the dialog to dismiss back to the Start page.
		_ = Title;
		Thread.Sleep(300);
	}

	/// <summary>
	/// Loads the demo (sample) data set. After load, the page transitions to Home mode
	/// and the WorkGroup/Work lists become visible.
	/// </summary>
	public void LoadSample() => LoadDemoButton.Click();

	/// <summary>
	/// Taps "Connect to Server" and returns the dialog's page object.
	/// </summary>
	public ConnectServerDialogPageObject OpenConnectServerDialog()
	{
		ConnectServerButton.Click();
		return new ConnectServerDialogPageObject(Driver);
	}

	/// <summary>
	/// Taps "ファイルを選択" and returns the dialog's page object.
	/// </summary>
	public SelectFileDialogPageObject OpenSelectFileDialog()
	{
		SelectFileButton.Click();
		return new SelectFileDialogPageObject(Driver);
	}

	/// <summary>
	/// Taps the UI_TEST-only test-seed button so tests can populate URL history
	/// without typing through Appium SendKeys (flaky on iOS XCUITest).
	/// </summary>
	public void SeedUrlHistoryForTesting() => TestSeedButton.Click();

	/// <summary>
	/// Taps the UI_TEST-only GPS-seed button so tests can push a fake GPS coord
	/// without typing through Appium SendKeys.
	/// </summary>
	public void SeedGpsLocationForTesting() => TestSeedGpsButton.Click();

	/// <summary>
	/// Taps the UI_TEST-only auto-open button. Picks the first WorkGroup +
	/// first Work and commits via the same code path as 開く, then navigates
	/// to DTAC. Lets DTAC tests bypass the picker UI.
	/// </summary>
	public DTACViewHostPageObject AutoOpenForTesting()
	{
		TestAutoOpenButton.Click();
		return new DTACViewHostPageObject(Driver);
	}

	/// <summary>
	/// URLs that <see cref="SeedUrlHistoryForTesting"/> writes into history.
	/// Mirrors the literals in StartHomePage.xaml.cs so tests can assert against them.
	/// </summary>
	public static readonly string[] SeededHistoryUrls =
	{
		"https://example.com/timetable-a.json",
		"https://example.com/timetable-b.json",
	};

	/// <summary>
	/// Returns the count of WorkGroup rows after a sample load. Useful to verify
	/// "表示件数" against the known sample-data fixture.
	/// </summary>
	public int CountWorkGroups()
	{
		var list = WaitForElement(AutomationIds.StartHome.WorkGroupList);
		var children = list.FindElements(OpenQA.Selenium.By.XPath(".//*"));
		// Filter to elements whose text is non-empty so we don't double-count
		// child layout primitives (Grid/StackLayout) on platforms whose
		// accessibility tree exposes inner cells.
		int count = 0;
		foreach (var el in children)
		{
			try
			{
				if (!string.IsNullOrEmpty(el.Text))
					count++;
			}
			catch { /* element went stale or text unavailable */ }
		}
		return count;
	}
}
