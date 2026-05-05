using NUnit.Framework;

using TRViS.LocationService.Abstractions;
using TRViS.NetworkSyncService;
using TRViS.NetworkSyncService.IntegrationTests.Helpers;

namespace TRViS.NetworkSyncService.IntegrationTests;

/// <summary>
/// HttpNetworkSyncService を使った統合テスト。
/// リファレンスサーバーと実際に HTTP 通信を行い、プロトコルの正確な動作を検証する。
/// </summary>
[TestFixture]
public class HttpIntegrationTests
{
	private ReferenceServerClient _control = null!;
	private HttpClient _httpClient = null!;
	private HttpNetworkSyncService _service = null!;

	[SetUp]
	public async Task SetUp()
	{
		_control = GlobalServerSetup.Server.ControlClient;
		await _control.ResetAsync();
		await _control.ClearHttpQueriesAsync();

		_httpClient = new HttpClient();
		var uri = new Uri(GlobalServerSetup.Server.HttpBaseUrl);
		_service = await NetworkSyncServiceUtil.CreateFromUriAsync(uri, _httpClient);
	}

	[TearDown]
	public void TearDown()
	{
		_service.Dispose();
		_httpClient.Dispose();
	}

	// ================================================================
	// プリフライト
	// ================================================================

	[Test]
	public async Task Preflight_ServerRespondsWithSuccess()
	{
		// CreateFromUriAsync の内部でプリフライトリクエストが成功していれば
		// この時点で _service は null ではない
		var state = await _control.GetStateAsync();
		Assert.That(state, Is.Not.Null);
	}

	// ================================================================
	// 時刻同期
	// ================================================================

	[Test]
	public async Task TimeSynced_CorrectTimeDeliveredViaTimeChangedEvent()
	{
		long expectedTime_ms = 43_200_000L; // 12:00:00
		await _control.SetStateAsync(time_ms: expectedTime_ms, canStart: true);

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var tcs = new TaskCompletionSource<int>();
		_service.TimeChanged += (_, t) => tcs.TrySetResult(t);

		await _service.TickAsync(cts.Token);

		int time_s = await tcs.Task.WaitAsync(cts.Token);
		Assert.That(time_s, Is.EqualTo(expectedTime_ms / 1000));
	}

	[Test]
	public async Task TimeSynced_MultiplePolls_TimeUpdatesCorrectly()
	{
		long time1 = 36_000_000L; // 10:00:00
		long time2 = 36_060_000L; // 10:01:00
		var receivedTimes = new List<int>();
		_service.TimeChanged += (_, t) => receivedTimes.Add(t);

		await _control.SetStateAsync(time_ms: time1, canStart: true);
		await _service.TickAsync();

		await _control.SetStateAsync(time_ms: time2, canStart: true);
		await _service.TickAsync();

		Assert.That(receivedTimes, Has.Count.EqualTo(2));
		Assert.That(receivedTimes[0], Is.EqualTo((int)(time1 / 1000)));
		Assert.That(receivedTimes[1], Is.EqualTo((int)(time2 / 1000)));
	}

	// ================================================================
	// CanStart
	// ================================================================

	[Test]
	public async Task CanStart_True_ServiceReportsCanStart()
	{
		await _control.SetStateAsync(time_ms: 0, canStart: true);
		await _service.TickAsync();
		Assert.That(_service.CanStart, Is.True);
	}

	[Test]
	public async Task CanStart_False_ServiceReportsCannotStart()
	{
		await _control.SetStateAsync(time_ms: 0, canStart: false);
		await _service.TickAsync();
		Assert.That(_service.CanStart, Is.False);
	}

	[Test]
	public async Task CanStart_TransitionFromFalseToTrue_CanStartChangedEventFired()
	{
		await _control.SetStateAsync(time_ms: 0, canStart: false);
		await _service.TickAsync();

		bool? receivedCanStart = null;
		_service.CanStartChanged += (_, v) => receivedCanStart = v;

		await _control.SetStateAsync(canStart: true);
		await _service.TickAsync();

		Assert.That(receivedCanStart, Is.True);
	}

	[Test]
	public async Task CanStart_TransitionFromTrueToFalse_CanStartChangedEventFired()
	{
		await _control.SetStateAsync(time_ms: 0, canStart: true);
		await _service.TickAsync();

		bool? receivedCanStart = null;
		_service.CanStartChanged += (_, v) => receivedCanStart = v;

		await _control.SetStateAsync(canStart: false);
		await _service.TickAsync();

		Assert.That(receivedCanStart, Is.False);
	}

	// ================================================================
	// CanUseService
	// ================================================================

	[Test]
	public async Task CanUseService_MatchesCanStart()
	{
		await _control.SetStateAsync(time_ms: 0, canStart: false);
		await _service.TickAsync();
		Assert.That(_service.CanUseService, Is.False);

		await _control.SetStateAsync(canStart: true);
		await _service.TickAsync();
		Assert.That(_service.CanUseService, Is.True);
	}

	[Test]
	public async Task CanUseServiceChanged_FiredWhenCanStartChanges()
	{
		await _control.SetStateAsync(time_ms: 0, canStart: false);
		await _service.TickAsync();

		bool? receivedValue = null;
		_service.CanUseServiceChanged += (_, v) => receivedValue = v;

		await _control.SetStateAsync(canStart: true);
		await _service.TickAsync();

		Assert.That(receivedValue, Is.True);
	}

	// ================================================================
	// クエリパラメータ転送
	// ================================================================

	[Test]
	public async Task QueryParams_WorkGroupIdSet_ServerReceivesWorkGroupIdInQuery()
	{
		_service.WorkGroupId = "wg-test";
		await _service.TickAsync();

		var queries = await _control.GetHttpQueriesAsync();
		// プリフライト + TickAsync 分のクエリが記録されている
		var tickQuery = queries.Last();
		Assert.That(tickQuery.WorkGroupId, Is.EqualTo("wg-test"));
	}

	[Test]
	public async Task QueryParams_AllIdsSet_ServerReceivesAllIds()
	{
		_service.WorkGroupId = "wg-1";
		_service.WorkId = "w-1";
		_service.TrainId = "t-1";
		await _service.TickAsync();

		var queries = await _control.GetHttpQueriesAsync();
		var tickQuery = queries.Last();
		Assert.Multiple(() =>
		{
			Assert.That(tickQuery.WorkGroupId, Is.EqualTo("wg-1"));
			Assert.That(tickQuery.WorkId, Is.EqualTo("w-1"));
			Assert.That(tickQuery.TrainId, Is.EqualTo("t-1"));
		});
	}

	[Test]
	public async Task QueryParams_IdsNotSet_ServerReceivesNullIds()
	{
		await _service.TickAsync();
		var queries = await _control.GetHttpQueriesAsync();
		var tickQuery = queries.Last();
		Assert.Multiple(() =>
		{
			Assert.That(tickQuery.WorkGroupId, Is.Null);
			Assert.That(tickQuery.WorkId, Is.Null);
			Assert.That(tickQuery.TrainId, Is.Null);
		});
	}

	// ================================================================
	// 位置情報
	// ================================================================

	[Test]
	public async Task Location_WithinStationRadius_StationIndexUpdated()
	{
		var stations = new StaLocationInfo[]
		{
			new(0.0,   135.0,   35.0,   200.0),
			new(1000.0, 135.01, 35.01, 200.0),
			new(2000.0, 135.02, 35.02, 200.0),
		};
		_service.StaLocationInfo = stations;
		_service.IsEnabled = true;
		// 駅2 (index=1) から駅1 (index=0) への変化を検知できるよう初期位置を設定
		_service.ForceSetLocationInfo(1, false);

		// 駅1 付近 (location_m = 0) の位置情報を送信
		await _control.SetStateAsync(location_m: 0.0, canStart: true);

		LocationStateChangedEventArgs? lastState = null;
		_service.LocationStateChanged += (_, e) => lastState = e;

		await _service.TickAsync();

		Assert.That(lastState, Is.Not.Null);
		Assert.That(lastState!.NewStationIndex, Is.EqualTo(0));
	}

	[Test]
	public async Task Location_Null_LocationStateNotChanged()
	{
		var stations = new StaLocationInfo[]
		{
			new(0.0, 135.0, 35.0, 200.0),
			new(1000.0, 135.01, 35.01, 200.0),
		};
		_service.StaLocationInfo = stations;
		_service.IsEnabled = true;
		_service.ForceSetLocationInfo(0, false);

		int locationChangedCount = 0;
		_service.LocationStateChanged += (_, _) => locationChangedCount++;

		// location_m を null (NaN) にして送信
		await _control.SetStateAsync(location_m: null, canStart: true);
		await _service.TickAsync();

		// NaN の場合は位置更新をスキップする
		Assert.That(locationChangedCount, Is.EqualTo(0));
	}
}
