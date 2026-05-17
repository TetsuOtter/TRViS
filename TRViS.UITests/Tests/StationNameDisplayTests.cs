using System.Text;

using OpenQA.Selenium;
using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// Regression for "1–4 character station names no longer fully display".
///
/// A 4-char name ("さ新都心") rendered at the fixed timetable font overflowed
/// the fixed station-name column, wrapped, and only the first line ("さ新")
/// stayed visible. The production fix keeps the font and column width fixed and
/// widens only the station-name label past its cell, and unifies the converter
/// so 1/2/3/4-char names are spaced consistently on every platform.
///
/// This fixture guards the StationName rendering pipeline: it scans every
/// timetable row's StationName label, strips the spacing characters
/// StationNameConverter inserts between glyphs, and asserts that a fully
/// rendered representative name for each length (1, 2, 3, 4 chars) is present.
/// It deliberately does NOT assume a fixed RowIndex→name mapping (off-screen
/// rows and WinUI element resolution make per-index lookups unreliable) — the
/// set of rendered names is stable regardless of which index each resolves to.
/// </summary>
[TestFixture]
[Platform(Exclude = "Linux", Reason = "Android UIAutomator2 does not expose the timetable Grid's per-row children as addressable nodes (same limitation documented on IsInfoRowTransitionTests). The fix lives in platform-agnostic VerticalTimetableRow.cs / StationNameConverter.cs and is verified on iPhone, iPad, macOS and Windows. (The Android job is the only UI-test job whose NUnit host is Linux.)")]
[Infrastructure.RetryAllTests(2)]
public class StationNameDisplayTests : Infrastructure.BaseUITest
{
	// Characters StationNameConverter inserts between glyphs to spread a short
	// name across the column (EN SPACE for 2/3-char, THIN SPACE for 4-char).
	// Strip them to recover the source name. Mirrors StationNameConverter.ConvertBack.
	private const char SPACE_CHAR = '\x2002';
	private const char THIN_SPACE = '\x2009';

	// One representative name per length, all present in the default sample
	// train (1-1-1): 津(1), 大宮(2), 南浦和(3), さ新都心(4). If a name wrapped and
	// dropped trailing glyphs (the bug), its stripped text would differ (e.g.
	// "さ新") and would be absent from the rendered set.
	private static readonly string[] Targets = ["津", "大宮", "南浦和", "さ新都心"];

	// Default sample train 1-1-1 has ~16 rows; scan a little past that.
	private const int MaxRowIndexToScan = 18;

	private StartHomePageObject _startHomePage = null!;

	protected override bool ShareSessionAcrossTestsInFixture => true;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		_startHomePage = new StartHomePageObject(Driver);

		if (!_startHomePage.PollDisplayed(AutomationIds.StartHome.Title, timeoutSeconds: 3))
		{
			new AppShellPage(Driver).NavigateToHome();
			_startHomePage = new StartHomePageObject(Driver);
		}
		_startHomePage.ClearLoaderForTesting();
		_startHomePage.AcceptPrivacyPolicyIfNeeded();
	}

	[Test]
	public void StationNames_OneToFourChars_AreFullyDisplayed()
	{
		Assert.That(_startHomePage.IsDisplayed(), Is.True);
		_startHomePage.LoadSample();
		_startHomePage.WaitForElement(AutomationIds.StartHome.WorkGroupList);

		var dtac = _startHomePage.AutoOpenForTesting();
		dtac.SwitchToTimetableTab();

		var rendered = new HashSet<string>();

		// Drive the lookups with a zero implicit wait so absent rows fail fast.
		// Only the setter is touched and it is restored to this codebase's
		// default (10 s, as IsInfoRowTransitionTests does) — the getter is
		// unimplemented on the Windows driver and must never be read.
		Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
		try
		{
			// The Grid is not virtualised, but mac2 only populates an element's
			// text once it is laid out on-screen, so sweep the visible rows,
			// swipe the timetable down, and sweep again until every target name
			// has been seen (or the swipe budget is exhausted).
			const int maxSweeps = 8;
			for (int sweep = 0; sweep < maxSweeps; sweep++)
			{
				for (int i = 0; i <= MaxRowIndexToScan; i++)
				{
					string? text = ReadStrippedStationName(i);
					if (!string.IsNullOrEmpty(text))
						rendered.Add(text!);
				}

				if (Targets.All(rendered.Contains))
					break;

				dtac.SwipeTimetableUp();
			}
		}
		finally
		{
			try { Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10); }
			catch { }
		}

		var missing = Targets.Where(t => !rendered.Contains(t)).ToList();
		Assert.That(
			missing,
			Is.Empty,
			$"Every 1–4 char station name must render in full. Missing: " +
			$"[{string.Join(", ", missing)}]. Rendered names seen: " +
			$"[{string.Join(", ", rendered.OrderBy(s => s.Length))}]. " +
			"A missing target with a shorter look-alike present (e.g. \"さ新\" " +
			"instead of \"さ新都心\") means the name wrapped and dropped glyphs.");
	}

	/// <summary>
	/// One fast lookup of a row's StationName label by AutomationId, returning
	/// its rendered text with the converter's inserted spacing stripped, or null
	/// when the row is absent / off-screen / empty.
	/// </summary>
	private string? ReadStrippedStationName(int rowIndex)
	{
		string automationId = string.Format(
			AutomationIds.DTAC.TimetableRowStationNamePattern, rowIndex);
		try
		{
			var elements = Driver.FindElements(AutomationIdLocator(automationId));
			foreach (var el in elements)
			{
				string? t = ExtractText(el);
				if (!string.IsNullOrEmpty(t))
					return Strip(t!);
			}
		}
		catch { }
		return null;
	}

	private static string Strip(string s)
	{
		var sb = new StringBuilder(s.Length);
		foreach (char c in s)
		{
			if (c != SPACE_CHAR && c != THIN_SPACE)
				sb.Append(c);
		}
		return sb.ToString();
	}

	/// <summary>
	/// Reads an element's rendered text. iOS/mac2 expose a Label's text through
	/// Selenium's <c>Text</c> (AXValue), so that alone is used there. WinUI
	/// surfaces a MAUI Label's text via the UIA <c>Name</c> property and leaves
	/// the Value pattern (Selenium <c>Text</c>) empty, so the Name/Value
	/// attributes are tried only on Windows — the same reason the
	/// NextTrainButton lookup falls back to an <c>@Name</c> XPath there.
	/// (XCUITest rejects the capitalised "Name"/"Value" attributes with a 500,
	/// so probing them on iOS would only spam the shared session.)
	/// </summary>
	private string? ExtractText(AppiumElement el)
	{
		try
		{
			string t = el.Text;
			if (!string.IsNullOrEmpty(t))
				return t;
		}
		catch { }

		if (Driver is WindowsDriver)
		{
			foreach (string attr in new[] { "Name", "Value" })
			{
				try
				{
					string? v = el.GetAttribute(attr);
					if (!string.IsNullOrEmpty(v))
						return v;
				}
				catch { }
			}
		}
		return null;
	}

	private By AutomationIdLocator(string automationId)
		=> IsAndroid ? By.Id(automationId) : MobileBy.AccessibilityId(automationId);
}
