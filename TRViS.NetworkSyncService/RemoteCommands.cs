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
	public string? Title { get; set; }
	public string? Body { get; set; }
	/// <summary>0=通常, 1=重要 等。サーバ任意。</summary>
	public int Priority { get; set; }
	public DateTimeOffset? IssuedAt { get; set; }
}

/// <summary>
/// タイトルバー部分の時刻表示フォーマット指定。
/// 例: "HH:mm:ss" / "HH:mm" / null は端末既定。
/// </summary>
public class TimeFormatCommand
{
	public string? Format { get; set; }
}
