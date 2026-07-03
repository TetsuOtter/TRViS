using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using TRViS.UITests.Infrastructure;

namespace TRViS.UITests.Pages;

/// <summary>
/// Page object for the in-app QR scanner (phone only). The camera itself can't
/// be driven by Appium, so the two <c>SimulateScan</c> methods tap hidden
/// UI_TEST seam buttons that feed a canned payload through the exact same
/// acceptance gate a live QR detection hits:
/// <list type="bullet">
///   <item>valid → a <c>trvis://</c> AppLink is accepted, the scanner closes and
///   the link is handed to the AppLink pipeline;</item>
///   <item>invalid → a non-<c>trvis</c> QR is ignored and the scanner stays open.</item>
/// </list>
/// </summary>
public class ScanQrPageObject : PageObject
{
	public ScanQrPageObject(AppiumDriver driver) : base(driver) { }

	/// <summary>
	/// URL the valid-scan seam seeds into ExternalResourceUrlHistory once the
	/// scanned TRViS AppLink is processed. Mirrors the decoded form of
	/// <c>ScanQrPage.TestValidPayload</c> so the accept path is observable via
	/// the ConnectServer history list.
	/// </summary>
	public const string SeededUrl = "https://e2e.example/scanned.json";

	public AppiumElement Instruction => WaitForElement(AutomationIds.ScanQr.Instruction);
	public AppiumElement CloseButton => FindByAutomationId(AutomationIds.ScanQr.CloseButton);

	/// <summary>True when the scanner page is on screen.</summary>
	public bool IsDisplayed(double timeoutSeconds = 8)
		=> PollDisplayed(AutomationIds.ScanQr.Instruction, timeoutSeconds);

	/// <summary>
	/// Simulates detecting a valid TRViS AppLink QR (accept path). The page
	/// closes on success and the link is processed.
	/// </summary>
	public void SimulateValidScan()
		=> FindByAutomationId(AutomationIds.ScanQr.TestSimulateValidButton).Click();

	/// <summary>
	/// Simulates detecting a non-TRViS QR (reject path). The page must stay open.
	/// </summary>
	public void SimulateInvalidScan()
		=> FindByAutomationId(AutomationIds.ScanQr.TestSimulateInvalidButton).Click();

	/// <summary>Closes the scanner and returns to StartHomePage.</summary>
	public StartHomePageObject Close()
	{
		CloseButton.Click();
		return new StartHomePageObject(Driver);
	}
}
