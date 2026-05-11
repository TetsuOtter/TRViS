using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// Tests covering the horizontal-timetable button + page reachable from
/// PageHeader (時刻表 tab). The button is hidden unless the selected Work
/// carries an embedded horizontal timetable; the UI_TEST seed seam injects
/// one synthetically so the navigation path can be exercised.
/// </summary>
[TestFixture]
[Infrastructure.RetryAllTests(2)]
public class HorizontalTimetableTests : BaseUITest
{
	private StartHomePageObject _startHomePage = null!;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		_startHomePage = new StartHomePageObject(Driver);
		_startHomePage.AcceptPrivacyPolicyIfNeeded();
	}

	private DTACViewHostPageObject LoadSampleAndOpenDTAC()
	{
		Assert.That(_startHomePage.IsDisplayed(), Is.True);
		_startHomePage.LoadSample();
		_startHomePage.WaitForElement(AutomationIds.StartHome.WorkGroupList);
		return _startHomePage.AutoOpenForTesting();
	}

	/// <summary>
	/// The auto-open seam (<see cref="StartHomePageObject.AutoOpenForTesting"/>)
	/// commits the first WorkGroup + first Work, which in sample_data.json is a
	/// Work with HasETrainTimetable=false. So the 横型時刻表 button must stay
	/// hidden even though other Works in the fixture do carry an embedded
	/// timetable. Doubles as a regression marker: if PageHeader ever renders
	/// the button unconditionally, this test catches it.
	/// </summary>
	[Test]
	public void HorizontalTimetableButton_HiddenWithSampleData()
	{
		var dtac = LoadSampleAndOpenDTAC();
		dtac.SwitchToTimetableTab();

		Assert.That(
			dtac.IsHorizontalTimetableButtonVisible(timeoutSeconds: 2),
			Is.False,
			"The first sample Work has no embedded horizontal timetable; the button must stay hidden.");
	}

	/// <summary>
	/// Drives the full path: seed seam swaps in a synthetic Work with
	/// HasETrainTimetable=true + a 1×1 PNG, navigates to DTAC, taps the
	/// 時刻表 tab, and asserts the 横型時刻表 button is visible. We do not
	/// further assert WebView render content because cross-platform readback
	/// of WebView body is unreliable; reaching the page is the observable
	/// contract.
	/// </summary>
	[Test]
	public void HorizontalTimetableButton_Visible_NavigatesToHorizontalTimetablePage()
	{
		Assert.That(_startHomePage.IsDisplayed(), Is.True);
		_startHomePage.LoadSample();
		_startHomePage.WaitForElement(AutomationIds.StartHome.WorkGroupList);

		var dtac = _startHomePage.SeedHorizontalTimetableAndOpenForTesting();
		Assert.That(dtac.IsDisplayed(), Is.True);
		dtac.SwitchToTimetableTab();

		Assert.That(
			dtac.IsHorizontalTimetableButtonVisible(timeoutSeconds: 5),
			Is.True,
			"Seeded Work has HasETrainTimetable=true; the button should appear.");

		dtac.TapHorizontalTimetableButton();

		var page = new HorizontalTimetablePageObject(Driver);
		Assert.That(page.IsDisplayed(), Is.True,
			"HorizontalTimetablePage's WebView should be in the accessibility tree after tap.");
	}
}
