using System.Net;
using System.Net.Sockets;

using NUnit.Framework;

using TRViS.ReferenceServer;

namespace TRViS.NetworkSyncService.IntegrationTests.Helpers;

/// <summary>
/// テストセッション全体でリファレンスサーバーのライフサイクルを管理するフィクスチャ。
///
/// 動作モード:
///   - 環境変数 REFERENCE_SERVER_HTTP_URL が設定されている場合:
///       外部サーバー (Docker コンテナ等) に接続する。
///   - 設定されていない場合:
///       ReferenceNetworkSyncServer を in-process で起動する (ローカル開発用)。
///
/// テストコードはどちらのモードでも ControlClient 経由でのみサーバーを操作する。
/// </summary>
public sealed class ServerFixture : IDisposable
{
	private readonly ReferenceNetworkSyncServer? _localServer;

	public string HttpBaseUrl { get; }
	public string WsBaseUrl { get; }
	public ReferenceServerClient ControlClient { get; }

	public ServerFixture()
	{
		string? envHttpUrl = Environment.GetEnvironmentVariable("REFERENCE_SERVER_HTTP_URL");

		if (envHttpUrl is not null)
		{
			// Docker / CI モード: 外部サーバーを使用
			HttpBaseUrl = envHttpUrl.TrimEnd('/');
			string? envWsUrl = Environment.GetEnvironmentVariable("REFERENCE_SERVER_WS_URL");
			WsBaseUrl = (envWsUrl ?? HttpBaseUrl
				.Replace("https://", "wss://", StringComparison.OrdinalIgnoreCase)
				.Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase)).TrimEnd('/');
		}
		else
		{
			// ローカル開発モード: in-process でサーバーを起動
			// TR.SimpleHttpServer は port:0 を渡しても実際のポートを公開しないため、
			// TcpListener で空きポートを事前確保してから使用する。
			ushort port = GetFreePort();
			_localServer = new ReferenceNetworkSyncServer(port);
			_localServer.Start();
			HttpBaseUrl = $"http://localhost:{port}";
			WsBaseUrl = $"ws://localhost:{port}";
		}

		ControlClient = new ReferenceServerClient(HttpBaseUrl);
	}

	public void Dispose()
	{
		ControlClient.Dispose();
		_localServer?.Stop();
		_localServer?.Dispose();
	}

	private static ushort GetFreePort()
	{
		using var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		ushort port = (ushort)((IPEndPoint)listener.LocalEndpoint).Port;
		listener.Stop();
		return port;
	}
}

