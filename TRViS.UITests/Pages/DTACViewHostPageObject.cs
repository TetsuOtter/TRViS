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

	// --- AppBar WebSocket status indicator (#266) ---

	// Invisible mirror Label reflecting AppViewModel.ServerConnectionStatus.
	// Sentinel-prefixed so it is always non-empty / findable on iOS.
	public AppiumElement ConnectionStatusSeam => WaitForElement(AutomationIds.AppBar.ConnectionStatus);

	/// <summary>
	/// Current AppBar connection-status enum name ("None" / "Connecting" /
	/// "Connected" / "Disconnected") as reflected by the UI_TEST mirror Label.
	/// </summary>
	public string ReadConnectionStatusViaSeam() => StripSeamPrefix(
		ConnectionStatusSeam.Text ?? string.Empty,
		AutomationIds.AppBar.ConnectionStatusPrefix);

	/// <summary>
	/// Polls the connection-status mirror until it equals <paramref name="expected"/>
	/// (or times out). The seam updates via PropertyChanged after a state-toggle
	/// seam tap, so a short poll absorbs the cross-process dispatch latency.
	/// </summary>
	public bool WaitForConnectionStatus(string expected, double timeoutSeconds = 8)
	{
		var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
		do
		{
			try
			{
				if (ReadConnectionStatusViaSeam() == expected)
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

	// --- AppBar header color (#310) ---

	// Invisible mirror Label reflecting EasterEggPageViewModel.
	// EffectiveShellBackgroundColor as "#RRGGBB" text. Sentinel-prefixed so it
	// is always non-empty / findable, same pattern as ConnectionStatusSeam.
	public AppiumElement HeaderColorSeam => WaitForElement(AutomationIds.AppBar.HeaderColorSeam);

	/// <summary>
	/// Current AppBar background color ("#RRGGBB") as reflected by the
	/// UI_TEST mirror Label. Compare against
	/// <see cref="EasterEggPageObject.ReadHeaderColorViaSeam"/> to assert the
	/// Settings screen and DTAC's AppBar always show the same color (#310).
	/// </summary>
	public string ReadHeaderColorViaSeam() => StripSeamPrefix(
		HeaderColorSeam.Text ?? string.Empty,
		AutomationIds.AppBar.HeaderColorSeamPrefix);

	/// <summary>
	/// Polls the header-color mirror until it equals <paramref name="expected"/>
	/// (or times out). The seam updates via PropertyChanged after a
	/// HeaderColor-override seam tap, so a short poll absorbs the cross-process
	/// dispatch latency.
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

	public void TapWsConnectedSeam()
		=> FindByAutomationId(AutomationIds.DTAC.TestWsConnectedButton).Click();

	public void TapWsDisconnectedSeam()
		=> FindByAutomationId(AutomationIds.DTAC.TestWsDisconnectedButton).Click();

	public void TapWsReconnectingSeam()
		=> FindByAutomationId(AutomationIds.DTAC.TestWsReconnectingButton).Click();

	// issue #41: 縦型時刻表のレスポンシブ状態ミラー。
	// "mode=<ViewWidthMode>|rt=0/1|rl=0/1|rm=0/1|mk=0/1|snn=0/1|tnn=0/1"
	public AppiumElement ColumnVisibilitySeam
		=> WaitForElement(AutomationIds.DTAC.TestColumnVisibilitySeam);

	/// <summary>
	/// Reads the responsive-state seam and returns its key→value pairs
	/// (mode + each visibility flag). Empty dictionary if the seam is missing
	/// or only the sentinel prefix is present.
	/// </summary>
	public IReadOnlyDictionary<string, string> ReadColumnVisibilityState()
	{
		string payload = StripSeamPrefix(
			ColumnVisibilitySeam.Text ?? string.Empty,
			AutomationIds.DTAC.TestSeamColumnVisibilityPrefix);
		var result = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var pair in payload.Split('|', StringSplitOptions.RemoveEmptyEntries))
		{
			int eq = pair.IndexOf('=');
			if (eq > 0)
				result[pair.Substring(0, eq)] = pair.Substring(eq + 1);
		}
		return result;
	}

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

	/// <summary>
	/// Best-effort scroll of the timetable toward the bottom by one viewport.
	/// Reuses the same cross-platform swipe the NextTrainButton lookup relies
	/// on (no-op on Windows, whose window is tall enough to fit the rows).
	/// </summary>
	public void SwipeTimetableUp() => TrySwipeUp();

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
	/// issue #41: scans the first <paramref name="maxRows"/> timetable rows for
	/// a station-name label that is genuinely visible to the user (non-zero
	/// frame). The user-facing promise of #41 is that station names stay
	/// readable instead of being clipped off-screen on narrow widths.
	/// </summary>
	public bool HasVisibleStationName(int maxRows = 20)
	{
		var prevWait = TimeSpan.FromSeconds(10);
		try
		{
			Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
			for (int i = 0; i < maxRows; i++)
			{
				string id = AutomationIds.DTAC.TimetableRowStationNamePattern.Replace("{0}", i.ToString());
				try
				{
					var el = Driver.FindElement(AutomationIdLocator(id));
					if (IsElementUserVisible(el))
						return true;
				}
				catch (NoSuchElementException) { }
			}
			return false;
		}
		finally
		{
			Driver.Manage().Timeouts().ImplicitWait = prevWait;
		}
	}

	private AppiumElement FindCustomControl(string automationId, params string[] candidateTexts)
	{
		if (IsWindows)
			return WaitForElementByVisibleText(
				TimeSpan.FromSeconds(WindowsXPathTimeoutSeconds),
				candidateTexts);
		return FindByAutomationId(automationId);
	}

	public bool IsDisplayed(double timeoutSeconds = 60)
	{
		return PollDisplayed(AutomationIds.DTAC.MenuButton, timeoutSeconds);
	}

	public DTACViewHostPageObject SwitchToTimetableTab()
	{
		TabTimetable.Click();
		// The callers already assert the timetable-specific element they need
		// after this tab switch. Keep the handoff lightweight here so Android
		// does not spend a long time churning on the timetable tree before the
		// follow-up assertion runs.
		Thread.Sleep(500);
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

	// ---------- Train search (Issue #197) ----------

	/// <summary>Taps the AppBar title to open the QuickSwitchPopup.</summary>
	public void OpenQuickSwitch()
	{
		TitleLabel.Click();
		Thread.Sleep(300);
	}

	/// <summary>
	/// Dismisses an open QuickSwitchPopup by tapping outside its bounds
	/// (ViewHost.xaml.cs sets DismissOnTapOutside = true). Callers MUST leave
	/// QuickSwitch closed before the test ends: TrainSearchTests previously
	/// left it open after its final assertion, and the next fixture's shared-
	/// session recovery (AppShellPage.NavigateToHome) couldn't dismiss it,
	/// so its 5 s seam probe and fallback flyout tap both missed, cascading
	/// into a 30 s WaitForFlyoutItem timeout (CI run 28886535334).
	/// </summary>
	public void CloseQuickSwitch()
	{
		var size = Driver.Manage().Window.Size;
		int x = size.Width / 2;
		int y = (int)(size.Height * 0.95);

		var touch = new PointerInputDevice(PointerKind.Touch, "finger");
		var seq = new ActionSequence(touch);
		seq.AddAction(touch.CreatePointerMove(CoordinateOrigin.Viewport, x, y, TimeSpan.Zero));
		seq.AddAction(touch.CreatePointerDown(MouseButton.Left));
		seq.AddAction(touch.CreatePointerUp(MouseButton.Left));
		Driver.PerformActions(new List<ActionSequence> { seq });
		Thread.Sleep(300);
	}

	/// <summary>True when the QuickSwitch Search tab is present (server advertises TrainSearch).</summary>
	public bool IsSearchTabPresent(double timeoutSeconds = 5)
		=> PollDisplayed(AutomationIds.DTAC.QuickSwitch.SearchTab, timeoutSeconds);

	public void TapSearchTab()
	{
		FindByAutomationId(AutomationIds.DTAC.QuickSwitch.SearchTab).Click();
		Thread.Sleep(200);
	}

	/// <summary>Switches the match mode to 中間一致 (Contains) — needed when the query
	/// isn't a true prefix of the target train number (default mode is Prefix).</summary>
	public void TapMatchModeContains()
	{
		FindByAutomationId(AutomationIds.DTAC.QuickSwitch.SearchMatchModeContains).Click();
		Thread.Sleep(200);
	}

	/// <summary>
	/// Types a train number. On wide screens QuickSwitchPopup shows a software numeric
	/// keypad and makes TrainNumberEntry read-only (no OS keyboard); on narrow screens
	/// (e.g. Android phone portrait in CI) it falls back to the OS keyboard instead, so
	/// this checks which mode is active and drives whichever is present.
	/// </summary>
	public void EnterTrainNumber(string number)
	{
		if (PollDisplayed(AutomationIds.DTAC.QuickSwitch.SearchKeypadDigitPrefix + number[0], timeoutSeconds: 1))
		{
			foreach (char c in number)
			{
				if (!char.IsDigit(c))
					continue;
				FindByAutomationId(AutomationIds.DTAC.QuickSwitch.SearchKeypadDigitPrefix + c).Click();
			}
			return;
		}

		var entry = WaitForElement(AutomationIds.DTAC.QuickSwitch.SearchEntry);
		try { entry.Clear(); } catch { /* some platforms disallow Clear on empty */ }
		entry.SendKeys(number);
	}

	/// <summary>Waits for a search result row (its AutomationId is the candidate's TrainId).
	/// Search runs automatically (debounced) as the train number is typed — there is no
	/// search button to tap.</summary>
	public bool WaitForSearchResult(string trainId, double timeoutSeconds = 5)
		=> PollDisplayed(trainId, timeoutSeconds);

	public void TapSearchResult(string trainId) => FindByAutomationId(trainId).Click();

	/// <summary>Accepts the native confirmation alert (OK). Cross-platform.</summary>
	public void AcceptConfirmDialog()
	{
		Thread.Sleep(300);
		try
		{
			Driver.SwitchTo().Alert().Accept();
			return;
		}
		catch (NoAlertPresentException) { }
		catch { /* fall through to element-based tap */ }

		try
		{
			Driver.FindElement(By.XPath(
				"//XCUIElementTypeAlert//XCUIElementTypeButton[@label='OK']" +
				" | //android.widget.Button[@text='OK']" +
				" | //*[@text='OK']")).Click();
		}
		catch { /* no alert surfaced */ }
	}

	/// <summary>True when the ハコ tab is present.</summary>
	public bool IsHakoTabPresent(double timeoutSeconds = 3)
		=> PollDisplayed(AutomationIds.DTAC.TabHako, timeoutSeconds);
}
