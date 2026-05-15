using OpenQA.Selenium;
using OpenQA.Selenium.Appium.iOS;
using OpenQA.Selenium.Appium.Mac;
using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// Regression test for "non-InfoRow components (station name, drive time, etc.)
/// remain visible after a WebSocket soft-update changes IsInfoRow from false to true
/// on a timetable row."
///
/// Uses the UI_TEST seam <c>DTAC.TestSeedIsInfoRowTransitionButton</c> which:
///   1. Takes the first non-InfoRow in the currently selected train,
///   2. Clones the TrainData with that row's IsInfoRow flipped to true, and
///   3. Re-sets AppViewModel.SelectedTrainData — triggering the same soft-update
///      code path as a WebSocket edit (same train ID → ApplyPositionAlignedDiff →
///      ApplyRowToExistingModel → PropertyChanged("IsInfoRow") → UpdateAllComponents).
///
/// The AutomationId-based assertions check the Grid.Children state indirectly:
///   - "TimetableRow.N.StationName" is present &amp; user-visible before the transition.
///   - After the transition: StationName is gone; "TimetableRow.N.InfoRow" is visible.
/// </summary>
[TestFixture]
[Infrastructure.RetryAllTests(2)]
public class IsInfoRowTransitionTests : Infrastructure.BaseUITest
{
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

	/// <summary>
	/// Core regression: after the IsInfoRow seam fires, the station-name label
	/// must disappear and the info-row label must appear.
	///
	/// Before the fix: UpdateAllComponents() in the IsInfoRow=true branch only
	/// called UpdateInfoRow() — station-name (and other non-InfoRow components)
	/// stayed in the Grid. This test would find StationName still visible → fail.
	///
	/// After the fix: RemoveNonInfoRowComponents() is called first, so StationName
	/// is removed from the Grid before UpdateInfoRow() adds the InfoRow label.
	/// </summary>
	[Test]
	public void InfoRowTransition_FalseToTrue_RemovesStationNameShowsInfoLabel()
	{
		// --- Setup: load sample data and navigate to DTAC timetable. ---
		Assert.That(_startHomePage.IsDisplayed(), Is.True);
		_startHomePage.LoadSample();
		_startHomePage.WaitForElement(AutomationIds.StartHome.WorkGroupList);

		var dtac = _startHomePage.AutoOpenForTesting();
		dtac.SwitchToTimetableTab();

		// Row 0 in the default sample train is "駅１" (IsInfoRow=false).
		// The UI_TEST build sets AutomationId = "TimetableRow.0.StationName" on it.
		string stationId = string.Format(AutomationIds.DTAC.TimetableRowStationNamePattern, 0);
		string infoRowId = string.Format(AutomationIds.DTAC.TimetableRowInfoRowPattern, 0);

		// --- Pre-condition: station-name label is visible before the transition. ---
		Assert.That(
			IsElementUserVisible(stationId),
			Is.True,
			"Row 0 StationNameLabel must be visible before the IsInfoRow transition.");

		// --- Action: trigger the seam that simulates the WebSocket IsInfoRow edit. ---
		var seamButton = dtac.WaitForElement(AutomationIds.DTAC.TestSeedIsInfoRowTransitionButton);
		seamButton.Click();
		Thread.Sleep(500); // allow the PropertyChanged cascade to settle

		// --- Post-condition (FAILS before fix): station-name must be gone. ---
		Assert.That(
			IsElementUserVisible(stationId),
			Is.False,
			"Row 0 StationNameLabel must NOT be visible after IsInfoRow changed to true " +
			"(non-InfoRow components must be removed from the Grid).");

		// --- Post-condition: info-row label must be visible. ---
		Assert.That(
			IsElementUserVisible(infoRowId),
			Is.True,
			"Row 0 InfoRowLabel must be visible after IsInfoRow changed to true.");
	}

	/// <summary>
	/// Returns true when the element is findable AND genuinely visible (non-zero size).
	/// Combining Displayed + Size disambiguates the Mac Catalyst quirk where unparented
	/// elements with an AutomationId are surfaced as Displayed=true but with 0×0 size.
	/// </summary>
	private bool IsElementUserVisible(string automationId, double timeoutSeconds = 3)
	{
		var prevWait = Driver.Manage().Timeouts().ImplicitWait;
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
						if (el.Displayed && el.Size.Width > 0 && el.Size.Height > 0)
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
