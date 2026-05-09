namespace TRViS.NetworkSyncService;

/// <summary>
/// サーバーから受け取るサーバー情報
/// </summary>
public class ServerInfo
{
	/// <summary>
	/// サーバー名
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// 管理者名 / 連絡先
	/// </summary>
	public string? Admin { get; set; }

	/// <summary>
	/// サーバー実装バージョン
	/// </summary>
	public string? Version { get; set; }

	/// <summary>
	/// サーバーが対応するプロトコルバージョン
	/// </summary>
	public string? ProtocolVersion { get; set; }
}
