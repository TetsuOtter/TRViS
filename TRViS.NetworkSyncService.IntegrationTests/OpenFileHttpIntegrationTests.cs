using System.Collections.Specialized;
using System.Net;
using System.Net.Sockets;
using System.Text;

using NUnit.Framework;

using TR.SimpleHttpServer;
using TR.SimpleHttpServer.WebSocket;

using TRViS.IO;
using TRViS.IO.RequestInfo;

namespace TRViS.NetworkSyncService.IntegrationTests;

/// <summary>
/// HTTP 経由の時刻表読み込み (<see cref="OpenFile"/> → <see cref="LoaderJson"/>) の統合テスト。
///
/// 既存の <see cref="HttpIntegrationTests"/> はリアルタイム同期
/// (<see cref="HttpNetworkSyncService"/>) のみを検証しており、
/// 「ローカルサーバーから時刻表 JSON をダウンロードして開く」経路
/// (TRViS.LocalServers との連携) はこれまで一切カバーされていなかった。
///
/// このテストは TRViS.LocalServers と同じ <c>TR.SimpleHttpServer</c> を使い、
/// その HTTP コントラクト (<c>/timetable.json</c> で WorkGroupData[] を返す /
/// シナリオ未ロード時は 204 No Content) を忠実に再現する。
/// </summary>
[TestFixture]
public class OpenFileHttpIntegrationTests
{
	private LocalTimetableServer _server = null!;
	private HttpClient _httpClient = null!;

	[SetUp]
	public void SetUp()
	{
		_server = new LocalTimetableServer(GetFreePort());
		_server.Start();
		_httpClient = new HttpClient();
	}

	[TearDown]
	public void TearDown()
	{
		_httpClient.Dispose();
		_server.Dispose();
	}

	private AppLinkInfo MakeJsonAppLink(string url)
		// ConnectServerDialog が生 URL に対して構築するものと同一の形。
		=> new(
			AppLinkInfo.FileType.Json,
			Version: new(1, 0),
			ResourceUri: new(url)
		);

	// ================================================================
	// 正常系: TRViS.LocalServers の /timetable.json を開く
	// ================================================================

	[Test]
	public async Task OpenAppLink_TimetableJson_LoadsWorkGroups()
	{
		_server.Mode = ResponseMode.HappyPath;
		var appLink = MakeJsonAppLink($"{_server.BaseUrl}/timetable.json");

		// CanContinueWhenHeadRequestSuccess / CanContinueWhenResourceUriContainsIp は
		// どちらも `is not null` ゲートのため、未設定なら UI ダイアログ経路は素通りする。
		var openFile = new OpenFile(_httpClient);

		using ILoader loader = await openFile.OpenAppLinkAsync(appLink, CancellationToken.None);

		var workGroups = loader.GetWorkGroupList();
		Assert.That(workGroups, Has.Count.EqualTo(2));
		Assert.Multiple(() =>
		{
			Assert.That(workGroups[0].Name, Is.EqualTo("WorkGroup01"));
			Assert.That(workGroups[1].Name, Is.EqualTo("WorkGroup02"));
		});

		// HEAD → GET の順でアクセスされていること (OpenFile のプリフライト仕様)。
		Assert.That(_server.ReceivedMethods, Is.EqualTo(new[] { "HEAD", "GET" }));
	}

	[Test]
	public async Task OpenAppLink_TimetableJson_TrainDataReadable()
	{
		_server.Mode = ResponseMode.HappyPath;
		var appLink = MakeJsonAppLink($"{_server.BaseUrl}/timetable.json");
		var openFile = new OpenFile(_httpClient);

		using ILoader loader = await openFile.OpenAppLinkAsync(appLink, CancellationToken.None);

		string wgId = loader.GetWorkGroupList()[0].Id;
		var works = loader.GetWorkList(wgId);
		Assert.That(works, Has.Count.EqualTo(2));
		var trains = loader.GetTrainDataList(works[0].Id);
		Assert.That(trains, Has.Count.EqualTo(2));
		Assert.That(trains[0].TrainNumber, Is.EqualTo("WG01-W01-Train01"));
	}

	// ================================================================
	// 異常系: シナリオ未ロード時の空ボディ
	//
	// TRViS.LocalServers は連携元 (ゲーム等) でシナリオ/列車が未ロードのとき
	// (a) 204 No Content、または (b) 200 + 空ボディ を返す
	// (HttpRequestHandler.cs / GenerateJson)。
	//
	// 改修前: 空ボディが LoaderJson に渡り、生の
	//   JsonException ("The input does not contain any JSON tokens")
	// が漏れて ConnectServerDialog が「読み込みに失敗しました: ...」と表示し、
	// ユーザーには原因 (= サーバー側でシナリオ未ロード) が伝わらなかった。
	//
	// 改修後: OpenFile が両ケースを明示的に弾き、行動可能なメッセージを持つ
	// HttpRequestException に変換する。
	// ================================================================

	[Test]
	public void OpenAppLink_ScenarioNotLoaded_204_ThrowsActionableError()
	{
		_server.Mode = ResponseMode.NoContent204; // シナリオ未ロード → 204
		var appLink = MakeJsonAppLink($"{_server.BaseUrl}/timetable.json");
		var openFile = new OpenFile(_httpClient);

		var ex = Assert.ThrowsAsync<HttpRequestException>(async () =>
		{
			using ILoader _ = await openFile.OpenAppLinkAsync(appLink, CancellationToken.None);
		});
		Assert.That(ex!.Message, Does.Contain("時刻表データがまだありません"));
	}

	[Test]
	public void OpenAppLink_ScenarioLoadedButEmptyBody_200_ThrowsActionableError()
	{
		_server.Mode = ResponseMode.EmptyBody200; // GetWorkGroup()==null → 200 + 空
		var appLink = MakeJsonAppLink($"{_server.BaseUrl}/timetable.json");
		var openFile = new OpenFile(_httpClient);

		var ex = Assert.ThrowsAsync<HttpRequestException>(async () =>
		{
			using ILoader _ = await openFile.OpenAppLinkAsync(appLink, CancellationToken.None);
		});
		Assert.That(ex!.Message, Does.Contain("時刻表データがまだありません"));
	}

	// ================================================================
	// ヘルパー
	// ================================================================

	private static ushort GetFreePort()
	{
		using var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		ushort port = (ushort)((IPEndPoint)listener.LocalEndpoint).Port;
		listener.Stop();
		return port;
	}

	/// <summary>
	/// TRViS.LocalServers の HTTP コントラクトを最小再現したサーバー。
	/// 実装の TR.SimpleHttpServer をそのまま使い、メソッドを問わず
	/// パスのみでルーティングする (本家 HttpRequestHandler と同じ挙動)。
	/// </summary>
	private sealed class LocalTimetableServer : IDisposable
	{
		private static readonly string SampleJsonPath =
			Path.Combine(TestContext.CurrentContext.TestDirectory, "Resources", "db.sample.json");

		private readonly HttpServer _httpServer;
		private readonly List<string> _receivedMethods = new();
		private readonly object _lock = new();

		public ResponseMode Mode { get; set; } = ResponseMode.HappyPath;
		public string BaseUrl { get; }
		public IReadOnlyList<string> ReceivedMethods
		{
			get { lock (_lock) { return _receivedMethods.ToArray(); } }
		}

		public LocalTimetableServer(ushort port)
		{
			_httpServer = new HttpServer(port, HandleAsync, SelectWsHandlerAsync);
			BaseUrl = $"http://localhost:{port}";
		}

		public void Start() => _httpServer.Start();

		public void Dispose()
		{
			_httpServer.Stop();
			_httpServer.Dispose();
		}

		private static Task<WebSocketHandler?> SelectWsHandlerAsync(string path)
			=> Task.FromResult<WebSocketHandler?>(null);

		private Task<HttpResponse> HandleAsync(HttpRequest request)
		{
			lock (_lock) { _receivedMethods.Add(request.Method.ToUpperInvariant()); }

			string path = request.Path.Split('?', '#')[0];
			if (path != "/timetable.json")
				return Task.FromResult(new HttpResponse(
					HttpStatusCode.NotFound, "text/plain", new NameValueCollection(), "Not Found"));

			return Task.FromResult(Mode switch
			{
				// シナリオ未ロード → 204 No Content (本家 HttpRequestHandler)
				ResponseMode.NoContent204 => new HttpResponse(
					HttpStatusCode.NoContent, "text/plain", new NameValueCollection(), ""),
				// シナリオはロード済みだが GetWorkGroup()==null → 200 + 空ボディ
				ResponseMode.EmptyBody200 => new HttpResponse(
					HttpStatusCode.OK, "application/json", new NameValueCollection(), ""),
				_ => new HttpResponse(
					HttpStatusCode.OK, "application/json", new NameValueCollection(),
					File.ReadAllText(SampleJsonPath, Encoding.UTF8)),
			});
		}
	}

	internal enum ResponseMode
	{
		HappyPath,
		NoContent204,
		EmptyBody200,
	}
}
