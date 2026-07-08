using NUnit.Framework;

using TRViS.NetworkSyncService;
using TRViS.NetworkSyncService.IntegrationTests.Helpers;

namespace TRViS.NetworkSyncService.IntegrationTests;

/// <summary>
/// 列車検索機能 (v1.1) の統合テスト。リファレンスサーバーと実際に WebSocket 通信を行い、
/// 検索の成功 / 0件 / タイムアウト、機能検出 (ServerInfo.Features)、
/// 2段階の時刻表取得を検証する。
/// </summary>
[TestFixture]
public class TrainSearchIntegrationTests
{
	private ReferenceServerClient _control = null!;

	[SetUp]
	public async Task SetUp()
	{
		_control = GlobalServerSetup.Server.ControlClient;
		await _control.ResetAsync();
	}

	// ================================================================
	// ヘルパー
	// ================================================================

	private async Task<WebSocketNetworkSyncService> ConnectServiceAsync()
	{
		var uri = new Uri(GlobalServerSetup.Server.WsBaseUrl);
		return await NetworkSyncServiceUtil.CreateFromWebSocketAsync(
			uri, reconnectIntervalMs: 300, reconnectAttemptMax: 3);
	}

	private static async Task DisconnectAsync(WebSocketNetworkSyncService service)
	{
		await service.DisconnectAsync();
		service.Dispose();
	}

	/// <summary>単純な TrainData JSON (Train スコープの Data) を生成する。</summary>
	private static string TrainDataJson(string trainId, string trainNumber, int direction)
		=> $$"""
			{
			  "Id": "{{trainId}}",
			  "TrainNumber": "{{trainNumber}}",
			  "Direction": {{direction}},
			  "TimetableRows": [
			    { "StationName": "始発駅", "Location_m": 0.0, "OnStationDetectRadius_m": 300.0, "Departure": "09:00:00" },
			    { "StationName": "終着駅", "Location_m": 1000.0, "OnStationDetectRadius_m": 300.0, "Arrive": "10:00:00" }
			  ]
			}
			""";

	// ================================================================
	// 検索
	// ================================================================

	[Test]
	public async Task SearchTrain_SameNumber_ReturnsMultipleCandidates()
	{
		// 既定シードは列番 "1234" に 2 行路 (別列車) を持つ。
		var service = await ConnectServiceAsync();
		try
		{
			var results = await service.SearchTrainAsync("1234");
			Assert.That(results, Has.Count.EqualTo(2));
			Assert.That(results.Select(r => r.WorkId), Is.EquivalentTo(new[] { "w-ref-1", "w-ref-2" }));
			Assert.That(results.All(r => r.TrainNumber == "1234"), Is.True);
			var first = results.First(r => r.TrainId == "t-ref-1234a");
			Assert.Multiple(() =>
			{
				Assert.That(first.WorkName, Is.EqualTo("1行路"));
				Assert.That(first.StartStationName, Is.EqualTo("東京"));
				Assert.That(first.StartTime, Is.EqualTo("09:00"));
				Assert.That(first.EndStationName, Is.EqualTo("大阪"));
				Assert.That(first.EndTime, Is.EqualTo("12:30"));
			});
		}
		finally { await DisconnectAsync(service); }
	}

	[Test]
	public async Task SearchTrain_SingleMatch_ReturnsOne()
	{
		var service = await ConnectServiceAsync();
		try
		{
			var results = await service.SearchTrainAsync("5678");
			Assert.That(results, Has.Count.EqualTo(1));
			Assert.That(results[0].TrainId, Is.EqualTo("t-ref-5678"));
		}
		finally { await DisconnectAsync(service); }
	}

	[Test]
	public async Task SearchTrain_PrefixMatchMode_MatchesByStartsWith()
	{
		// 既定 (Prefix): 列番が検索文字列で始まるものにマッチ。
		var service = await ConnectServiceAsync();
		try
		{
			var results = await service.SearchTrainAsync("12", TrainSearchMatchMode.Prefix);
			Assert.That(results, Has.Count.EqualTo(2));
			Assert.That(results.All(r => r.TrainNumber == "1234"), Is.True);
		}
		finally { await DisconnectAsync(service); }
	}

	[Test]
	public async Task SearchTrain_ContainsMatchMode_MatchesBySubstring()
	{
		// Contains: 前方一致では拾えない中間の部分文字列でもマッチする。
		var service = await ConnectServiceAsync();
		try
		{
			var results = await service.SearchTrainAsync("34", TrainSearchMatchMode.Contains);
			Assert.That(results, Has.Count.EqualTo(2));
			Assert.That(results.All(r => r.TrainNumber == "1234"), Is.True);
		}
		finally { await DisconnectAsync(service); }
	}

	[Test]
	public async Task SearchTrain_ExactMatchMode_RejectsPartialMatch()
	{
		// Exact: 部分一致では拾わない (Prefix/Contains なら "1234" にマッチする "12" でも 0 件)。
		var service = await ConnectServiceAsync();
		try
		{
			var partial = await service.SearchTrainAsync("12", TrainSearchMatchMode.Exact);
			Assert.That(partial, Is.Empty);

			var exact = await service.SearchTrainAsync("1234", TrainSearchMatchMode.Exact);
			Assert.That(exact, Has.Count.EqualTo(2));
		}
		finally { await DisconnectAsync(service); }
	}

	[Test]
	public async Task SearchTrain_NoMatch_ReturnsEmptyNotTimeout()
	{
		var service = await ConnectServiceAsync();
		try
		{
			// 0件は「空応答」であり、タイムアウトではない。
			var results = await service.SearchTrainAsync("0000");
			Assert.That(results, Is.Empty);
		}
		finally { await DisconnectAsync(service); }
	}

	[Test]
	public async Task SearchTrain_ServerDoesNotRespond_TimesOut()
	{
		// TrainSearch 機能を無効化するとサーバーは SearchTrain に応答しない。
		await _control.SetServerInfoAsync(features: Array.Empty<string>());

		var service = await ConnectServiceAsync();
		try
		{
			service.SearchTrainTimeoutMs = 500;  // CI 高速化
			Assert.ThrowsAsync<TimeoutException>(async () => await service.SearchTrainAsync("1234"));
		}
		finally { await DisconnectAsync(service); }
	}

	// ================================================================
	// 機能検出 (ServerInfo.Features)
	// ================================================================

	[Test]
	public async Task FeatureDetection_DefaultServer_SupportsTrainSearch()
	{
		var service = await ConnectServiceAsync();
		try
		{
			// 接続直後に RequestServerInfo が自動送信され、Features が反映される。
			await ReferenceServerClient.WaitForConditionAsync(
				() => Task.FromResult(service.IsFeatureSupported(ServerFeatureIds.TrainSearch)),
				timeoutMs: 5000);
			Assert.That(service.ServerFeatures, Does.Contain(ServerFeatureIds.TrainSearch));
		}
		finally { await DisconnectAsync(service); }
	}

	[Test]
	public async Task FeatureDetection_FeaturesEmpty_NotSupported()
	{
		await _control.SetServerInfoAsync(features: Array.Empty<string>());

		var service = await ConnectServiceAsync();
		try
		{
			// Features 空の ServerInfo が返るまで待つ (RequestServerInfo の応答受信を確認)。
			await ReferenceServerClient.WaitForConditionAsync(
				async () =>
				{
					var reqs = await _control.GetReceivedRequestsAsync();
					return reqs.Any(r => r.MessageType == "RequestServerInfo");
				},
				timeoutMs: 5000);
			// 応答適用の猶予
			await ReferenceServerClient.WaitForConditionAsync(
				() => Task.FromResult(!service.IsFeatureSupported(ServerFeatureIds.TrainSearch)),
				timeoutMs: 2000);
			Assert.That(service.IsFeatureSupported(ServerFeatureIds.TrainSearch), Is.False);
		}
		finally { await DisconnectAsync(service); }
	}

	// ================================================================
	// 2段階目: 時刻表取得
	// ================================================================

	[Test]
	public async Task FetchTimetable_ReturnsTrainDataAndCaches()
	{
		var service = await ConnectServiceAsync();
		try
		{
			var results = await service.SearchTrainAsync("5678");
			Assert.That(results, Has.Count.EqualTo(1));

			var train = await service.FetchSearchedTrainTimetableAsync(results[0]);
			Assert.That(train, Is.Not.Null);
			Assert.That(train!.Id, Is.EqualTo("t-ref-5678"));
			Assert.That(train.TrainNumber, Is.EqualTo("5678"));
			Assert.That(train.Rows, Is.Not.Null.And.Length.GreaterThan(0));

			// ILoader キャッシュにも入っている。
			Assert.That(service.GetTrainData("t-ref-5678"), Is.Not.Null);
		}
		finally { await DisconnectAsync(service); }
	}

	[Test]
	public async Task FetchTimetable_UnknownTrain_TimesOut()
	{
		var service = await ConnectServiceAsync();
		try
		{
			service.FetchTrainTimetableTimeoutMs = 500;
			// データセットに無い TrainId → サーバーは時刻表を返さない。
			var bogus = new TrainSearchResult(
				"wg-x", "w-x", "t-does-not-exist", "9999", null, null, null, null, null, null);
			Assert.ThrowsAsync<TimeoutException>(
				async () => await service.FetchSearchedTrainTimetableAsync(bogus));
		}
		finally { await DisconnectAsync(service); }
	}

	// ================================================================
	// 任意データセット (Control API で差し替え)
	// ================================================================

	[Test]
	public async Task SearchTrain_CustomDataset_IsSearchable()
	{
		await _control.SetSearchTrainsAsync(new[]
		{
			new SearchTrainSeed(
				WorkGroupId: "wg-c", WorkId: "w-c", TrainId: "t-c-9001",
				TrainNumber: "9001", DataJson: TrainDataJson("t-c-9001", "9001", 1),
				WorkName: "カスタム行路", Direction: 1,
				StartStationName: "A駅", StartTime: "08:00",
				EndStationName: "B駅", EndTime: "08:30"),
		});

		var service = await ConnectServiceAsync();
		try
		{
			var results = await service.SearchTrainAsync("9001");
			Assert.That(results, Has.Count.EqualTo(1));
			Assert.That(results[0].WorkName, Is.EqualTo("カスタム行路"));

			var train = await service.FetchSearchedTrainTimetableAsync(results[0]);
			Assert.That(train?.Id, Is.EqualTo("t-c-9001"));

			// 既定シードは差し替えられているので "1234" はヒットしない。
			Assert.That(await service.SearchTrainAsync("1234"), Is.Empty);
		}
		finally { await DisconnectAsync(service); }
	}
}
