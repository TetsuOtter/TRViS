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
/// This fixture loads the sample data and asserts the station-name labels for
/// 1-, 2-, 3- and 4-character names are all present, user-visible, and carry
/// their full source text (stripped of the spacing characters the
/// StationNameConverter inserts between glyphs).
/// </summary>
[TestFixture]
[Platform(Exclude = "Linux", Reason = "Android UIAutomator2 does not expose the timetable Grid's per-row children as addressable nodes (same limitation documented on IsInfoRowTransitionTests). The fix lives in platform-agnostic VerticalTimetableRow.cs / StationNameConverter.cs and is verified on iPhone, iPad, macOS and Windows. (The Android job is the only UI-test job whose NUnit host is Linux.)")]
[Infrastructure.RetryAllTests(2)]
public class StationNameDisplayTests : Infrastructure.BaseUITest
{
	// Characters StationNameConverter inserts between glyphs to spread a short
	// name across the column (EN SPACE for 2/3-char, THIN SPACE for 4-char).
	// Strip them to recover the source name for the text assertion. Mirrors
	// StationNameConverter.ConvertBack.
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

			Assert.That(
				IsElementUserVisible(id),
				Is.True,
				$"Row {rowIndex} StationNameLabel ({source.Length}-char \"{source}\") must be visible.");

			string? text = ReadElementText(id);
			if (!string.IsNullOrEmpty(text))
			{
				string stripped = text
					.Replace(SPACE_CHAR.ToString(), string.Empty)
					.Replace(THIN_SPACE.ToString(), string.Empty);
				foreach (char c in source)
				{
					Assert.That(
						stripped,
						Does.Contain(c.ToString()),
						$"Row {rowIndex} StationNameLabel must render every character of " +
						$"\"{source}\" — char '{c}' missing from \"{stripped}\" " +
						$"(4-char names previously wrapped and dropped trailing glyphs).");
				}
			}
		}
	}

	private string? ReadElementText(string automationId)
	{
		try
		{
			var elements = Driver.FindElements(AutomationIdLocator(automationId));
			return elements.Count > 0 ? elements[0].Text : null;
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// Findable AND laid out (non-zero size). Size-only (not <c>Displayed</c>)
	/// is the cross-platform-reliable signal — see IsInfoRowTransitionTests for
	/// the full rationale (off-screen / wider-than-screen parents cascade
	/// Displayed=false while the child frame stays non-zero).
	/// </summary>
	private bool IsElementUserVisible(string automationId, double timeoutSeconds = 5)
	{
		var prevWait = TimeSpan.FromSeconds(10);
		var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
		var locator = AutomationIdLocator(automationId);
		try
		{
			Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
			while (DateTime.UtcNow < deadline)
			{
				var elements = Driver.FindElements(locator);
				if (elements.Count > 0)
				{
					try
					{
						var el = elements[0];
						if (el.Size.Width > 0 && el.Size.Height > 0)
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

	private By AutomationIdLocator(string automationId)
		=> IsAndroid ? By.Id(automationId) : MobileBy.AccessibilityId(automationId);
}
