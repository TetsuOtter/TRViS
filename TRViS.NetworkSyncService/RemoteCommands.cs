using System;

namespace TRViS.NetworkSyncService;

/// <summary>
/// サーバーから列車選択を指示するコマンド。
/// 受信側は WorkGroupId / WorkId / TrainId に対応する列車を選択する。
/// 任意のフィールドが null の場合、その階層は変更しない (将来拡張用)。
/// </summary>
public class SelectTrainCommand
{
	public string? WorkGroupId { get; set; }
	public string? WorkId { get; set; }
	public string? TrainId { get; set; }
}

/// <summary>
/// 運行操作コマンドの種別。
/// </summary>
public enum OperationCommandType
{
	/// <summary>運行開始 (位置情報サービスを有効にして運行モードに入る)</summary>
	StartOperation,
	/// <summary>運行終了</summary>
	EndOperation,
	/// <summary>位置情報サービスを有効化する</summary>
	EnableLocationService,
	/// <summary>位置情報サービスを無効化する</summary>
	DisableLocationService,
}

/// <summary>
/// サーバーから送られる運行操作コマンド。
/// </summary>
public class OperationCommand
{
	public OperationCommandType Action { get; set; }
}

/// <summary>
/// サーバーから、既に配信済みの通告 (<see cref="NotificationData"/>) の削除を指示するコマンド。
/// 受信側は <see cref="Id"/> に一致する通告を、受領/未受領を問わず破棄する
/// (保持中のポップアップ/バナー表示中であれば、それも閉じる)。
/// </summary>
public class DeleteNotificationCommand
{
	/// <summary>削除対象の <see cref="NotificationData.Id"/>。</summary>
	public string Id { get; set; } = string.Empty;
}

/// <summary>
/// 通告の受信音・接近音の既定値を設定するコマンド。受信するたびに両ロールを
/// フルに置き換える (差分更新ではない)。対象ロールのフィールドが null の場合、
/// そのロールの既定値は「無し (無音)」にリセットされる。セッション中のみ有効な
/// メモリ上の状態で、WebSocket 切断時に破棄される。
/// </summary>
public class DefaultSoundCommand
{
	/// <summary>受信音の既定として使う音声の Base64 エンコードされたバイナリ。</summary>
	public string? ReceivedSoundBase64 { get; set; }

	/// <summary><see cref="ReceivedSoundBase64"/> の形式 ("wav"/"mp3")。</summary>
	public string? ReceivedSoundFormat { get; set; }

	/// <summary>接近音の既定として使う音声の Base64 エンコードされたバイナリ。</summary>
	public string? ApproachSoundBase64 { get; set; }

	/// <summary><see cref="ApproachSoundBase64"/> の形式 ("wav"/"mp3")。</summary>
	public string? ApproachSoundFormat { get; set; }
}

/// <summary>
/// タイトルバー (ヘッダ) の色変更要求。
/// <see cref="ResetToDefault"/> が true のとき、端末の設定値に戻す。
/// false のとき、<see cref="Color_RGB"/> の RGB 値 (0xRRGGBB) を適用する。
/// </summary>
public class HeaderColorCommand
{
	public bool ResetToDefault { get; set; }
	public int? Color_RGB { get; set; }
}

/// <summary>
/// 通告 (任意のお知らせ) を表すデータ。
/// 画面実装は別途行うが、プロトコル/イベントとしては受信できるようにする。
/// </summary>
public class NotificationData
{
	public string? Id { get; set; }
	/// <summary>指令番号。サーバー・現場運用側の管理番号で、表示のみに用いる。</summary>
	public string? OrderNumber { get; set; }
	public string? Title { get; set; }
	/// <summary>
	/// 小型バナー表示用の要約。未指定/空文字のときは <see cref="Title"/> をそのまま使う。
	/// 大型ポップアップでは常に <see cref="Title"/> を表示する (Summary は使わない)。
	/// </summary>
	public string? Summary { get; set; }
	public string? Body { get; set; }
	/// <summary>0=通常, 1=重要 等。サーバ任意。</summary>
	public int Priority { get; set; }
	/// <summary>
	/// 発信日時。オフセット付き ISO 8601 (例 <c>2024-03-01T09:00:00+09:00</c>) を
	/// 受信した場合のみ設定され、表示側は端末の現在の TZ に変換して表示する
	/// (<see cref="DateTimeOffset.LocalDateTime"/>)。オフセット無しの文字列は
	/// <see cref="IssuedAtIsUnspecifiedTimeZone"/> 側を参照。
	/// </summary>
	public DateTimeOffset? IssuedAt { get; set; }
	/// <summary>
	/// <see cref="IssuedAt"/> が「TZ 指定無し」の文字列から得られたものかどうか。
	/// true の場合、表示側は <see cref="IssuedAt"/> の値をそのまま (TZ 変換せず) 表示する。
	/// </summary>
	public bool IssuedAtIsUnspecifiedTimeZone { get; set; }
	/// <summary>
	/// <see cref="IssuedAt"/> が ISO 8601 (日付部分あり) としてパースできなかったときの
	/// 生の入力文字列。表示側はこれを (日時として解釈せず) そのまま表示する。
	/// <see cref="IssuedAt"/> が設定されているときは常に null。
	/// </summary>
	public string? IssuedAtRawText { get; set; }
	/// <summary>受信者。表示のみに用いる。</summary>
	public string? Receiver { get; set; }
	/// <summary>指令者 (発信者)。表示のみに用いる。</summary>
	public string? Sender { get; set; }
	/// <summary>
	/// アイコンとして表示する文字 (1〜2文字程度を想定)。<see cref="IconImageBase64"/> が
	/// 指定されている場合はそちらが優先され、この文字は使用されない。
	/// </summary>
	public string? IconText { get; set; }
	/// <summary><see cref="IconText"/> の背景色 (0xRRGGBB)。未指定時は既定色を使う。</summary>
	public int? IconColor_RGB { get; set; }
	/// <summary>
	/// アイコン画像の Base64 エンコードされたバイナリ (data URI の場合はプレフィックスを含んでいてもよい)。
	/// 指定されている場合、<see cref="IconText"/>/<see cref="IconColor_RGB"/> より優先して表示する。
	/// </summary>
	public string? IconImageBase64 { get; set; }
	/// <summary>
	/// サーバーがこのクライアントについて当該通告を「受領済み」と判断しているか。
	/// true のとき、クライアントは既読扱いとしてポップアップを再表示しない
	/// (再接続時などに一覧として再配信されたケースを想定)。省略/false は未受領。
	/// </summary>
	public bool Acknowledged { get; set; }

	/// <summary>
	/// 初回表示を小型 (画面上部の 1 行バナー) で行うか。true のとき、大型の中央ポップアップ
	/// ではなく小型バナーで表示する。未指定/false は大型表示。小型でも受領ボタンは表示され、
	/// 受領必須の通告 (Id あり) は受領するまで消えない。
	/// </summary>
	public bool CompactDisplay { get; set; }

	/// <summary>
	/// この通告が対象とする区間・駅の開始側。<b>駅名または駅 ID</b> の文字列で指定する
	/// (照合は ID 一致 → 駅名一致の順)。<see cref="SectionEndStation"/> と併せて区間を表す。
	/// <see cref="SectionEndStation"/> が未指定の場合は単駅指定 (この駅のみ) とみなす。
	/// <para>
	/// 受領後、通告は非表示になるが、この区間の
	/// <see cref="StationsBefore"/> 駅手前に到達したタイミングで受領済み状態の小型バナーとして
	/// 自動的に再表示され、区間を抜けると自動的に非表示になる。経路 (現在列車) に該当駅が
	/// 無い場合は再表示しない。
	/// </para>
	/// </summary>
	public string? SectionStartStation { get; set; }

	/// <summary>
	/// この通告が対象とする区間の終了側。<b>駅名または駅 ID</b> の文字列で指定する。
	/// 未指定のとき <see cref="SectionStartStation"/> と同一 (単駅) 扱い。区間の向き
	/// (開始/終了の前後) は問わない (経路上のインデックスで正規化する)。
	/// </summary>
	public string? SectionEndStation { get; set; }

	/// <summary>
	/// 区間開始の何駅手前から再表示を開始するか。既定 1 (1 駅前)。0 以下は 0 として扱い、
	/// 区間開始駅から表示する。
	/// </summary>
	public int StationsBefore { get; set; } = 1;

	/// <summary>
	/// この通告固有の受信音 (初回表示時に再生) の Base64 エンコードされたバイナリ
	/// (data URI プレフィックスを含んでいてもよい)。未指定/null の場合、
	/// <see cref="DefaultSoundCommand"/> で設定された受信音の既定値があればそれを使う。
	/// デコード後 16MiB を超える場合は再生されない (無音)。
	/// </summary>
	public string? ReceivedSoundBase64 { get; set; }

	/// <summary><see cref="ReceivedSoundBase64"/> の形式 ("wav"/"mp3")。</summary>
	public string? ReceivedSoundFormat { get; set; }

	/// <summary>
	/// この通告固有の接近音 (区間連動の再表示バナー表示時に再生) の Base64 エンコードされた
	/// バイナリ。未指定/null の場合、<see cref="DefaultSoundCommand"/> の接近音の既定値が
	/// あればそれを使う。デコード後 16MiB を超える場合は再生されない (無音)。
	/// </summary>
	public string? ApproachSoundBase64 { get; set; }

	/// <summary><see cref="ApproachSoundBase64"/> の形式 ("wav"/"mp3")。</summary>
	public string? ApproachSoundFormat { get; set; }

	/// <summary>
	/// <see cref="IconColor_RGB"/> を文字列表現からパースする。JSON では数値
	/// (0xRRGGBB の 10 進表記) が本来の形式だが、<c>"#RRGGBB"</c> 形式の 16 進文字列も
	/// 受け付ける (先頭の <c>#</c> は省略可)。UI_TEST の deeplink クエリパラメータ経由での
	/// 指定にも使う。
	/// </summary>
	public static bool TryParseIconColor(string? s, out int rgb)
	{
		rgb = 0;
		if (string.IsNullOrEmpty(s))
			return false;

		// "#RRGGBB" 形式 (先頭 '#' があるときのみ 16 進として解釈。無いときは従来通り 10 進整数)。
		if (s.StartsWith('#'))
		{
			string hex = s[1..];
			return hex.Length == 6 && int.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
				System.Globalization.CultureInfo.InvariantCulture, out rgb);
		}

		return int.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out rgb);
	}
}

/// <summary>
/// タイトルバー部分の時刻表示フォーマット指定。
/// 例: "HH:mm:ss" / "HH:mm" / null は端末既定。
/// </summary>
public class TimeFormatCommand
{
	public string? Format { get; set; }
}

/// <summary>
/// 指定の列車の時刻表ビュー (D-TAC 画面の「時刻表」タブ) を開かせるコマンド。
/// <see cref="SelectTrainCommand"/> と同じ階層指定で列車を選択した上で D-TAC へ遷移し、
/// 時刻表タブ (VerticalView) を表示する。
/// 任意のフィールドが null の場合、その階層は変更しない。
/// </summary>
public class OpenTimetableCommand
{
	public string? WorkGroupId { get; set; }
	public string? WorkId { get; set; }
	public string? TrainId { get; set; }
}
