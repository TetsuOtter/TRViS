using TRViS.Services;

namespace TRViS.LocationService.Tests;

public class Tests
{
	[Test]
	public void InitializeTest()
	{
		LonLatLocationService service = new();
		Assert.Multiple(() =>
		{
			Assert.That(service.StaLocationInfo, Is.Null);
			Assert.That(service.CurrentStationIndex, Is.EqualTo(-1));
			Assert.That(service.IsRunningToNextStation, Is.False);
		});
	}

	[Test]
	public void MoveTest()
	{
		LonLatLocationService service = new()
		{
			StaLocationInfo =
			[
				new(0, 0, 0, 200),
				new(1, 1, 1, 200),
				new(2, 2, 2, 200),
			]
		};

		// 初期状態は、駅0にいる
		Assert.Multiple(() =>
		{
			Assert.That(service.CurrentStationIndex, Is.EqualTo(0));
			Assert.That(service.IsRunningToNextStation, Is.False);
		});

		service.SetCurrentLocation(0.1, 0.1);

		// (平均値を取るために、3回以上の移動が必要)
		Assert.Multiple(() =>
		{
			Assert.That(service.CurrentStationIndex, Is.EqualTo(0));
			Assert.That(service.IsRunningToNextStation, Is.False);
		});

		service.SetCurrentLocation(0.1, 0.1);
		service.SetCurrentLocation(0.1, 0.1);

		// 駅0を離れ、駅1に向かっている
		Assert.Multiple(() =>
		{
			Assert.That(service.CurrentStationIndex, Is.EqualTo(0));
			Assert.That(service.IsRunningToNextStation, Is.True);
		});

		service.SetCurrentLocation(1, 1);
		service.SetCurrentLocation(1, 1);
		service.SetCurrentLocation(1, 1);

		// ちょうど駅1にいる
		Assert.Multiple(() =>
		{
			Assert.That(service.CurrentStationIndex, Is.EqualTo(1));
			Assert.That(service.IsRunningToNextStation, Is.False);
		});

		service.SetCurrentLocation(2, 2);
		service.SetCurrentLocation(2, 2);
		service.SetCurrentLocation(2, 2);

		// 駅1を離れ、駅2に向かっている
		Assert.Multiple(() =>
		{
			Assert.That(service.CurrentStationIndex, Is.EqualTo(1));
			Assert.That(service.IsRunningToNextStation, Is.True);
		});

		service.SetCurrentLocation(2, 2);
		service.SetCurrentLocation(2, 2);
		service.SetCurrentLocation(2, 2);

		// ちょうど駅2にいる
		Assert.Multiple(() =>
		{
			Assert.That(service.CurrentStationIndex, Is.EqualTo(2));
			Assert.That(service.IsRunningToNextStation, Is.False);
		});

		service.SetCurrentLocation(3, 3);
		service.SetCurrentLocation(3, 3);
		service.SetCurrentLocation(3, 3);

		// 駅2から先には進まない
		Assert.Multiple(() =>
		{
			Assert.That(service.CurrentStationIndex, Is.EqualTo(2));
			Assert.That(service.IsRunningToNextStation, Is.False);
		});
	}

	[Test]
	public void ForceSetPositionTest_NearStation()
	{
		StaLocationInfo sta1 = new(0, 0, 0, 200);
		StaLocationInfo sta2 = new(1, 1, 1, 200);
		StaLocationInfo sta3 = new(2, 2, 2, 200);
		LonLatLocationService service = new()
		{
			StaLocationInfo =
			[
				sta1,
				sta2,
				sta3,
			]
		};

		// 初期状態は、駅0にいる
		Assert.Multiple(() =>
		{
			Assert.That(service.CurrentStationIndex, Is.EqualTo(0));
			Assert.That(service.IsRunningToNextStation, Is.False);
		});

		service.ForceSetLocationInfo(1, 1);

		Assert.Multiple(() =>
		{
			Assert.That(service.CurrentStationIndex, Is.EqualTo(1));
			Assert.That(service.IsRunningToNextStation, Is.False);
		});
	}

	[Test]
	public void ForceSetPositionTest_RunningToNextStation1()
	{
		StaLocationInfo sta1 = new(0, 0, 0, 200);
		StaLocationInfo sta2 = new(1, 1, 1, 200);
		StaLocationInfo sta3 = new(2, 2, 2, 200);
		LonLatLocationService service = new()
		{
			StaLocationInfo =
			[
				sta1,
				sta2,
				sta3,
			]
		};

		// 初期状態は、駅0にいる
		Assert.Multiple(() =>
		{
			Assert.That(service.CurrentStationIndex, Is.EqualTo(0));
			Assert.That(service.IsRunningToNextStation, Is.False);
		});

		service.ForceSetLocationInfo(0.5, 0.5);

		Assert.Multiple(() =>
		{
			Assert.That(service.CurrentStationIndex, Is.EqualTo(0));
			Assert.That(service.IsRunningToNextStation, Is.True);
		});
	}

	[Test]
	public void ForceSetPositionTest_RunningToNextStation2()
	{
		StaLocationInfo sta1 = new(0, 0, 0, 200);
		StaLocationInfo sta2 = new(1, 1, 1, 200);
		StaLocationInfo sta3 = new(2, 2, 2, 200);
		LonLatLocationService service = new()
		{
			StaLocationInfo =
			[
				sta1,
				sta2,
				sta3,
			]
		};

		// 初期状態は、駅0にいる
		Assert.Multiple(() =>
		{
			Assert.That(service.CurrentStationIndex, Is.EqualTo(0));
			Assert.That(service.IsRunningToNextStation, Is.False);
		});

		service.ForceSetLocationInfo(1.5, 1.5);

		Assert.Multiple(() =>
		{
			Assert.That(service.CurrentStationIndex, Is.EqualTo(1));
			Assert.That(service.IsRunningToNextStation, Is.True);
		});
	}
}
