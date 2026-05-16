using OpenQA.Selenium;
using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// Regression for "4-character station names no longer fully display".
///
/// A 4-char name (sample data row "さ新都心") rendered at the fixed timetable
/// font overflowed the fixed station-name column, wrapped, and only the first
/// line ("さ新") stayed visible. The fix keeps the font and column width fixed
/// and instead widens only the station-name label past its cell.
///
/// The timetable is a non-virtualised Grid inside a ScrollView, so every row's
/// StationName label is realised in the tree from the start (off-screen rows
/// just report size 0×0 on mac2 / WinUI). This fixture therefore asserts the
/// label's full text content — every source character must be rendered, after
/// stripping the spacing characters StationNameConverter inserts between
/// glyphs — rather than its on-screen size, for 1-, 2-, 3- and 4-char names.
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

	// Default sample-data first train (1-1-1). RowIndex is the position in the
	// row list including info rows, so these are fixed:
	//   4 -> "津"      (1 char)
	//   5 -> "大宮"     (2 char)
	//   7 -> "南浦和"   (3 char)   (index 6 is the 交直切換 info row)
	//   8 -> "さ新都心" (4 char)
	private static readonly (int RowIndex, string Source)[] Cases =
	[
		(4, "津"),
		(5, "大宮"),
		(7, "南浦和"),
		(8, "さ新都心"),
	];

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

		foreach (var (rowIndex, source) in Cases)
		{
			string id = string.Format(AutomationIds.DTAC.TimetableRowStationNamePattern, rowIndex);

			// The Grid is not virtualised so the label is in the tree from the
			// start, but mac2 only populates an element's text once it is laid
			// out on-screen. Swipe the timetable down and retry until the row
			// surfaces its text (no-op fast path when it already has it, e.g.
			// iOS / on-screen rows).
			string? text = ReadStationNameText(id);
			for (int attempt = 0; string.IsNullOrEmpty(text) && attempt < 5; attempt++)
			{
				dtac.SwipeTimetableUp();
				text = ReadStationNameText(id);
			}

			Assert.That(
				text,
				Is.Not.Null.And.Not.Empty,
				$"Row {rowIndex} StationNameLabel ({source.Length}-char \"{source}\") must be " +
				"present with non-empty text in the timetable.");

			string stripped = text!
				.Replace(SPACE_CHAR.ToString(), string.Empty)
				.Replace(THIN_SPACE.ToString(), string.Empty);

			foreach (char c in source)
			{
				Assert.That(
					stripped,
					Does.Contain(c.ToString()),
					$"Row {rowIndex} StationNameLabel must render every character of " +
					$"\"{source}\" — char '{c}' missing from \"{stripped}\" " +
					"(4-char names previously wrapped and dropped trailing glyphs).");
			}
		}
	}

	/// <summary>
	/// Polls for the StationName label by AutomationId and returns its rendered
	/// text. The label is an HtmlAutoDetectLabel (a ContentView) whose inner
	/// Label mirrors the same AutomationId, so a lookup can match both the
	/// (text-less) wrapper and the inner Label — return the first match that
	/// actually carries text. Size is intentionally not checked: the Grid is
	/// not virtualised, so off-screen rows are present but report 0×0 on mac2
	/// / WinUI; the text content is the cross-platform-reliable signal.
	/// </summary>
	private string? ReadStationNameText(string automationId, double timeoutSeconds = 4)
	{
		var prevWait = TimeSpan.FromSeconds(10);
		var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
		var locator = AutomationIdLocator(automationId);
		try
		{
			Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
			while (DateTime.UtcNow < deadline)
			{
				try
				{
					var elements = Driver.FindElements(locator);
					foreach (var el in elements)
					{
						string? t = ExtractText(el);
						if (!string.IsNullOrEmpty(t))
							return t;
					}
				}
				catch { }
				Thread.Sleep(150);
			}
			return null;
		}
		finally
		{
			Driver.Manage().Timeouts().ImplicitWait = prevWait;
		}
	}

	/// <summary>
	/// Reads an element's rendered text across drivers. iOS/mac2 expose a
	/// Label's text through Selenium's <c>Text</c> (AXValue), but WinUI surfaces
	/// a MAUI Label's text via the UIA <c>Name</c> property and leaves the Value
	/// pattern (Selenium <c>Text</c>) empty — the same reason the NextTrainButton
	/// lookup falls back to an <c>@Name</c> XPath on Windows.
	/// </summary>
	private static string? ExtractText(AppiumElement el)
	{
		foreach (var read in new Func<string?>[]
		{
			() => el.Text,
			() => el.GetAttribute("Name"),
			() => el.GetAttribute("Value"),
		})
		{
			try
			{
				string? v = read();
				if (!string.IsNullOrEmpty(v))
					return v;
			}
			catch { }
		}
		return null;
	}

	private By AutomationIdLocator(string automationId)
		=> IsAndroid ? By.Id(automationId) : MobileBy.AccessibilityId(automationId);
}
