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
}
