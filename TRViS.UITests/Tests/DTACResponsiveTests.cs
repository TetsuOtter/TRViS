using System;
using System.Collections.Generic;
using System.Threading;

using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// issue #41 regression coverage: the 縦型時刻表 must adapt its columns to the
/// actual view width (Android narrow tablets / iPad multitasking / phones),
/// and the resolved <c>ViewWidthMode</c> must match the real layout width.
///
/// <para>
/// Before this work main set <c>VerticalTimetableColumnVisibilityState</c>
/// once from <c>DeviceDisplay.MainDisplayInfo.Width</c> (device pixels) at
/// construction and never again, so it was dead for resize/multitasking and
/// classified phones as iPad-wide. This fixture pins, device-independently,
/// that the seam reports a real positive layout width and that
/// <c>ClassifyWidth(width) == mode</c> — with the old bug, mode stays at the
/// seed while the real DIP width differs, so this assertion fails.
/// </para>
/// </summary>
[TestFixture]
[Infrastructure.RetryAllTests(2)] // see AppLaunchTests for rationale
public class DTACResponsiveTests : BaseUITest
{
	// Shared one session for the fixture; SetUp re-asserts the StartHome
	// pre-state via idempotent seams (same rationale as DTACTimetableTests).
	protected override bool ShareSessionAcrossTestsInFixture => true;

	// ViewWidthMode narrow→wide order. MUST mirror
	// TRViS.DTAC.ViewModels.VerticalTimetableColumnVisibilityState.ViewWidthMode.
	// The UITests project is a black-box Appium harness with no reference to
	// the app, so the contract is pinned here intentionally.
	private static readonly string[] WidthModeOrder =
	{
		"NARROW",
		"IPHONE_SE_V",
		"IPHONE_6_7_8_V",
		"IPHONE_6_7_8_PLUS_V",
		"IPHONE_SE_H",
		"IPHONE_6_7_8_H",
		"IPHONE_6_7_8_PLUS_H",
		"IPAD_MINI_6_V",
		"IPAD_MINI_2_3_4_5_V",
	};

	// Mirrors VerticalTimetableColumnVisibilityState.ClassifyWidth thresholds.
	private static string ExpectedMode(int w) =>
		w >= 768 ? "IPAD_MINI_2_3_4_5_V" :
		w >= 756 ? "IPAD_MINI_6_V" :
		w >= 736 ? "IPHONE_6_7_8_PLUS_H" :
		w >= 667 ? "IPHONE_6_7_8_H" :
		w >= 568 ? "IPHONE_SE_H" :
		w >= 414 ? "IPHONE_6_7_8_PLUS_V" :
		w >= 375 ? "IPHONE_6_7_8_V" :
		w >= 320 ? "IPHONE_SE_V" :
		"NARROW";

	private StartHomePageObject _startHomePage = null!;

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
	public void ResponsiveColumns_ModeMatchesRealWidth_AndFlagsStayConsistent()
	{
		Assert.That(_startHomePage.IsDisplayed(), Is.True,
			"StartHome should be displayed at fixture entry.");

		_startHomePage.LoadSample();
		_startHomePage.WaitForElement(AutomationIds.StartHome.WorkGroupList);
		var dtac = _startHomePage.AutoOpenForTesting();
		Assert.That(dtac.IsDisplayed(), Is.True,
			"DTAC view should be displayed after AutoOpenForTesting.");

		dtac.SwitchToTimetableTab();

		// Poll until the seam reports a real positive layout width. With the
		// #41 regression present the seam is only ever the ctor-time value
		// (TimetableView.Width = -1, no PropertyChanged ever fires because
		// UpdateState is never called again), so w stays <= 0 and this times
		// out — the test fails, which is the point.
		IReadOnlyDictionary<string, string> state = new Dictionary<string, string>();
		int w = -1;
		var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
		while (DateTime.UtcNow < deadline)
		{
			state = dtac.ReadColumnVisibilityState();
			if (state.TryGetValue("w", out var wRaw) && int.TryParse(wRaw, out w) && w > 0)
				break;
			Thread.Sleep(250);
		}

		Assert.That(w, Is.GreaterThan(0),
			"Responsive seam never reported a positive layout width. The width→" +
			"visibility path did not run for a real view size — the exact #41 " +
			"regression (visibility was seeded once from device width at " +
			"construction and never updated).");

		Assert.That(state.ContainsKey("mode"), Is.True, "Seam must report a ViewWidthMode.");
		string mode = state["mode"];
		int modeIdx = Array.IndexOf(WidthModeOrder, mode);
		Assert.That(modeIdx, Is.GreaterThanOrEqualTo(0),
			$"Unknown ViewWidthMode '{mode}' — seam/enum drift vs WidthModeOrder.");

		// The core #41 guard: the resolved mode must match the real width.
		Assert.That(mode, Is.EqualTo(ExpectedMode(w)),
			$"ViewWidthMode '{mode}' does not match the actual layout width {w}px " +
			$"(expected '{ExpectedMode(w)}'). The mode is stale / not tracking width.");

		bool Flag(string k) => state.TryGetValue(k, out var v) && v == "1";
		int Idx(string m) => Array.IndexOf(WidthModeOrder, m);

		// Flags recomputed from the resolved mode. Mirrors the static
		// predicates in VerticalTimetableColumnVisibilityState (anti-drift).
		bool expRunTime = modeIdx >= Idx("IPAD_MINI_6_V");
		bool expRunInOutLimit = modeIdx >= Idx("IPHONE_6_7_8_PLUS_H");
		bool expRemarks = modeIdx >= Idx("IPHONE_6_7_8_H");
		bool expMarker = modeIdx >= Idx("IPHONE_6_7_8_H");
		bool expStaNarrow = modeIdx <= Idx("IPHONE_6_7_8_PLUS_V");
		bool expTrackNarrow = modeIdx <= Idx("IPHONE_6_7_8_PLUS_V");

		Assert.Multiple(() =>
		{
			Assert.That(Flag("rt"), Is.EqualTo(expRunTime),
				$"RunTime visibility drifted from mode {mode}.");
			Assert.That(Flag("rl"), Is.EqualTo(expRunInOutLimit),
				$"RunInOutLimit visibility drifted from mode {mode}.");
			Assert.That(Flag("rm"), Is.EqualTo(expRemarks),
				$"Remarks visibility drifted from mode {mode}.");
			Assert.That(Flag("mk"), Is.EqualTo(expMarker),
				$"Marker visibility drifted from mode {mode}.");
			Assert.That(Flag("snn"), Is.EqualTo(expStaNarrow),
				$"StationName narrow flag drifted from mode {mode}.");
			Assert.That(Flag("tnn"), Is.EqualTo(expTrackNarrow),
				$"TrackName narrow flag drifted from mode {mode}.");
		});

		// The user-facing #41 promise: station names stay readable instead of
		// being clipped off-screen on narrow widths.
		Assert.That(dtac.HasVisibleStationName(), Is.True,
			"At least one station-name label must be visible to the user on the " +
			"test device's width (issue #41: content must fit, not be cut off).");
	}
}
