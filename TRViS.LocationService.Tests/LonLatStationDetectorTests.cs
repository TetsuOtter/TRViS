using NUnit.Framework;

using TRViS.LocationService.Abstractions;
using TRViS.NetworkSyncService.Internals;

namespace TRViS.LocationService.Tests;

/// <summary>
/// <see cref="LonLatStationDetector"/> の単体テスト。
/// NetworkSyncService が <see cref="TRViS.NetworkSyncService.SyncedData.Location_m"/> = NaN かつ
/// 緯度経度ありの SyncedData を受信したときの駅判定アルゴリズムを検証する。
/// </summary>
[TestFixture]
public class LonLatStationDetectorTests
{
	private static StaLocationInfo[] ThreeStationsClose() =>
	[
		// (location_m, lon, lat, nearby_radius_m)
		new(0.0,    135.0,   35.0,   200.0),
		new(1000.0, 135.01, 35.01, 200.0),
		new(2000.0, 135.02, 35.02, 200.0),
	];

	[Test]
	public void InitialFix_AtFirstStation_PicksFirstStation()
	{
		var detector = new LonLatStationDetector();
		detector.SetStationLocations(ThreeStationsClose());

		detector.UpdateWithLonLat(135.0, 35.0);

		Assert.Multiple(() =>
		{
			Assert.That(detector.CurrentStationIndex, Is.EqualTo(0));
			Assert.That(detector.IsRunningToNextStation, Is.False);
			Assert.That(detector.IsFirstFix, Is.False);
		});
	}

	[Test]
	public void InitialFix_NearMiddleStation_PicksMiddleStation_ReportsChanged()
	{
		var detector = new LonLatStationDetector();
		detector.SetStationLocations(ThreeStationsClose());

		// 初期状態は index=0 (Reset 後の最初の有効駅)。駅2 (index=1) 付近を測位
		bool changed = detector.UpdateWithLonLat(135.01, 35.01);

		Assert.Multiple(() =>
		{
			Assert.That(detector.CurrentStationIndex, Is.EqualTo(1));
			Assert.That(detector.IsRunningToNextStation, Is.False);
			Assert.That(changed, Is.True, "0 → 1 に動いたので変化フラグは true");
		});
	}

	[Test]
	public void Reset_RestoresInitialState()
	{
		var detector = new LonLatStationDetector();
		detector.SetStationLocations(ThreeStationsClose());
		detector.UpdateWithLonLat(135.0, 35.0);

		detector.Reset();

		Assert.That(detector.IsFirstFix, Is.True);
	}

	[Test]
	public void SetStationLocations_Null_DoesNotCrashOnUpdate()
	{
		var detector = new LonLatStationDetector();
		detector.SetStationLocations(null);

		Assert.DoesNotThrow(() => detector.UpdateWithLonLat(135.0, 35.0));
		Assert.That(detector.CurrentStationIndex, Is.LessThan(0));
	}

	[Test]
	public void Sync_ClearsHistoryButKeepsIndex()
	{
		var detector = new LonLatStationDetector();
		detector.SetStationLocations(ThreeStationsClose());
		detector.UpdateWithLonLat(135.0, 35.0);

		detector.Sync(currentStationIndex: 2, isRunningToNextStation: true);

		Assert.Multiple(() =>
		{
			Assert.That(detector.CurrentStationIndex, Is.EqualTo(2));
			Assert.That(detector.IsRunningToNextStation, Is.True);
			Assert.That(detector.IsFirstFix, Is.False);
		});
	}
}
