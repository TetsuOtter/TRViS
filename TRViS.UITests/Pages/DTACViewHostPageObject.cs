using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Interactions;
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

	// UI_TEST seams mirroring AppBar Title / TimeLabelText. Always non-empty
	// (sentinel-prefixed) so they appear in iOS's accessibility tree even
	// before the first state update, and not affected by TimeLabel's
	// narrow-screen visibility threshold. Reads return the *stripped* value.
	public AppiumElement TestTitleSeam => WaitForElement(AutomationIds.DTAC.TestTitleSeam);
	public AppiumElement TestTimeSeam => WaitForElement(AutomationIds.DTAC.TestTimeSeam);

	/// <summary>
	/// Current AppBar title as seen by the presenter. Reads the UI_TEST-only
	/// TestTitleSeam Label and strips its sentinel prefix. Returns "" when
	/// the presenter has set TitleText to empty (no Work selected).
	/// </summary>
	public string ReadTitleViaSeam() => StripSeamPrefix(
		TestTitleSeam.Text ?? string.Empty,
		AutomationIds.DTAC.TestSeamTitlePrefix);

	/// <summary>
	/// Current AppBar clock text as seen by the presenter. Reads the
	/// UI_TEST-only TestTimeSeam Label and strips its sentinel prefix.
	/// Updates once per second when the presenter is alive.
	/// </summary>
	public string ReadTimeViaSeam() => StripSeamPrefix(
		TestTimeSeam.Text ?? string.Empty,
		AutomationIds.DTAC.TestSeamTimePrefix);

	private static string StripSeamPrefix(string raw, string prefix)
		=> raw.StartsWith(prefix) ? raw.Substring(prefix.Length) : raw;
	public AppiumElement TabHako => FindCustomControl(AutomationIds.DTAC.TabHako, "ハ　コ");
	public AppiumElement TabTimetable => FindCustomControl(AutomationIds.DTAC.TabTimetable, "時刻表");
	public AppiumElement TabWorkAffix => FindCustomControl(AutomationIds.DTAC.TabWorkAffix, "行路添付");

	// StartEndRunButton's visible label flips between "運行開始" and "運行終了"
	// as the IsChecked state toggles, so XPath must accept either text.
	public AppiumElement StartEndRunButton
		=> FindCustomControl(AutomationIds.DTAC.StartEndRunButton, "運行開始", "運行終了");

	// LocationServiceButton has three labels stacked inside it: a Material
	// Icons glyph (\uE0C8 = location_on) and the literal "ON" / "OFF" strings. Any of
	// them being present in the UIA tree is sufficient to satisfy the
	// caller's `.Displayed` check, so include all three as candidates.
	public AppiumElement LocationServiceButton
		=> FindCustomControl(AutomationIds.DTAC.LocationServiceButton, "\uE0C8", "ON", "OFF");

	public AppiumElement OpenCloseButton => FindByAutomationId(AutomationIds.DTAC.OpenCloseButton);
	public AppiumElement TimetableScrollView => FindByAutomationId(AutomationIds.DTAC.TimetableScrollView);
	public AppiumElement VerticalTimetableView => FindByAutomationId(AutomationIds.DTAC.VerticalTimetableView);
	public AppiumElement NextTrainButton => WaitForElement(AutomationIds.DTAC.NextTrainButton);

	/// <summary>
	/// Returns true when the NextTrainButton is displayed to the user — either
	/// already on-screen or scrollable into view. Returns false when it never
	/// becomes visible after several scroll attempts.
	///
	/// Why <c>Displayed</c> and not <c>FindElement</c> existence: Mac Catalyst
	/// surfaces Grid elements that have an AutomationId in the accessibility
	/// tree even when they are unparented or have IsVisible=false. Their frame
	/// is then 0×0 / off-window, so <c>Displayed</c> returns false in those
	/// states. <c>Displayed</c> is therefore the cross-platform-reliable
	/// "user can see it" signal.
	///
	/// The button sits at the bottom of the timetable Grid; on small viewports
	/// it can start off-screen, hence the swipe-and-retry loop.
	/// </summary>
	public bool IsNextTrainButtonPresent(TimeSpan? timeout = null)
	{
		var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(8));
		int swipeAttempts = 0;
		const int maxSwipes = 4;

		// Suffix of the button's visible label (e.g. "Ｌｉｎｅａｒ ０ ２の時刻表へ").
		// Stable across train-number variations and used as the Windows fallback,
		// because WinUI 3 surfaces a MAUI Grid's AutomationId as a non-control
		// Pane that AccessibilityId search doesn't always reach.
		const string ButtonTextSuffix = "の時刻表へ";

		Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
		try
		{
			while (DateTime.UtcNow < deadline)
			{
				if (TryFindVisibleNextTrainButton(ButtonTextSuffix))
					return true;

				// Element either not in tree or in tree but not visible
				// (off-screen, unparented, or hidden via IsVisible=false).
				// Swipe up to bring it on-screen if possible; otherwise
				// keep polling until the deadline.
				if (swipeAttempts < maxSwipes)
				{
					TrySwipeUp();
					swipeAttempts++;
				}
				else
				{
					return false;
				}
			}
			return false;
		}
		finally
		{
			Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
		}
	}

	private bool TryFindVisibleNextTrainButton(string buttonTextSuffix)
	{
		// Primary: AutomationId lookup. Works on iOS/Android/macOS.
		try
		{
			var el = Driver.FindElement(AutomationIdLocator(AutomationIds.DTAC.NextTrainButton));
			if (IsElementUserVisible(el))
				return true;
		}
		catch (NoSuchElementException) { }

		// Windows fallback: search by the constant Japanese suffix in the
		// inner Button's visible label using XPath contains() against the
		// UIA Name property.
		if (IsWindows)
		{
			try
			{
				var el = Driver.FindElement(By.XPath(
					$"//*[contains(@Name, '{buttonTextSuffix}')]"));
				if (IsElementUserVisible(el))
					return true;
			}
			catch (NoSuchElementException) { }
		}

		return false;
	}

	/// <summary>
	/// Returns true only when an element is genuinely visible to the user.
	/// Combines <c>Displayed</c> with a non-zero <c>Size</c> check: Mac Catalyst's
	/// mac2 driver surfaces unparented elements with an AutomationId in the
	/// accessibility tree and reports them as <c>Displayed=true</c>, but their
	/// frame is still 0×0 because they are not laid out. Size is the disambiguator.
	/// </summary>
	private static bool IsElementUserVisible(AppiumElement el)
	{
		try
		{
			if (!el.Displayed)
				return false;
			var size = el.Size;
			return size.Width > 0 && size.Height > 0;
		}
		catch
		{
			// Stale element / driver-side error → treat as not visible.
			return false;
		}
	}

	/// <summary>
	/// Best-effort upward swipe in the centre of the screen to scroll the
	/// timetable toward the bottom. Cross-platform via W3C PointerActions;
	/// falls back to a no-op on platforms that don't accept the gesture
	/// (Windows / macOS desktop), where the timetable usually fits anyway.
	/// </summary>
	private void TrySwipeUp()
	{
		try
		{
			if (IsWindows)
			{
				// Windows desktop uses a wide window; the button typically fits.
				// Skip swipe to avoid unsupported pointer-input errors.
				return;
			}

			var size = Driver.Manage().Window.Size;
			int x = size.Width / 2;
			int startY = (int)(size.Height * 0.75);
			int endY = (int)(size.Height * 0.25);

			var touch = new PointerInputDevice(PointerKind.Touch, "finger");
			var seq = new ActionSequence(touch);
			seq.AddAction(touch.CreatePointerMove(CoordinateOrigin.Viewport, x, startY, TimeSpan.Zero));
			seq.AddAction(touch.CreatePointerDown(MouseButton.Left));
			seq.AddAction(touch.CreatePointerMove(CoordinateOrigin.Viewport, x, endY, TimeSpan.FromMilliseconds(400)));
			seq.AddAction(touch.CreatePointerUp(MouseButton.Left));
			Driver.PerformActions(new List<ActionSequence> { seq });
			Thread.Sleep(300);
		}
		catch
		{
			// Best-effort; swallow driver-specific failures so the caller can
			// proceed to the next attempt.
		}
	}

	// Hidden when the selected Work has no embedded horizontal timetable;
	// FindCustomControl falls back to UIA Name lookup on Windows because
	// the inner Border doesn't surface as an addressable AccessibilityId there.
	// EasterEgg setting can switch the label between 横型時刻表 / 電車時刻表 / Ｅ電時刻表 —
	// all three candidates are needed so the Windows fallback finds the button in any mode.
	public AppiumElement HorizontalTimetableButton
		=> FindCustomControl(AutomationIds.DTAC.HorizontalTimetableButton, "横型時刻表", "電車時刻表", "Ｅ電時刻表");

	/// <summary>
	/// Polls briefly for the horizontal-timetable button. Returns true only when
	/// the element is both findable and Displayed=true within the timeout.
	/// Used to assert the button is hidden by default with sample data.
	///
	/// On Windows the MAUI Border doesn't expose its AutomationId reliably (WinUI 3
	/// surfaces it as a non-control Pane), so fall back to a UIA Name lookup on
	/// the inner label text — same dual-strategy as <see cref="FindCustomControl"/>.
	/// </summary>
	public bool IsHorizontalTimetableButtonVisible(double timeoutSeconds = 1)
	{
		var prevWait = TimeSpan.FromSeconds(10);
		var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
		try
		{
			Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
			while (DateTime.UtcNow < deadline)
			{
				try
				{
					if (FindByAutomationId(AutomationIds.DTAC.HorizontalTimetableButton).Displayed)
						return true;
				}
				catch { }
				if (IsWindows)
				{
					try
					{
						var el = Driver.FindElement(By.XPath(
							"//*[@Name='横型時刻表' or @Name='電車時刻表' or @Name='Ｅ電時刻表']"));
						if (el.Displayed)
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

	public DTACViewHostPageObject TapHorizontalTimetableButton()
	{
		HorizontalTimetableButton.Click();
		return this;
	}

	/// <summary>
	/// Polls briefly for the iPhone-only full-scroll entry button (#155).
	/// Returns true only when it is both findable and Displayed within the
	/// timeout. False on tablet / desktop idioms where it is intentionally
	/// hidden, and on the full-scroll page itself. AutomationId-only: the
	/// button never appears on Windows (desktop idiom) so no UIA Name fallback
	/// is needed, and its label is a Material-icon glyph anyway.
	/// </summary>
	public bool IsFullScrollButtonVisible(double timeoutSeconds = 1)
	{
		var prevWait = TimeSpan.FromSeconds(10);
		var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
		try
		{
			Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
			while (DateTime.UtcNow < deadline)
			{
				try
				{
					if (FindByAutomationId(AutomationIds.DTAC.FullScrollButton).Displayed)
						return true;
				}
				catch { }
				Thread.Sleep(100);
			}
			return false;
		}
		finally
		{
			Driver.Manage().Timeouts().ImplicitWait = prevWait;
		}
	}

	public FullScrollVerticalTimetablePageObject TapFullScrollButton()
	{
		FindByAutomationId(AutomationIds.DTAC.FullScrollButton).Click();
		return new FullScrollVerticalTimetablePageObject(Driver);
	}

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
		//
		// 60 s (vs the default 30 s) because the iPad mini matrix entries on
		// macos-26 occasionally take longer than 30 s to lay out the timetable
		// tab after the click — run 25631786714 timed out here on iPad mini
		// A17 with everything else passing (20/21). Mirrors the same headroom
		// the iPhone job already needed on SelectTrainPageObject.Title.
		WaitForElement(AutomationIds.DTAC.TimetableScrollView, TimeSpan.FromSeconds(60));
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
