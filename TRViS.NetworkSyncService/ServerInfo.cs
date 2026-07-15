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

	/// <summary>
	/// サーバーが対応する拡張機能の一覧 (機能ネゴシエーション)。
	/// 省略 / null は「拡張機能なし」を意味する。既知の機能 ID は
	/// <see cref="ServerFeatureIds"/> を参照。
	/// </summary>
	public string[]? Features { get; set; }

	/// <summary>
	/// サーバーアイコン画像 (ライトモード用)。<c>data:image/png;base64,...</c> のような
	/// data URI、または素の base64 文字列 (この場合 PNG として扱う)。
	/// png / jpg / gif / svg (image/svg+xml) に対応する。
	/// デコード後のバイト列が 16MiB (16 * 1024 * 1024 バイト) 以上になる場合は
	/// 受信側で破棄される。
	/// </summary>
	public string? IconImage { get; set; }

	/// <summary>
	/// サーバーアイコン画像 (ダークモード用、省略可)。省略時はダークモードでも
	/// <see cref="IconImage"/> を使用する。書式・サイズ制限は <see cref="IconImage"/> と同じ。
	/// </summary>
	public string? IconImageDark { get; set; }
}

/// <summary>
/// <see cref="ServerInfo.Features"/> で用いる既知の機能 ID。
/// </summary>
public static class ServerFeatureIds
{
	/// <summary>列番によるサーバー側列車検索 (<c>SearchTrain</c> / <c>RequestTrainTimetable</c>) に対応する。</summary>
	public const string TrainSearch = "TrainSearch";
}
