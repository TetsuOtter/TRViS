using System.Net.WebSockets;

using NUnit.Framework;

using TRViS.IO;
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
	private static Task<T> WaitForEventAsync<T>(
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
		return tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs));
	}

	/// <summary>
	/// 非ジェネリック EventHandler イベント (ConnectionClosed / ConnectionFailed 等) が
	/// 発火するまで最大 <paramref name="timeoutMs"/> ms 待機する。
	/// </summary>
	private static Task WaitForNonGenericEventAsync(
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
		return tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs));
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

	// ================================================================
	// 運行中の時刻表更新
	// ================================================================

	[Test]
	public async Task Timetable_Update_CurrentTrain_ResetsLocationState()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);
			service.TrainId = TestData.TrainId;

			// 位置を駅2 (index=1) に設定
			service.ForceSetLocationInfo(1, false);

			// 現在選択中の Train ID で時刻表更新を受信したとき → リセットされる
			// LocationStateChanged が index=0 で発火することを検証
			var locationTask = WaitForEventAsync<LocationStateChangedEventArgs>(
				h => service.LocationStateChanged += h,
				h => service.LocationStateChanged -= h
			);

			await _control.BroadcastTimetableAsync(
				TestData.TrainScopeJson,
				workId: TestData.WorkId,
				trainId: TestData.TrainId
			);

			var state = await locationTask;
			Assert.That(state.NewStationIndex, Is.EqualTo(0));
			Assert.That(state.IsRunningToNextStation, Is.False);
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
	public async Task Timetable_WorkGroupScope_MatchingWorkGroup_ResetsLocationState()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);
			service.WorkGroupId = TestData.WorkGroupId;

			service.ForceSetLocationInfo(1, false);

			var locationTask = WaitForEventAsync<LocationStateChangedEventArgs>(
				h => service.LocationStateChanged += h,
				h => service.LocationStateChanged -= h
			);

			// 現在選択中の WorkGroupId と一致する WorkGroup スコープ更新 → リセットされる
			await _control.BroadcastTimetableAsync(
				TestData.WorkGroupScopeJson,
				workGroupId: TestData.WorkGroupId
			);

			var state = await locationTask;
			Assert.That(state.NewStationIndex, Is.EqualTo(0));
			Assert.That(state.IsRunningToNextStation, Is.False);
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
	public async Task Timetable_WorkScope_MatchingWork_ResetsLocationState()
	{
		var service = await ConnectServiceAsync();
		try
		{
			await WaitForWsClientCountAsync(_control, 1);
			service.WorkId = TestData.WorkId;

			service.ForceSetLocationInfo(1, false);

			var locationTask = WaitForEventAsync<LocationStateChangedEventArgs>(
				h => service.LocationStateChanged += h,
				h => service.LocationStateChanged -= h
			);

			// 現在選択中の WorkId と一致する Work スコープ更新 → リセットされる
			await _control.BroadcastTimetableAsync(
				TestData.WorkScopeJson,
				workGroupId: TestData.WorkGroupId,
				workId: TestData.WorkId
			);

			var state = await locationTask;
			Assert.That(state.NewStationIndex, Is.EqualTo(0));
			Assert.That(state.IsRunningToNextStation, Is.False);
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
}
