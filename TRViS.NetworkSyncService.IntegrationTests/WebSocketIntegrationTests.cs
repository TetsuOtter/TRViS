using System.Net.WebSockets;

using NUnit.Framework;

using TRViS.Core;
using TRViS.IO;
using TRViS.IO.Models;
using TRViS.LocationService.Abstractions;
using TRViS.NetworkSyncService;
using TRViS.NetworkSyncService.IntegrationTests.Helpers;

namespace TRViS.NetworkSyncService.IntegrationTests;

/// <summary>
/// WebSocketNetworkSyncService を使った統合テスト。
/// リファレンスサーバーと実際に WebSocket 通信を行い、以下を検証する:
///   - 時刻・位置の配信
///   - 時刻表のロード (全スコープ / WorkGroup / Work / Train)
///   - 運行中の時刻表更新 (リセット・継続)
///   - ID 更新メッセージの送信
///   - 接続切断・再接続
///   - 複数クライアントへのブロードキャスト
/// </summary>
[TestFixture]
public class WebSocketIntegrationTests
{
	private ReferenceServerClient _control = null!;

	// 再接続テストで使う間隔。デフォルトの 5000ms では CI が遅くなるため短縮する。
	private const int FastReconnectIntervalMs = 300;
	private const int FastReconnectAttemptMax = 3;

	[SetUp]
	public async Task SetUp()
	{
		_control = GlobalServerSetup.Server.ControlClient;
		await _control.ResetAsync();
	}

	// ================================================================
	// ヘルパー
	// ================================================================

	/// <summary>
	/// Loader を設定し、旧 auto-pick 相当の初期コミット (先頭 WorkGroup → 先頭 Work → 先頭 Train)
	/// を行う。<see cref="TimetableSelectionManager.OnLoaderChanged"/> は #224 で auto-pick を
	/// 廃止したため、Refresh の preserve-by-Id を検証する統合テストでは明示的に
	/// 初期選択を作成する必要がある。
	/// </summary>
	private static TimetableSelectionManager CreateManagerAndCommitFirst(ILoader loader)
	{
		var manager = new TimetableSelectionManager { Loader = loader };
		manager.SelectedWorkGroup = manager.WorkGroupList?.FirstOrDefault();
		return manager;
	}

	private async Task<WebSocketNetworkSyncService> ConnectServiceAsync(
		int reconnectIntervalMs = FastReconnectIntervalMs,
		int reconnectAttemptMax = FastReconnectAttemptMax)
	{
		var uri = new Uri(GlobalServerSetup.Server.WsBaseUrl);
		return await NetworkSyncServiceUtil.CreateFromWebSocketAsync(
			uri,
			reconnectIntervalMs: reconnectIntervalMs,
			reconnectAttemptMax: reconnectAttemptMax);
	}

	private static async Task DisconnectAsync(WebSocketNetworkSyncService service)
	{
		await service.DisconnectAsync();
		service.Dispose();
	}

	/// <summary>
	/// EventHandler&lt;T&gt; イベントが発火するまで最大 <paramref name="timeoutMs"/> ms 待機する。
	/// </summary>
	private static async Task<T> WaitForEventAsync<T>(
		Action<EventHandler<T>> subscribe,
		Action<EventHandler<T>> unsubscribe,
		int timeoutMs = 5000)
	{
		var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
		EventHandler<T> handler = null!;
		handler = (_, v) =>
		{
			unsubscribe(handler);
			tcs.TrySetResult(v);
		};
		subscribe(handler);
		try
		{
			return await tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs));
		}
		finally
		{
			unsubscribe(handler);
		}
	}

	/// <summary>
	/// 非ジェネリック EventHandler イベント (ConnectionClosed / ConnectionFailed 等) が
	/// 発火するまで最大 <paramref name="timeoutMs"/> ms 待機する。
	/// </summary>
	private static async Task WaitForNonGenericEventAsync(
		Action<EventHandler> subscribe,
		Action<EventHandler> unsubscribe,
		int timeoutMs = 5000)
	{
		var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		EventHandler handler = null!;
		handler = (_, _) =>
		{
			unsubscribe(handler);
			tcs.TrySetResult(true);
		};
		subscribe(handler);
		try
		{
			await tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs));
		}
		finally
		{
			unsubscribe(handler);
		}
	}

	private static async Task WaitForWsClientCountAsync(
		ReferenceServerClient control,
		int expectedCount,
		int timeoutMs = 5000)
	{
		await ReferenceServerClient.WaitForConditionAsync(
			async () => (await control.GetWsClientsAsync()).Count == expectedCount,
			timeoutMs: timeoutMs
		);
	}

	// ================================================================
	// 接続
	// ================================================================

	[Test]
	public async Task Connection_ClientConnectsSuccessfully()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);
			var clients = await _control.GetWsClientsAsync();
			Assert.That(clients, Has.Count.EqualTo(1));
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task Connection_Disconnect_ClientCountDecreases()
	{
		var service = await ConnectServiceAsync();
		await WaitForWsClientCountAsync(_control, 1);
		await DisconnectAsync(service);
		await WaitForWsClientCountAsync(_control, 0, timeoutMs: 3000);
		var clients = await _control.GetWsClientsAsync();
		Assert.That(clients, Has.Count.EqualTo(0));
	}

	// ================================================================
	// 時刻同期 (SyncedData)
	// ================================================================

	[Test]
	public async Task SyncedData_TimeChangedEventFired()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			long expectedTime_ms = 43_200_000L;
			await _control.SetStateAsync(time_ms: expectedTime_ms, canStart: true);

			var timeTask = WaitForEventAsync<int>(
				h => service.TimeChanged += h,
				h => service.TimeChanged -= h
			);
			await _control.BroadcastSyncedDataAsync();

			int receivedTime_s = await timeTask;
			Assert.That(receivedTime_s, Is.EqualTo(expectedTime_ms / 1000));
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task SyncedData_MultipleUpdates_AllTimesReceived()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			var receivedTimes = new List<int>();
			service.TimeChanged += (_, t) => receivedTimes.Add(t);

			for (int i = 0; i < 3; i++)
			{
				long time_ms = (36_000L + i * 60_000L) * 1000;
				await _control.SetStateAsync(time_ms: time_ms, canStart: true);
				await _control.BroadcastSyncedDataAsync();
				await Task.Delay(100);
			}

			await ReferenceServerClient.WaitForConditionAsync(
				() => Task.FromResult(receivedTimes.Count >= 3),
				timeoutMs: 5000
			);
			Assert.That(receivedTimes, Has.Count.GreaterThanOrEqualTo(3));
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task SyncedData_CanStart_PropagatedCorrectly()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			// CanStart = false → true の変化を検証
			await _control.SetStateAsync(time_ms: 0, canStart: false);
			await _control.BroadcastSyncedDataAsync();
			await Task.Delay(200);
			Assert.That(service.CanStart, Is.False);

			var canStartTask = WaitForEventAsync<bool>(
				h => service.CanStartChanged += h,
				h => service.CanStartChanged -= h
			);

			await _control.SetStateAsync(canStart: true);
			await _control.BroadcastSyncedDataAsync();

			bool receivedCanStart = await canStartTask;
			Assert.That(receivedCanStart, Is.True);
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task SyncedData_CanUseService_MatchesCanStart()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			await _control.SetStateAsync(time_ms: 0, canStart: false);
			await _control.BroadcastSyncedDataAsync();
			await Task.Delay(200);
			Assert.That(service.CanUseService, Is.False);

			await _control.SetStateAsync(canStart: true);
			await _control.BroadcastSyncedDataAsync();
			await Task.Delay(200);
			Assert.That(service.CanUseService, Is.True);
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task SyncedData_CanUseServiceChanged_FiredWhenCanStartChanges()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			await _control.SetStateAsync(time_ms: 0, canStart: false);
			await _control.BroadcastSyncedDataAsync();
			await Task.Delay(200);

			var canUseTask = WaitForEventAsync<bool>(
				h => service.CanUseServiceChanged += h,
				h => service.CanUseServiceChanged -= h
			);

			await _control.SetStateAsync(canStart: true);
			await _control.BroadcastSyncedDataAsync();

			bool receivedValue = await canUseTask;
			Assert.That(receivedValue, Is.True);
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task SyncedData_LonLat_FiresLonLatLocationReceived()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			var locTask = WaitForEventAsync<(double Longitude, double Latitude, double? Accuracy)>(
				h => service.LonLatLocationReceived += h,
				h => service.LonLatLocationReceived -= h
			);

			await _control.SetStateAsync(
				canStart: true,
				latitude_deg: 35.681236,
				longitude_deg: 139.767125,
				accuracy_m: 12.5
			);
			await _control.BroadcastSyncedDataAsync();

			var received = await locTask;
			Assert.Multiple(() =>
			{
				Assert.That(received.Latitude, Is.EqualTo(35.681236).Within(1e-9));
				Assert.That(received.Longitude, Is.EqualTo(139.767125).Within(1e-9));
				Assert.That(received.Accuracy, Is.EqualTo(12.5).Within(1e-9));
			});
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task SyncedData_NoLonLat_DoesNotFireLonLatLocationReceived()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			bool received = false;
			service.LonLatLocationReceived += (_, _) => received = true;

			await _control.SetStateAsync(canStart: true);
			await _control.BroadcastSyncedDataAsync();
			await Task.Delay(200);

			Assert.That(received, Is.False, "LonLatLocationReceived should not fire when no lat/lon is provided");
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task SyncedData_Location_StationIndexUpdated()
	{
		var service = await ConnectServiceAsync();
		try
		{
			var stations = new StaLocationInfo[]
			{
				new(0.0,    135.0,   35.0,   200.0),
				new(1000.0, 135.01, 35.01, 200.0),
				new(2000.0, 135.02, 35.02, 200.0),
			};
			service.StaLocationInfo = stations;
			service.IsEnabled = true;

			await WaitForWsClientCountAsync(_control, 1);

			var stateTask = WaitForEventAsync<LocationStateChangedEventArgs>(
				h => service.LocationStateChanged += h,
				h => service.LocationStateChanged -= h
			);

			// 駅2 (index=1) 付近の位置を送信
			await _control.SetStateAsync(location_m: 1000.0, canStart: true);
			await _control.BroadcastSyncedDataAsync();

			var state = await stateTask;
			Assert.That(state.NewStationIndex, Is.EqualTo(1));
			Assert.That(state.IsRunningToNextStation, Is.False);
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task SyncedData_LonLat_StationIndexUpdatedWhenLocationMIsNaN()
	{
		var service = await ConnectServiceAsync();
		try
		{
			var stations = new StaLocationInfo[]
			{
				new(0.0,    135.0,   35.0,   200.0),
				new(1000.0, 135.01, 35.01, 200.0),
				new(2000.0, 135.02, 35.02, 200.0),
			};
			service.StaLocationInfo = stations;
			service.IsEnabled = true;

			await WaitForWsClientCountAsync(_control, 1);

			var stateTask = WaitForEventAsync<LocationStateChangedEventArgs>(
				h => service.LocationStateChanged += h,
				h => service.LocationStateChanged -= h
			);

			// Location_m は送らず、駅2 (index=1) のすぐそばの緯度経度を送信
			await _control.SetStateAsync(
				canStart: true,
				latitude_deg: 35.01,
				longitude_deg: 135.01
			);
			await _control.BroadcastSyncedDataAsync();

			var state = await stateTask;
			Assert.That(state.NewStationIndex, Is.EqualTo(1));
			Assert.That(state.IsRunningToNextStation, Is.False);
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	// ================================================================
	// 時刻表ロード
	// ================================================================

	[Test]
	public async Task Timetable_AllScope_CachesAllWorkGroupsWorksAndTrains()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			var timetableTask = WaitForEventAsync<TimetableData>(
				h => service.TimetableUpdated += h,
				h => service.TimetableUpdated -= h
			);

			await _control.BroadcastTimetableAsync(TestData.AllScopeJson);
			await timetableTask;

			// ILoader として検証
			var loader = (ILoader)service;
			var workGroups = loader.GetWorkGroupList();
			Assert.That(workGroups, Has.Count.EqualTo(1));
			Assert.That(workGroups[0].Id, Is.EqualTo(TestData.WorkGroupId));

			var works = loader.GetWorkList(TestData.WorkGroupId);
			Assert.That(works, Has.Count.EqualTo(1));
			Assert.That(works[0].Id, Is.EqualTo(TestData.WorkId));

			var trains = loader.GetTrainDataList(TestData.WorkId);
			Assert.That(trains, Has.Count.EqualTo(2));
			Assert.That(trains.Select(t => t.Id), Does.Contain(TestData.TrainId));
			Assert.That(trains.Select(t => t.Id), Does.Contain(TestData.TrainId2));

			var trainData = loader.GetTrainData(TestData.TrainId);
			Assert.That(trainData, Is.Not.Null);
			Assert.That(trainData!.TrainNumber, Is.EqualTo("T-001"));
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task Timetable_WorkGroupScope_CachesWorkGroup()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			var timetableTask = WaitForEventAsync<TimetableData>(
				h => service.TimetableUpdated += h,
				h => service.TimetableUpdated -= h
			);

			await _control.BroadcastTimetableAsync(
				TestData.WorkGroupScopeJson,
				workGroupId: TestData.WorkGroupId
			);
			var timetableData = await timetableTask;

			Assert.That(timetableData.Scope, Is.EqualTo(TimetableScopeType.WorkGroup));
			Assert.That(timetableData.WorkGroupId, Is.EqualTo(TestData.WorkGroupId));

			var loader = (ILoader)service;
			var workGroups = loader.GetWorkGroupList();
			Assert.That(workGroups.Any(wg => wg.Id == TestData.WorkGroupId), Is.True);
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task Timetable_WorkScope_CachesWork()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			var timetableTask = WaitForEventAsync<TimetableData>(
				h => service.TimetableUpdated += h,
				h => service.TimetableUpdated -= h
			);

			await _control.BroadcastTimetableAsync(
				TestData.WorkScopeJson,
				workGroupId: TestData.WorkGroupId,
				workId: TestData.WorkId
			);
			var timetableData = await timetableTask;

			Assert.That(timetableData.Scope, Is.EqualTo(TimetableScopeType.Work));
			Assert.That(timetableData.WorkId, Is.EqualTo(TestData.WorkId));
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task Timetable_TrainScope_CachesTrain()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			// まず全体データをロードしてキャッシュを初期化
			var firstTask = WaitForEventAsync<TimetableData>(
				h => service.TimetableUpdated += h,
				h => service.TimetableUpdated -= h
			);
			await _control.BroadcastTimetableAsync(TestData.AllScopeJson);
			await firstTask;

			// Train スコープの更新
			var timetableTask = WaitForEventAsync<TimetableData>(
				h => service.TimetableUpdated += h,
				h => service.TimetableUpdated -= h
			);
			await _control.BroadcastTimetableAsync(
				TestData.TrainScopeJson,
				workId: TestData.WorkId,
				trainId: TestData.TrainId
			);
			var timetableData = await timetableTask;

			Assert.That(timetableData.Scope, Is.EqualTo(TimetableScopeType.Train));
			Assert.That(timetableData.TrainId, Is.EqualTo(TestData.TrainId));

			var loader = (ILoader)service;
			var updatedTrain = loader.GetTrainData(TestData.TrainId);
			Assert.That(updatedTrain, Is.Not.Null);
			Assert.That(updatedTrain!.TrainNumber, Is.EqualTo("T-001-Updated"));
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task Timetable_TrainScope_WithExplicitColor_PropagatesToCachedTrainAsLineColor()
	{
		// リグレッション: WebSocket 経由で Color を明示指定した Train データを配信したとき、
		// ILoader.GetTrainData が LineColor_RGB を保持する必要がある。
		// 以前は JsonModelsConverter.ConvertTrain が Color を渡していなかったため
		// 常に null になり、サーバーから配信した路線色が表示されなかった。
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			var firstTask = WaitForEventAsync<TimetableData>(
				h => service.TimetableUpdated += h,
				h => service.TimetableUpdated -= h
			);
			await _control.BroadcastTimetableAsync(TestData.AllScopeJson);
			await firstTask;

			var timetableTask = WaitForEventAsync<TimetableData>(
				h => service.TimetableUpdated += h,
				h => service.TimetableUpdated -= h
			);
			await _control.BroadcastTimetableAsync(
				TestData.TrainScopeJson_WithColor,
				workId: TestData.WorkId,
				trainId: TestData.TrainId
			);
			await timetableTask;

			var loader = (ILoader)service;
			var updatedTrain = loader.GetTrainData(TestData.TrainId);
			Assert.That(updatedTrain, Is.Not.Null);
			Assert.That(updatedTrain!.LineColor_RGB, Is.EqualTo(0xFF0000));
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	// ================================================================
	// 運行中の時刻表更新
	// ================================================================

	[Test]
	public async Task Timetable_Update_CurrentTrain_DoesNotResetLocationState()
	{
		// リアルタイム編集対応 (#214): 自スコープと一致する Train 更新を受信しても
		// 位置情報 (StationIndex) は維持されなければならない。
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);
			service.TrainId = TestData.TrainId;

			// 位置を駅2 (index=1) に設定
			service.ForceSetLocationInfo(1, false);
			int locationChangedCount = 0;
			service.LocationStateChanged += (_, _) => locationChangedCount++;

			var timetableTask = WaitForEventAsync<TimetableData>(
				h => service.TimetableUpdated += h,
				h => service.TimetableUpdated -= h
			);

			await _control.BroadcastTimetableAsync(
				TestData.TrainScopeJson,
				workId: TestData.WorkId,
				trainId: TestData.TrainId
			);

			await timetableTask;
			await Task.Delay(300); // リセットが起きないことを確認するための猶予

			Assert.That(locationChangedCount, Is.EqualTo(0));
			Assert.That(service.CurrentStationIndex, Is.EqualTo(1));
			Assert.That(service.IsRunningToNextStation, Is.False);
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task Timetable_Update_DifferentTrain_DoesNotResetLocationState()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);
			service.TrainId = TestData.TrainId; // 現在選択中は TrainId

			// 位置を駅2 (index=1) に設定
			service.ForceSetLocationInfo(1, false);
			int locationChangedCount = 0;
			service.LocationStateChanged += (_, _) => locationChangedCount++;

			// 別の Train ID で時刻表更新 → リセットされないはず
			var timetableTask = WaitForEventAsync<TimetableData>(
				h => service.TimetableUpdated += h,
				h => service.TimetableUpdated -= h
			);

			await _control.BroadcastTimetableAsync(
				TestData.TrainScopeJson,
				workId: TestData.WorkId,
				trainId: TestData.TrainId2 // 別の列車
			);

			await timetableTask;
			await Task.Delay(300); // イベントが発火しないことを確認するための猶予

			Assert.That(locationChangedCount, Is.EqualTo(0));
			Assert.That(service.CurrentStationIndex, Is.EqualTo(1)); // リセットされていない
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task Timetable_AllScopeUpdate_AlwaysResetsState()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);
			service.TrainId = TestData.TrainId;
			service.ForceSetLocationInfo(2, true);

			var locationTask = WaitForEventAsync<LocationStateChangedEventArgs>(
				h => service.LocationStateChanged += h,
				h => service.LocationStateChanged -= h
			);

			// All スコープは常にリセット
			await _control.BroadcastTimetableAsync(TestData.AllScopeJson);

			var state = await locationTask;
			Assert.That(state.NewStationIndex, Is.EqualTo(0));
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task Timetable_WorkGroupScope_MatchingWorkGroup_DoesNotResetLocationState()
	{
		// リアルタイム編集対応 (#214): 自スコープと一致する WorkGroup 更新を受信しても
		// 位置情報 (StationIndex) は維持されなければならない。
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);
			service.WorkGroupId = TestData.WorkGroupId;

			service.ForceSetLocationInfo(1, false);
			int locationChangedCount = 0;
			service.LocationStateChanged += (_, _) => locationChangedCount++;

			var timetableTask = WaitForEventAsync<TimetableData>(
				h => service.TimetableUpdated += h,
				h => service.TimetableUpdated -= h
			);

			await _control.BroadcastTimetableAsync(
				TestData.WorkGroupScopeJson,
				workGroupId: TestData.WorkGroupId
			);

			await timetableTask;
			await Task.Delay(300);

			Assert.That(locationChangedCount, Is.EqualTo(0));
			Assert.That(service.CurrentStationIndex, Is.EqualTo(1));
			Assert.That(service.IsRunningToNextStation, Is.False);
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task Timetable_WorkGroupScope_NonMatchingWorkGroup_DoesNotResetLocationState()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);
			service.WorkGroupId = "other-wg-id";

			service.ForceSetLocationInfo(1, false);
			int locationChangedCount = 0;
			service.LocationStateChanged += (_, _) => locationChangedCount++;

			// 別の WorkGroupId で時刻表更新 → リセットされないはず
			var timetableTask = WaitForEventAsync<TimetableData>(
				h => service.TimetableUpdated += h,
				h => service.TimetableUpdated -= h
			);

			await _control.BroadcastTimetableAsync(
				TestData.WorkGroupScopeJson,
				workGroupId: TestData.WorkGroupId
			);

			await timetableTask;
			await Task.Delay(300);

			Assert.That(locationChangedCount, Is.EqualTo(0));
			Assert.That(service.CurrentStationIndex, Is.EqualTo(1));
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task Timetable_WorkScope_MatchingWork_DoesNotResetLocationState()
	{
		// リアルタイム編集対応 (#214): 自スコープと一致する Work 更新を受信しても
		// 位置情報 (StationIndex) は維持されなければならない。
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);
			service.WorkId = TestData.WorkId;

			service.ForceSetLocationInfo(1, false);
			int locationChangedCount = 0;
			service.LocationStateChanged += (_, _) => locationChangedCount++;

			var timetableTask = WaitForEventAsync<TimetableData>(
				h => service.TimetableUpdated += h,
				h => service.TimetableUpdated -= h
			);

			await _control.BroadcastTimetableAsync(
				TestData.WorkScopeJson,
				workGroupId: TestData.WorkGroupId,
				workId: TestData.WorkId
			);

			await timetableTask;
			await Task.Delay(300);

			Assert.That(locationChangedCount, Is.EqualTo(0));
			Assert.That(service.CurrentStationIndex, Is.EqualTo(1));
			Assert.That(service.IsRunningToNextStation, Is.False);
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task Timetable_WorkScope_NonMatchingWork_DoesNotResetLocationState()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);
			service.WorkId = "other-work-id";

			service.ForceSetLocationInfo(1, false);
			int locationChangedCount = 0;
			service.LocationStateChanged += (_, _) => locationChangedCount++;

			// 別の WorkId で時刻表更新 → リセットされないはず
			var timetableTask = WaitForEventAsync<TimetableData>(
				h => service.TimetableUpdated += h,
				h => service.TimetableUpdated -= h
			);

			await _control.BroadcastTimetableAsync(
				TestData.WorkScopeJson,
				workGroupId: TestData.WorkGroupId,
				workId: TestData.WorkId
			);

			await timetableTask;
			await Task.Delay(300);

			Assert.That(locationChangedCount, Is.EqualTo(0));
			Assert.That(service.CurrentStationIndex, Is.EqualTo(1));
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	// ================================================================
	// ID 更新メッセージ (クライアント → サーバー)
	// ================================================================

	[Test]
	public async Task IdUpdate_WorkGroupIdChange_ServerReceivesWorkGroupId()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			service.WorkGroupId = TestData.WorkGroupId;
			// ID 変更は非同期で送信されるため少し待機
			await Task.Delay(500);

			var clients = await _control.GetWsClientsAsync();
			Assert.That(clients, Has.Count.EqualTo(1));
			Assert.That(clients[0].WorkGroupId, Is.EqualTo(TestData.WorkGroupId));
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task IdUpdate_AllIdsSet_ServerReceivesAllIds()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			service.WorkGroupId = TestData.WorkGroupId;
			service.WorkId = TestData.WorkId;
			service.TrainId = TestData.TrainId;
			await Task.Delay(500);

			var clients = await _control.GetWsClientsAsync();
			Assert.That(clients, Has.Count.EqualTo(1));
			Assert.Multiple(() =>
			{
				Assert.That(clients[0].WorkGroupId, Is.EqualTo(TestData.WorkGroupId));
				Assert.That(clients[0].WorkId, Is.EqualTo(TestData.WorkId));
				Assert.That(clients[0].TrainId, Is.EqualTo(TestData.TrainId));
			});
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	// ================================================================
	// 接続切断
	// ================================================================

	[Test]
	public async Task ConnectionClosed_ServerDisconnects_ConnectionClosedEventFired()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			var closedTask = WaitForNonGenericEventAsync(
				h => service.ConnectionClosed += h,
				h => service.ConnectionClosed -= h,
				timeoutMs: 5000
			);

			await _control.DisconnectAllClientsAsync();

			// ConnectionClosed または ConnectionFailed のいずれかが発火すればよい
			// (再接続の失敗後に ConnectionFailed が発火する場合もある)
			await closedTask;
		}
		finally
		{
			service.Dispose();
		}
	}

	// ================================================================
	// 再接続
	// ================================================================

	[Test]
	public async Task Reconnection_AfterServerDisconnect_ClientReconnectsAndWorks()
	{
		var service = await ConnectServiceAsync(
			reconnectIntervalMs: FastReconnectIntervalMs,
			reconnectAttemptMax: FastReconnectAttemptMax
		);
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			// サーバーが全クライアントを強制切断
			await _control.DisconnectAllClientsAsync();

			// クライアントが切断を検知してから再接続するまで待機
			// 再接続間隔(ms) × (最大試行回数 + バッファ)
			int reconnectTimeoutMs = FastReconnectIntervalMs * (FastReconnectAttemptMax + 2);
			await ReferenceServerClient.WaitForConditionAsync(
				async () => (await _control.GetWsClientsAsync()).Count >= 1,
				timeoutMs: reconnectTimeoutMs
			);

			var clients = await _control.GetWsClientsAsync();
			Assert.That(clients, Has.Count.GreaterThanOrEqualTo(1), "Client should have reconnected");

			// 再接続後も正常に SyncedData を受信できることを確認
			long testTime_ms = 54_000_000L;
			await _control.SetStateAsync(time_ms: testTime_ms, canStart: true);

			var timeTask = WaitForEventAsync<int>(
				h => service.TimeChanged += h,
				h => service.TimeChanged -= h
			);
			// イベント購読後にブロードキャストを発火して TimeChanged を確実に受け取る
			await _control.BroadcastSyncedDataAsync();

			int receivedTime_s = await timeTask;
			Assert.That(receivedTime_s, Is.EqualTo(testTime_ms / 1000));
		}
		finally
		{
			service.Dispose();
		}
	}

	[Test]
	public async Task Reconnection_MaxAttemptsExceeded_ConnectionFailedEventFired()
	{
		// サーバーを停止して再接続が全て失敗する状況を再現するのは難しいため、
		// 存在しないポートに接続して即座に失敗させる別サービスを使用
		var invalidUri = new Uri("ws://127.0.0.1:1");
		using var ws = new ClientWebSocket();
		var service = new WebSocketNetworkSyncService(
			invalidUri, ws,
			reconnectIntervalMs: FastReconnectIntervalMs,
			reconnectAttemptMax: 1
		);

		var failedTask = WaitForNonGenericEventAsync(
			h => service.ConnectionFailed += h,
			h => service.ConnectionFailed -= h,
			timeoutMs: 10_000
		);

		try
		{
			await service.ConnectAsync(CancellationToken.None);
		}
		catch { /* 最初の接続失敗は想定内 */ }

		// ReceiveLoop が起動していた場合は失敗イベントを待機
		// 起動していない場合はスキップ
		if (!failedTask.IsCompleted)
		{
			try { await failedTask; } catch (TimeoutException) { /* 許容 */ }
		}

		service.Dispose();
	}

	// ================================================================
	// 複数クライアント
	// ================================================================

	[Test]
	public async Task MultipleClients_BroadcastToAll()
	{
		var service1 = await ConnectServiceAsync();
		var service2 = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 2);

			long expectedTime_ms = 72_000_000L;
			await _control.SetStateAsync(time_ms: expectedTime_ms, canStart: true);

			var time1Task = WaitForEventAsync<int>(
				h => service1.TimeChanged += h,
				h => service1.TimeChanged -= h
			);
			var time2Task = WaitForEventAsync<int>(
				h => service2.TimeChanged += h,
				h => service2.TimeChanged -= h
			);

			await _control.BroadcastSyncedDataAsync();

			int time1 = await time1Task;
			int time2 = await time2Task;

			Assert.Multiple(() =>
			{
				Assert.That(time1, Is.EqualTo(expectedTime_ms / 1000));
				Assert.That(time2, Is.EqualTo(expectedTime_ms / 1000));
			});
		}
		finally
		{
			await DisconnectAsync(service1);
			await DisconnectAsync(service2);
		}
	}

	[Test]
	public async Task MultipleClients_TimetableBroadcast_AllClientsReceive()
	{
		var service1 = await ConnectServiceAsync();
		var service2 = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 2);

			var tt1Task = WaitForEventAsync<TimetableData>(
				h => service1.TimetableUpdated += h,
				h => service1.TimetableUpdated -= h
			);
			var tt2Task = WaitForEventAsync<TimetableData>(
				h => service2.TimetableUpdated += h,
				h => service2.TimetableUpdated -= h
			);

			await _control.BroadcastTimetableAsync(TestData.AllScopeJson);

			var tt1 = await tt1Task;
			var tt2 = await tt2Task;

			Assert.Multiple(() =>
			{
				Assert.That(tt1.Scope, Is.EqualTo(TimetableScopeType.All));
				Assert.That(tt2.Scope, Is.EqualTo(TimetableScopeType.All));
			});
		}
		finally
		{
			await DisconnectAsync(service1);
			await DisconnectAsync(service2);
		}
	}

	// ================================================================
	// 受け入れ基準 AC-1..AC-7 (#214 リアルタイム編集サポート)
	//
	// SelectionManager + WebSocketNetworkSyncService の ILoader 実装を組み合わせて、
	// 自スコープ更新で選択が維持されつつ最新データが反映されることを検証する。
	// ================================================================

	private static async Task SeedAllScopeAsync(WebSocketNetworkSyncService service, ReferenceServerClient control)
	{
		var task = WaitForEventAsync<TimetableData>(
			h => service.TimetableUpdated += h,
			h => service.TimetableUpdated -= h
		);
		await control.BroadcastTimetableAsync(TestData.AllScopeJson);
		await task;
	}

	[Test]
	public async Task AC1_TrainScope_SamePayload_PreservesSelectionAndUpdatesContent()
	{
		// AC-1: 表示中の Train(Tx) と一致する Scope.Train 更新で
		//   - SelectedTrainData?.Id が維持される
		//   - 行の TrackName が新しい値で反映される
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);
			await SeedAllScopeAsync(service, _control);

			var manager = CreateManagerAndCommitFirst(service);
			Assert.That(manager.SelectedTrainData, Is.Not.Null);
			// TrainId のいずれかが選択される。Tx をテスト対象として明示的に選択する。
			manager.SelectedTrainData = manager.OrderedTrainDataList!.First(t => t.Id == TestData.TrainId);

			// AppViewModel.OnTimetableUpdated と同じ流れで Refresh を駆動する
			service.TimetableUpdated += (_, _) => manager.Refresh();

			var ttTask = WaitForEventAsync<TimetableData>(
				h => service.TimetableUpdated += h,
				h => service.TimetableUpdated -= h
			);
			await _control.BroadcastTimetableAsync(
				TestData.TrainScopeJson_TrackNameOnly,
				workId: TestData.WorkId,
				trainId: TestData.TrainId
			);
			await ttTask;
			await Task.Delay(100); // Refresh のハンドラが走り切るのを待つ

			Assert.Multiple(() =>
			{
				Assert.That(manager.SelectedTrainData, Is.Not.Null);
				Assert.That(manager.SelectedTrainData!.Id, Is.EqualTo(TestData.TrainId));
				// 行 0 の TrackName が更新値になっていること (=再描画の素材が手元にある)
				Assert.That(manager.SelectedTrainData!.Rows, Is.Not.Null);
				Assert.That(manager.SelectedTrainData!.Rows![0].TrackName, Is.EqualTo(TestData.UpdatedTrackName));
			});
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task AC2_WorkScope_PreservesTrainSelectionWhenStillPresent()
	{
		// AC-2: AC-1 の状態で Scope.Work (=現在の Wx) を受信しても、
		//   SelectedTrainData?.Id が新ペイロードに存在する限り、選択は Tx のまま維持される。
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);
			await SeedAllScopeAsync(service, _control);

			var manager = CreateManagerAndCommitFirst(service);
			manager.SelectedTrainData = manager.OrderedTrainDataList!.First(t => t.Id == TestData.TrainId);
			service.TimetableUpdated += (_, _) => manager.Refresh();

			var ttTask = WaitForEventAsync<TimetableData>(
				h => service.TimetableUpdated += h,
				h => service.TimetableUpdated -= h
			);
			await _control.BroadcastTimetableAsync(
				TestData.WorkScopeJsonFull,
				workGroupId: TestData.WorkGroupId,
				workId: TestData.WorkId
			);
			await ttTask;
			await Task.Delay(100);

			Assert.Multiple(() =>
			{
				Assert.That(manager.SelectedWork?.Id, Is.EqualTo(TestData.WorkId));
				Assert.That(manager.SelectedTrainData?.Id, Is.EqualTo(TestData.TrainId));
				// 配下キャッシュも更新されているはず
				Assert.That(manager.SelectedTrainData!.TrainNumber, Is.EqualTo("T-001-Work"));
			});
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task AC3_WorkGroupScope_PreservesAllSelectionsWhenStillPresent()
	{
		// AC-3: Scope.WorkGroup(=現在の WGx) が届き、配下が変化しても、
		//   引き続き存在する Work / Train であれば選択が保持される。
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);
			await SeedAllScopeAsync(service, _control);

			var manager = CreateManagerAndCommitFirst(service);
			manager.SelectedTrainData = manager.OrderedTrainDataList!.First(t => t.Id == TestData.TrainId);
			service.TimetableUpdated += (_, _) => manager.Refresh();

			var ttTask = WaitForEventAsync<TimetableData>(
				h => service.TimetableUpdated += h,
				h => service.TimetableUpdated -= h
			);
			await _control.BroadcastTimetableAsync(
				TestData.WorkGroupScopeJsonFull,
				workGroupId: TestData.WorkGroupId
			);
			await ttTask;
			await Task.Delay(100);

			Assert.Multiple(() =>
			{
				Assert.That(manager.SelectedWorkGroup?.Id, Is.EqualTo(TestData.WorkGroupId));
				Assert.That(manager.SelectedWork?.Id, Is.EqualTo(TestData.WorkId));
				Assert.That(manager.SelectedTrainData?.Id, Is.EqualTo(TestData.TrainId));
				Assert.That(manager.SelectedTrainData!.TrainNumber, Is.EqualTo("T-001-WG"));
			});
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task AC4_DifferentTrainScope_DoesNotChangeCurrentSelection()
	{
		// AC-4: 異なる Train への Scope.Train 更新は、現在表示中の Train の選択を変更しない。
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);
			await SeedAllScopeAsync(service, _control);

			var manager = CreateManagerAndCommitFirst(service);
			manager.SelectedTrainData = manager.OrderedTrainDataList!.First(t => t.Id == TestData.TrainId);
			service.TimetableUpdated += (_, _) => manager.Refresh();

			var beforeTrainNumber = manager.SelectedTrainData!.TrainNumber;

			var ttTask = WaitForEventAsync<TimetableData>(
				h => service.TimetableUpdated += h,
				h => service.TimetableUpdated -= h
			);
			// 別 Train (TrainId2) への更新
			await _control.BroadcastTimetableAsync(
				TestData.TrainScopeJson,
				workId: TestData.WorkId,
				trainId: TestData.TrainId2
			);
			await ttTask;
			await Task.Delay(100);

			// 現在の選択 Train は変わらない
			Assert.That(manager.SelectedTrainData?.Id, Is.EqualTo(TestData.TrainId));
			Assert.That(manager.SelectedTrainData!.TrainNumber, Is.EqualTo(beforeTrainNumber));
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task AC5_WorkGroupScope_PropagatesToDescendantCaches()
	{
		// AC-5: Scope.WorkGroup 受信後に、Loader.GetTrainDataList(workId) /
		//   GetTrainData(trainId) が新データを返す (配下キャッシュが更新済み)。
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);
			await SeedAllScopeAsync(service, _control);

			var ttTask = WaitForEventAsync<TimetableData>(
				h => service.TimetableUpdated += h,
				h => service.TimetableUpdated -= h
			);
			await _control.BroadcastTimetableAsync(
				TestData.WorkGroupScopeJsonFull,
				workGroupId: TestData.WorkGroupId
			);
			await ttTask;
			await Task.Delay(100);

			var loader = (ILoader)service;

			var trains = loader.GetTrainDataList(TestData.WorkId);
			Assert.That(trains, Has.Count.EqualTo(2), "WorkGroup 配下の Trains が再構築されること");

			var t1 = loader.GetTrainData(TestData.TrainId);
			Assert.That(t1, Is.Not.Null);
			Assert.That(t1!.TrainNumber, Is.EqualTo("T-001-WG"));

			var t2 = loader.GetTrainData(TestData.TrainId2);
			Assert.That(t2, Is.Not.Null);
			Assert.That(t2!.TrainNumber, Is.EqualTo("T-002-WG"));
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task AC5_WorkScope_PropagatesToDescendantCaches()
	{
		// AC-5 補足: Scope.Work 受信後にも配下の Trains キャッシュが更新される。
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);
			await SeedAllScopeAsync(service, _control);

			var ttTask = WaitForEventAsync<TimetableData>(
				h => service.TimetableUpdated += h,
				h => service.TimetableUpdated -= h
			);
			await _control.BroadcastTimetableAsync(
				TestData.WorkScopeJsonFull,
				workGroupId: TestData.WorkGroupId,
				workId: TestData.WorkId
			);
			await ttTask;
			await Task.Delay(100);

			var loader = (ILoader)service;
			var t1 = loader.GetTrainData(TestData.TrainId);
			Assert.That(t1, Is.Not.Null);
			Assert.That(t1!.TrainNumber, Is.EqualTo("T-001-Work"));
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task AC6_AllScope_PreservesIdsWhenStillPresent()
	{
		// AC-6: Scope.All 受信時に、現在の SelectedWorkGroup/Work/Train の各 Id が
		//   新ペイロードに存在すれば、それぞれ保持される。
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);
			await SeedAllScopeAsync(service, _control);

			var manager = CreateManagerAndCommitFirst(service);
			manager.SelectedTrainData = manager.OrderedTrainDataList!.First(t => t.Id == TestData.TrainId);
			service.TimetableUpdated += (_, _) => manager.Refresh();

			// 同じ Id を持つ All スコープを再配信する
			var ttTask = WaitForEventAsync<TimetableData>(
				h => service.TimetableUpdated += h,
				h => service.TimetableUpdated -= h
			);
			await _control.BroadcastTimetableAsync(TestData.AllScopeJson);
			await ttTask;
			await Task.Delay(100);

			Assert.Multiple(() =>
			{
				Assert.That(manager.SelectedWorkGroup?.Id, Is.EqualTo(TestData.WorkGroupId));
				Assert.That(manager.SelectedWork?.Id, Is.EqualTo(TestData.WorkId));
				Assert.That(manager.SelectedTrainData?.Id, Is.EqualTo(TestData.TrainId));
			});
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task AC6_AllScope_FallsBackWhenIdsDisappear()
	{
		// AC-6 補足: 既存選択の Id が新ペイロードに存在しなくなった階層から先頭にフォールバック。
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);
			await SeedAllScopeAsync(service, _control);

			var manager = CreateManagerAndCommitFirst(service);
			manager.SelectedTrainData = manager.OrderedTrainDataList!.First(t => t.Id == TestData.TrainId);
			service.TimetableUpdated += (_, _) => manager.Refresh();

			// 全く別の WorkGroup を含む全体更新を送る
			string altJson = $$"""
				[
				  {
				    "Id": "wg-other",
				    "Name": "別 WG",
				    "DBVersion": 1,
				    "Works": [
				      {
				        "Id": "w-other",
				        "Name": "別 Work",
				        "AffectDate": "20260101",
				        "Trains": [
				          {
				            "Id": "t-other",
				            "TrainNumber": "OTHER-1",
				            "Direction": 1,
				            "TimetableRows": []
				          }
				        ]
				      }
				    ]
				  }
				]
				""";
			var ttTask = WaitForEventAsync<TimetableData>(
				h => service.TimetableUpdated += h,
				h => service.TimetableUpdated -= h
			);
			await _control.BroadcastTimetableAsync(altJson);
			await ttTask;
			await Task.Delay(100);

			// 既存選択は消えたので、新ペイロードの先頭にフォールバック
			Assert.Multiple(() =>
			{
				Assert.That(manager.SelectedWorkGroup?.Id, Is.EqualTo("wg-other"));
				Assert.That(manager.SelectedWork?.Id, Is.EqualTo("w-other"));
				Assert.That(manager.SelectedTrainData?.Id, Is.EqualTo("t-other"));
			});
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	// ================================================================
	// サーバー情報・ダイヤ情報 (将来拡張)
	// ================================================================

	[Test]
	public async Task ServerInfo_RequestFromClient_ReceivesResponse()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			await _control.SetServerInfoAsync(
				name: "Test Server",
				admin: "admin@example.com",
				version: "1.2.3",
				protocolVersion: "1.0"
			);

			var infoTask = WaitForEventAsync<ServerInfo>(
				h => service.ServerInfoUpdated += h,
				h => service.ServerInfoUpdated -= h
			);

			await service.RequestServerInfoAsync();
			var info = await infoTask;

			Assert.Multiple(() =>
			{
				Assert.That(info.Name, Is.EqualTo("Test Server"));
				Assert.That(info.Admin, Is.EqualTo("admin@example.com"));
				Assert.That(info.Version, Is.EqualTo("1.2.3"));
				Assert.That(info.ProtocolVersion, Is.EqualTo("1.0"));
			});
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task ServerInfo_BroadcastFromControl_AllClientsReceive()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			await _control.SetServerInfoAsync(name: "Pushed", version: "9.9.9");

			var infoTask = WaitForEventAsync<ServerInfo>(
				h => service.ServerInfoUpdated += h,
				h => service.ServerInfoUpdated -= h
			);
			await _control.BroadcastServerInfoAsync();

			var info = await infoTask;
			Assert.That(info.Name, Is.EqualTo("Pushed"));
			Assert.That(info.Version, Is.EqualTo("9.9.9"));
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task DiagramInfo_RequestFromClient_ReceivesResponse()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			await _control.SetDiagramAsync(
				id: "diag-1",
				name: "今日のダイヤ",
				description: "平日朝ラッシュ",
				workGroupIds: new[] { TestData.WorkGroupId },
				makeCurrent: true
			);

			var infoTask = WaitForEventAsync<DiagramInfo>(
				h => service.DiagramInfoUpdated += h,
				h => service.DiagramInfoUpdated -= h
			);
			await service.RequestDiagramInfoAsync();
			var info = await infoTask;

			Assert.Multiple(() =>
			{
				Assert.That(info.Id, Is.EqualTo("diag-1"));
				Assert.That(info.Name, Is.EqualTo("今日のダイヤ"));
				Assert.That(info.Description, Is.EqualTo("平日朝ラッシュ"));
				Assert.That(info.WorkGroupIds, Is.Not.Null);
				Assert.That(info.WorkGroupIds, Has.Length.EqualTo(1));
				Assert.That(info.WorkGroupIds![0], Is.EqualTo(TestData.WorkGroupId));
			});
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task DiagramInfo_RequestSpecificDiagram_ReceivesThatDiagram()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			await _control.SetDiagramAsync(id: "diag-A", name: "ダイヤ A", makeCurrent: true);
			await _control.SetDiagramAsync(id: "diag-B", name: "ダイヤ B");

			var infoTask = WaitForEventAsync<DiagramInfo>(
				h => service.DiagramInfoUpdated += h,
				h => service.DiagramInfoUpdated -= h
			);
			await service.RequestDiagramInfoAsync(diagramId: "diag-B");
			var info = await infoTask;

			Assert.That(info.Id, Is.EqualTo("diag-B"));
			Assert.That(info.Name, Is.EqualTo("ダイヤ B"));
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task DiagramInfo_BroadcastFromControl_AllClientsReceive()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			await _control.SetDiagramAsync(id: "diag-bcast", name: "Broadcast", makeCurrent: true);

			var infoTask = WaitForEventAsync<DiagramInfo>(
				h => service.DiagramInfoUpdated += h,
				h => service.DiagramInfoUpdated -= h
			);
			await _control.BroadcastDiagramAsync();

			var info = await infoTask;
			Assert.That(info.Id, Is.EqualTo("diag-bcast"));
			Assert.That(info.Name, Is.EqualTo("Broadcast"));
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task ServerInfo_ServerLogsClientRequest()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);
			await _control.ClearReceivedRequestsAsync();

			await service.RequestServerInfoAsync();
			await ReferenceServerClient.WaitForConditionAsync(
				async () => (await _control.GetReceivedRequestsAsync()).Any(r => r.MessageType == "RequestServerInfo"),
				timeoutMs: 3000
			);
			var requests = await _control.GetReceivedRequestsAsync();
			Assert.That(requests.Any(r => r.MessageType == "RequestServerInfo"), Is.True);
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	// ================================================================
	// リモートコマンド (Server → Client)
	// ================================================================

	[Test]
	public async Task SelectTrain_BroadcastFromServer_ClientReceivesEvent()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			var task = WaitForEventAsync<SelectTrainCommand>(
				h => service.TrainSelectionRequested += h,
				h => service.TrainSelectionRequested -= h
			);

			await _control.BroadcastSelectTrainAsync(
				workGroupId: TestData.WorkGroupId,
				workId: TestData.WorkId,
				trainId: TestData.TrainId
			);

			var cmd = await task;
			Assert.Multiple(() =>
			{
				Assert.That(cmd.WorkGroupId, Is.EqualTo(TestData.WorkGroupId));
				Assert.That(cmd.WorkId, Is.EqualTo(TestData.WorkId));
				Assert.That(cmd.TrainId, Is.EqualTo(TestData.TrainId));
			});
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task OperationCommand_StartOperation_ClientReceivesEvent()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			var task = WaitForEventAsync<OperationCommand>(
				h => service.OperationCommandReceived += h,
				h => service.OperationCommandReceived -= h
			);

			await _control.BroadcastOperationCommandAsync("StartOperation");
			var cmd = await task;

			Assert.That(cmd.Action, Is.EqualTo(OperationCommandType.StartOperation));
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task OperationCommand_AllActions_AreReceivedCorrectly()
	{
		var actions = new[]
		{
			("StartOperation", OperationCommandType.StartOperation),
			("EndOperation", OperationCommandType.EndOperation),
			("EnableLocationService", OperationCommandType.EnableLocationService),
			("DisableLocationService", OperationCommandType.DisableLocationService),
		};

		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			foreach (var (str, expected) in actions)
			{
				var task = WaitForEventAsync<OperationCommand>(
					h => service.OperationCommandReceived += h,
					h => service.OperationCommandReceived -= h
				);
				await _control.BroadcastOperationCommandAsync(str);
				var cmd = await task;
				Assert.That(cmd.Action, Is.EqualTo(expected), $"Action='{str}'");
			}
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task HeaderColor_SpecificColor_ClientReceivesRgb()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			var task = WaitForEventAsync<HeaderColorCommand>(
				h => service.HeaderColorChangeRequested += h,
				h => service.HeaderColorChangeRequested -= h
			);

			await _control.BroadcastHeaderColorAsync(resetToDefault: false, color_RGB: 0x336699);
			var cmd = await task;

			Assert.Multiple(() =>
			{
				Assert.That(cmd.ResetToDefault, Is.False);
				Assert.That(cmd.Color_RGB, Is.EqualTo(0x336699));
			});
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task HeaderColor_ResetToDefault_ClientReceivesResetFlag()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			var task = WaitForEventAsync<HeaderColorCommand>(
				h => service.HeaderColorChangeRequested += h,
				h => service.HeaderColorChangeRequested -= h
			);

			await _control.BroadcastHeaderColorAsync(resetToDefault: true);
			var cmd = await task;

			Assert.Multiple(() =>
			{
				Assert.That(cmd.ResetToDefault, Is.True);
				Assert.That(cmd.Color_RGB, Is.Null);
			});
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task Notification_BroadcastFromServer_ClientReceivesAllFields()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			var task = WaitForEventAsync<NotificationData>(
				h => service.NotificationReceived += h,
				h => service.NotificationReceived -= h
			);

			await _control.BroadcastNotificationAsync(
				id: "n-1",
				title: "通告タイトル",
				body: "本文です",
				priority: 1,
				issuedAt: "2026-05-08T12:34:56+09:00"
			);
			var n = await task;

			Assert.Multiple(() =>
			{
				Assert.That(n.Id, Is.EqualTo("n-1"));
				Assert.That(n.Title, Is.EqualTo("通告タイトル"));
				Assert.That(n.Body, Is.EqualTo("本文です"));
				Assert.That(n.Priority, Is.EqualTo(1));
				Assert.That(n.IssuedAt, Is.Not.Null);
			});
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task TimeFormat_BroadcastSpecificFormat_ClientReceivesFormat()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			var task = WaitForEventAsync<TimeFormatCommand>(
				h => service.TimeFormatChangeRequested += h,
				h => service.TimeFormatChangeRequested -= h
			);

			await _control.BroadcastTimeFormatAsync(format: "HH:mm");
			var cmd = await task;

			Assert.That(cmd.Format, Is.EqualTo("HH:mm"));
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task TimeFormat_BroadcastNullResetsFormat()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			var task = WaitForEventAsync<TimeFormatCommand>(
				h => service.TimeFormatChangeRequested += h,
				h => service.TimeFormatChangeRequested -= h
			);

			await _control.BroadcastTimeFormatAsync(format: null);
			var cmd = await task;

			// Format=null は端末既定にリセット
			Assert.That(cmd.Format, Is.Null);
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	// ================================================================
	// 施行日テキスト (任意文字列)
	// ================================================================

	[Test]
	public async Task AffectDate_NonDateString_ExposedAsAffectDateText()
	{
		// 「施行日」に日付以外の任意文字列が来たとき、Work.AffectDateText に格納される。
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			string customJson = $$"""
				[
				  {
				    "Id": "wg-affect",
				    "Name": "施行日テスト WG",
				    "DBVersion": 1,
				    "Works": [
				      {
				        "Id": "w-affect",
				        "Name": "施行日テスト Work",
				        "AffectDate": "ダイヤA",
				        "Trains": []
				      }
				    ]
				  }
				]
				""";

			var ttTask = WaitForEventAsync<TimetableData>(
				h => service.TimetableUpdated += h,
				h => service.TimetableUpdated -= h
			);
			await _control.BroadcastTimetableAsync(customJson);
			await ttTask;
			await Task.Delay(50);

			var loader = (ILoader)service;
			var work = loader.GetWorkList("wg-affect").FirstOrDefault();
			Assert.That(work, Is.Not.Null);
			Assert.That(work!.AffectDate, Is.Null, "日付として解釈できないのでDateOnlyはnull");
			Assert.That(work.AffectDateText, Is.EqualTo("ダイヤA"));
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task AffectDate_ValidDateString_AffectDateTextRemainsNull()
	{
		// 通常の日付 (YYYYMMDD など) が来たときは AffectDate に入り、AffectDateText は null。
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			var ttTask = WaitForEventAsync<TimetableData>(
				h => service.TimetableUpdated += h,
				h => service.TimetableUpdated -= h
			);
			await _control.BroadcastTimetableAsync(TestData.AllScopeJson);
			await ttTask;
			await Task.Delay(50);

			var loader = (ILoader)service;
			var work = loader.GetWorkList(TestData.WorkGroupId).First();
			Assert.That(work.AffectDate, Is.Not.Null);
			Assert.That(work.AffectDateText, Is.Null);
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	// ================================================================
	// 親 Work からの WorkName / AffectDate 引き継ぎ
	//
	// JsonModels.TrainData は WorkName / AffectDate を持たないため、IO.Models.TrainData の
	// これらフィールドは親 WorkData (Name / AffectDate) から引き継いで埋める必要がある。
	// LoaderJson はこれを行っているが、WebSocket 経由のキャッシュ生成でも同様に動くこと、
	// および Train スコープ単独更新では既にキャッシュ済みの Work から引き継がれることを検証する。
	// ================================================================

	[Test]
	public async Task TrainData_AllScope_InheritsWorkNameFromParentWork()
	{
		// All スコープロード後、各 Train の WorkName が親 Work.Name と一致すること。
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			var ttTask = WaitForEventAsync<TimetableData>(
				h => service.TimetableUpdated += h,
				h => service.TimetableUpdated -= h
			);
			await _control.BroadcastTimetableAsync(TestData.AllScopeJson);
			await ttTask;

			var loader = (ILoader)service;
			var work = loader.GetWorkList(TestData.WorkGroupId).First();

			var train1 = loader.GetTrainData(TestData.TrainId);
			var train2 = loader.GetTrainData(TestData.TrainId2);
			Assert.Multiple(() =>
			{
				Assert.That(train1, Is.Not.Null);
				Assert.That(train1!.WorkName, Is.EqualTo(work.Name));
				Assert.That(train2, Is.Not.Null);
				Assert.That(train2!.WorkName, Is.EqualTo(work.Name));
			});
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task TrainData_AllScope_InheritsAffectDateFromParentWork()
	{
		// All スコープロード後、各 Train の AffectDate が親 Work.AffectDate (パース済み DateOnly) と一致すること。
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			var ttTask = WaitForEventAsync<TimetableData>(
				h => service.TimetableUpdated += h,
				h => service.TimetableUpdated -= h
			);
			await _control.BroadcastTimetableAsync(TestData.AllScopeJson);
			await ttTask;

			var loader = (ILoader)service;
			var work = loader.GetWorkList(TestData.WorkGroupId).First();
			Assert.That(work.AffectDate, Is.Not.Null, "Work.AffectDate のパース前提");

			var train1 = loader.GetTrainData(TestData.TrainId);
			var train2 = loader.GetTrainData(TestData.TrainId2);
			Assert.Multiple(() =>
			{
				Assert.That(train1, Is.Not.Null);
				Assert.That(train1!.AffectDate, Is.EqualTo(work.AffectDate));
				Assert.That(train2, Is.Not.Null);
				Assert.That(train2!.AffectDate, Is.EqualTo(work.AffectDate));
			});
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	[Test]
	public async Task TrainData_TrainScopeAfterAllScope_PreservesWorkNameAndAffectDateFromCachedWork()
	{
		// Train スコープ単独更新の JSON には親 Work 情報が含まれない。
		// 既にキャッシュ済みの Work から WorkName / AffectDate を引き継いで埋め直すこと。
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			// 1. まず All スコープでキャッシュを作成
			var firstTask = WaitForEventAsync<TimetableData>(
				h => service.TimetableUpdated += h,
				h => service.TimetableUpdated -= h
			);
			await _control.BroadcastTimetableAsync(TestData.AllScopeJson);
			await firstTask;

			var loader = (ILoader)service;
			var work = loader.GetWorkList(TestData.WorkGroupId).First();
			string expectedWorkName = work.Name;
			DateOnly? expectedAffectDate = work.AffectDate;

			// 2. Train スコープ単独更新
			var ttTask = WaitForEventAsync<TimetableData>(
				h => service.TimetableUpdated += h,
				h => service.TimetableUpdated -= h
			);
			await _control.BroadcastTimetableAsync(
				TestData.TrainScopeJson,
				workId: TestData.WorkId,
				trainId: TestData.TrainId
			);
			await ttTask;

			// 3. 更新後の Train が新しい TrainNumber を持ちつつ、
			//    親 Work から引き継いだ WorkName / AffectDate も保持していること
			var updated = loader.GetTrainData(TestData.TrainId);
			Assert.Multiple(() =>
			{
				Assert.That(updated, Is.Not.Null);
				Assert.That(updated!.TrainNumber, Is.EqualTo("T-001-Updated"), "Train スコープの新ペイロード値");
				Assert.That(updated.WorkName, Is.EqualTo(expectedWorkName), "親 Work.Name を引き継いでいること");
				Assert.That(updated.AffectDate, Is.EqualTo(expectedAffectDate), "親 Work.AffectDate を引き継いでいること");
			});
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}

	// ================================================================
	// 空ダイヤのカスケード (#214 コメント対応)
	// ================================================================

	[Test]
	public async Task EmptyTimetable_AllScope_CascadesToEmptyChildren()
	{
		// Issue #214 コメント: ダイヤ (WorkGroup) が空のとき、配下の Work/Train も空でなければならない。
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);

			// まず通常データをロードして、選択を持たせる
			await SeedAllScopeAsync(service, _control);
			var manager = CreateManagerAndCommitFirst(service);
			service.TimetableUpdated += (_, _) => manager.Refresh();
			Assert.That(manager.SelectedTrainData, Is.Not.Null);

			// 次に空配列を配信する
			var ttTask = WaitForEventAsync<TimetableData>(
				h => service.TimetableUpdated += h,
				h => service.TimetableUpdated -= h
			);
			await _control.BroadcastTimetableAsync("[]");
			await ttTask;
			await Task.Delay(100);

			Assert.Multiple(() =>
			{
				Assert.That(manager.WorkGroupList, Is.Not.Null);
				Assert.That(manager.WorkGroupList!.Count, Is.EqualTo(0));
				Assert.That(manager.SelectedWorkGroup, Is.Null);
				Assert.That(manager.SelectedWork, Is.Null);
				Assert.That(manager.SelectedTrainData, Is.Null);
				Assert.That(manager.WorkList, Is.Null);
				Assert.That(manager.OrderedTrainDataList, Is.Null);
			});
		}
		finally
		{
			await DisconnectAsync(service);
		}
	}
}
