namespace TRViS.NetworkSyncService;

/// <summary>
/// ダイヤ情報。
/// WorkGroupよりも上の概念として「ダイヤ」を定義し、
/// その識別子・名称や所属するWorkGroupなどを取得する。
/// </summary>
public class DiagramInfo
{
	/// <summary>
	/// ダイヤの識別子
	/// </summary>
	public string? Id { get; set; }

	/// <summary>
	/// ダイヤの名称
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// ダイヤの説明文 / 補足
	/// </summary>
	public string? Description { get; set; }

	/// <summary>
	/// このダイヤに含まれる WorkGroup の ID 一覧。
	/// サーバーが提供する場合のみ設定される。
	/// </summary>
	public string[]? WorkGroupIds { get; set; }

	/// <summary>
	/// 接続情報カードに表示できるダイヤ情報 (名称か説明の少なくとも一方) を持つか。
	/// null、または名称・説明とも空白なら false。LoaderInfoCard の行高を未受信時の
	/// コンパクト表示と受信時の拡張表示で切り替える唯一の判定点。
	/// </summary>
	public static bool HasDisplayableContent(DiagramInfo? info) =>
		!string.IsNullOrWhiteSpace(info?.Name) || !string.IsNullOrWhiteSpace(info?.Description);
}
