namespace TRViS.Core.Tests;

public class MapRecenterThrottleTests
{
	static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void ShouldRecenter_FirstCall_AlwaysRecenters()
	{
		// Arrange
		var throttle = new MapRecenterThrottle(minIntervalSeconds: 2.0, minDistanceMeters: 30.0);

		// Act
		var result = throttle.ShouldRecenter(35.681, 139.766, T0);

		// Assert
		Assert.True(result);
	}

	[Fact]
	public void ShouldRecenter_TinyMoveWithinThresholds_DoesNotRecenter()
	{
		// Arrange
		var throttle = new MapRecenterThrottle(minIntervalSeconds: 2.0, minDistanceMeters: 30.0);
		throttle.ShouldRecenter(35.681, 139.766, T0); // 初回 recenter

		// Act: 約 1m の移動・10 秒経過 (距離が閾値未満)
		var result = throttle.ShouldRecenter(35.681009, 139.766, T0.AddSeconds(10));

		// Assert
		Assert.False(result);
	}

	[Fact]
	public void ShouldRecenter_MovedFarButWithinInterval_DoesNotRecenter()
	{
		// Arrange
		var throttle = new MapRecenterThrottle(minIntervalSeconds: 2.0, minDistanceMeters: 30.0);
		throttle.ShouldRecenter(35.681, 139.766, T0); // 初回 recenter

		// Act: 十分移動 (>100m) しているが、まだ 1 秒しか経っていない
		var result = throttle.ShouldRecenter(35.682, 139.766, T0.AddSeconds(1));

		// Assert
		Assert.False(result);
	}

	[Fact]
	public void ShouldRecenter_MovedFarAndPastInterval_Recenters()
	{
		// Arrange
		var throttle = new MapRecenterThrottle(minIntervalSeconds: 2.0, minDistanceMeters: 30.0);
		throttle.ShouldRecenter(35.681, 139.766, T0); // 初回 recenter

		// Act: 約 111m 移動 (緯度 0.001 度) かつ 3 秒経過
		var result = throttle.ShouldRecenter(35.682, 139.766, T0.AddSeconds(3));

		// Assert
		Assert.True(result);
	}

	[Fact]
	public void ShouldRecenter_AfterRecenter_StateAdvancesFromNewPosition()
	{
		// Arrange
		var throttle = new MapRecenterThrottle(minIntervalSeconds: 2.0, minDistanceMeters: 30.0);
		throttle.ShouldRecenter(35.681, 139.766, T0);
		Assert.True(throttle.ShouldRecenter(35.682, 139.766, T0.AddSeconds(3))); // 2 回目 recenter

		// Act: 2 回目の位置からほとんど動いていない (時間は十分経過)
		var result = throttle.ShouldRecenter(35.682005, 139.766, T0.AddSeconds(10));

		// Assert: 基準が 2 回目の位置に更新されているので recenter しない
		Assert.False(result);
	}

	[Fact]
	public void ShouldRecenter_BothConditionsRequired_TimeOkButDistanceShort()
	{
		// Arrange: 時間は経過するが移動が足りないケース
		var throttle = new MapRecenterThrottle(minIntervalSeconds: 2.0, minDistanceMeters: 30.0);
		throttle.ShouldRecenter(35.681, 139.766, T0);

		// Act: 5 秒経過しているが移動は数 m のみ
		var result = throttle.ShouldRecenter(35.681010, 139.766020, T0.AddSeconds(5));

		// Assert
		Assert.False(result);
	}

	[Fact]
	public void Reset_ForcesRecenterOnNextCall()
	{
		// Arrange
		var throttle = new MapRecenterThrottle(minIntervalSeconds: 2.0, minDistanceMeters: 30.0);
		throttle.ShouldRecenter(35.681, 139.766, T0);

		// Act
		throttle.Reset();
		var result = throttle.ShouldRecenter(35.681, 139.766, T0.AddSeconds(0.1)); // 同位置・即時でも

		// Assert
		Assert.True(result);
	}

	[Theory]
	// 緯度 1 度 ≒ 111.19 km
	[InlineData(35.0, 139.0, 36.0, 139.0, 111_000, 112_000)]
	// 同一地点は 0m
	[InlineData(35.681, 139.766, 35.681, 139.766, 0, 1)]
	public void DistanceMeters_IsWithinExpectedRange(
		double lat1, double lon1, double lat2, double lon2, double minMeters, double maxMeters)
	{
		// Act
		var d = MapRecenterThrottle.DistanceMeters(lat1, lon1, lat2, lon2);

		// Assert
		Assert.InRange(d, minMeters, maxMeters);
	}
}
