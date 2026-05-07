using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using TRViS.UITests.Infrastructure;

namespace TRViS.UITests.Pages;

public class DTACViewHostPageObject : PageObject
{
	public DTACViewHostPageObject(AppiumDriver driver) : base(driver) { }

	// Tab buttons (TabButton = MAUI ContentView) and the StartEndRun /
	// LocationService toggles (custom ToggleButton : ContentView) all expose
	// their AutomationId as a non-control Pane on WinUI that Appium's
	// AccessibilityId search doesn't match. Fall back to UIA Name lookup
	// using the visible label text on Windows.
	private const int WindowsXPathTimeoutSeconds = 15;

	public AppiumElement MenuButton => WaitForElement(AutomationIds.DTAC.MenuButton);
	public AppiumElement TimeLabel => FindByAutomationId(AutomationIds.DTAC.TimeLabel);
	public AppiumElement TitleLabel => FindByAutomationId(AutomationIds.DTAC.TitleLabel);
	public AppiumElement TabHako => FindCustomControl(AutomationIds.DTAC.TabHako, "ハ　コ");
	public AppiumElement TabTimetable => FindCustomControl(AutomationIds.DTAC.TabTimetable, "時刻表");
	public AppiumElement TabWorkAffix => FindCustomControl(AutomationIds.DTAC.TabWorkAffix, "行路添付");

	// StartEndRunButton's visible label flips between "運行開始" and "運行終了"
	// as the IsChecked state toggles, so XPath must accept either text.
	public AppiumElement StartEndRunButton
		=> FindCustomControl(AutomationIds.DTAC.StartEndRunButton, "運行開始", "運行終了");

	// LocationServiceButton has three labels stacked inside it: a Material
	// Icons glyph (U+E0C8) and the literal "ON" / "OFF" strings. Any of
	// them being present in the UIA tree is sufficient to satisfy the
	// caller's `.Displayed` check, so include all three as candidates.
	public AppiumElement LocationServiceButton
		=> FindCustomControl(AutomationIds.DTAC.LocationServiceButton, "", "ON", "OFF");

	public AppiumElement OpenCloseButton => FindByAutomationId(AutomationIds.DTAC.OpenCloseButton);
	public AppiumElement TimetableScrollView => FindByAutomationId(AutomationIds.DTAC.TimetableScrollView);
	public AppiumElement VerticalTimetableView => FindByAutomationId(AutomationIds.DTAC.VerticalTimetableView);

	private AppiumElement FindCustomControl(string automationId, params string[] candidateTexts)
	{
		if (IsWindows)
			return WaitForElementByVisibleText(
				TimeSpan.FromSeconds(WindowsXPathTimeoutSeconds),
				candidateTexts);
		return FindByAutomationId(automationId);
	}

	public bool IsDisplayed()
	{
		try
		{
			return MenuButton.Displayed;
		}
		catch (NoSuchElementException)
		{
			return false;
		}
	}

	public DTACViewHostPageObject SwitchToTimetableTab()
	{
		TabTimetable.Click();
		// Give the layout a beat to swap the tab content into view.
		Thread.Sleep(300);
		// On iOS the inner Grid (VerticalTimetableView) may not surface as an
		// addressable accessibility element by AutomationId, so wait for the
		// surrounding ScrollView instead — that container reliably appears in
		// the accessibility tree on every platform.
		WaitForElement(AutomationIds.DTAC.TimetableScrollView);
		return this;
	}

	public DTACViewHostPageObject TapStartEndRun()
	{
		StartEndRunButton.Click();
		return this;
	}

	public DTACViewHostPageObject TapOpenClose()
	{
		OpenCloseButton.Click();
		return this;
	}

	/// <summary>
	/// Counts visible station-name labels in the timetable. With the sample
	/// data this should be ≈ the number of TimetableRow entries (18 for the
	/// first sample train, including info rows). Used to verify "表示件数".
	/// </summary>
	public int CountVisibleTimetableTextElements()
	{
		var view = WaitForElement(AutomationIds.DTAC.VerticalTimetableView);
		// All descendant elements with non-empty text. Cross-platform XPath.
		var descendants = view.FindElements(By.XPath(".//*"));
		int count = 0;
		foreach (var el in descendants)
		{
			try
			{
				if (!string.IsNullOrEmpty(el.Text))
					count++;
			}
			catch { /* stale */ }
		}
		return count;
	}

	/// <summary>
	/// Waits for the timetable scroll view to scroll vertically — used by the
	/// auto-scroll test once a fake GPS location has been pushed.
	/// </summary>
	public bool WaitForScrollPositionChange(double initialScrollY, TimeSpan timeout)
	{
		var deadline = DateTime.UtcNow + timeout;
		while (DateTime.UtcNow < deadline)
		{
			try
			{
				// Some platforms expose the ScrollView's scrollY only via attribute access.
				// On iOS XCUITest the value may not be directly readable; bail out and
				// rely on element-position heuristics (out of scope for this helper).
				var scrollAttr = TimetableScrollView.GetAttribute("contentOffsetY");
				if (!string.IsNullOrEmpty(scrollAttr) && double.TryParse(scrollAttr, out double y) && Math.Abs(y - initialScrollY) > 1.0)
					return true;
			}
			catch { /* attribute not supported */ }
			Thread.Sleep(200);
		}
		return false;
	}
}
