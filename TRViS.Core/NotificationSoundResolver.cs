namespace TRViS.Core;

/// <summary>
/// 通告に紐づく音声 1 件を表す。<see cref="DataBase64"/> は音声バイナリの Base64
/// エンコード文字列 (data URI プレフィックスを含んでいてもよい)、<see cref="Format"/> は
/// その形式 ("wav"/"mp3"、省略時 null)。このクラスはデータの内容を解釈せず、
/// 単に「音が指定されているかどうか」を表すだけに留める (デコード・サイズ検証・実際の
/// 再生は呼び出し側の責務)。
/// </summary>
public sealed record SoundRef(string DataBase64, string? Format);

/// <summary>
/// 通告の受信音・接近音について、個別指定と既定値のどちらを再生すべきかを判定する
/// ステートレスな評価器。<see cref="NotificationRedisplayEvaluator"/> と同様、MAUI 非依存の
/// 純粋関数として切り出し、単体テストで解決順序 (個別 → 既定 → 無音) を担保する (#329)。
/// </summary>
public static class NotificationSoundResolver
{
	/// <summary>
	/// <paramref name="individual"/> (通告固有の音声指定) があればそれを、無ければ
	/// <paramref name="defaultSound"/> (サーバーが設定した既定値) を返す。どちらも無ければ
	/// <c>null</c> (無音)。
	/// </summary>
	public static SoundRef? Resolve(SoundRef? individual, SoundRef? defaultSound)
		=> individual ?? defaultSound;
}
