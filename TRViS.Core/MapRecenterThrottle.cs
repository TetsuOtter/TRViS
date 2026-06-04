namespace TRViS.Core;

/// <summary>
/// 地図を現在地へ追従させる際の <c>MoveToRegion</c> 呼び出しを間引くための判定ロジック。
///
/// <para>
/// iOS の MAUI Maps では GPS 更新ごとに <c>MoveToRegion</c> を呼ぶと、region 変更
/// アニメーション中に MKCircle overlay の renderer 探索とレースし、
/// <c>InvalidOperationException: MKOverlayRenderer not found</c> で異常終了する
/// (GitHub #291)。一定距離・一定時間が経過したときのみ recenter することで
/// overlay churn を抑え、クラッシュ発生頻度を下げる。
/// </para>
///
/// <para>
/// MAUI 非依存の純粋ロジックとして <c>TRViS.Core</c> に置き、単体テスト可能にしている。
/// 地図プラットフォーム実装 (例: <c>MyMap.apple.cs</c>) から利用する。
/// </para>
/// </summary>
public sealed class MapRecenterThrottle
{
	/// <summary>recenter 間隔の既定下限値 (秒)。</summary>
	public const double DefaultMinIntervalSeconds = 2.0;

	/// <summary>recenter する最小移動距離の既定値 (m)。</summary>
	public const double DefaultMinDistanceMeters = 30.0;

	readonly double minInterval_s;
	readonly double minDistance_m;

	bool hasLast;
	double lastLat_deg;
	double lastLon_deg;
	DateTime lastRecenterUtc;

	public MapRecenterThrottle(
		double minIntervalSeconds = DefaultMinIntervalSeconds,
		double minDistanceMeters = DefaultMinDistanceMeters)
	{
		minInterval_s = minIntervalSeconds;
		minDistance_m = minDistanceMeters;
	}

	/// <summary>
	/// 現在地が更新されたときに地図を recenter すべきかを判定する。
	/// 初回、または「前回 recenter から <see cref="minInterval_s"/> 秒以上経過し、かつ
	/// <see cref="minDistance_m"/> m 以上移動した」場合に <c>true</c> を返す。
	/// <c>true</c> を返すときは内部状態 (前回位置・時刻) を更新する。
	/// </summary>
	public bool ShouldRecenter(double latitude_deg, double longitude_deg, DateTime nowUtc)
	{
		if (!hasLast)
		{
			Commit(latitude_deg, longitude_deg, nowUtc);
			return true;
		}

		double elapsed_s = (nowUtc - lastRecenterUtc).TotalSeconds;
		double moved_m = DistanceMeters(lastLat_deg, lastLon_deg, latitude_deg, longitude_deg);
		if (elapsed_s >= minInterval_s && moved_m >= minDistance_m)
		{
			Commit(latitude_deg, longitude_deg, nowUtc);
			return true;
		}

		return false;
	}

	/// <summary>
	/// 状態を初期化し、次回 <see cref="ShouldRecenter"/> 呼び出しで必ず recenter させる。
	/// 位置情報サービスの再開時などに使用する。
	/// </summary>
	public void Reset() => hasLast = false;

	void Commit(double lat_deg, double lon_deg, DateTime nowUtc)
	{
		hasLast = true;
		lastLat_deg = lat_deg;
		lastLon_deg = lon_deg;
		lastRecenterUtc = nowUtc;
	}

	/// <summary>
	/// 2 点間の距離 (m) を haversine 公式で算出する。
	/// </summary>
	public static double DistanceMeters(double lat1_deg, double lon1_deg, double lat2_deg, double lon2_deg)
	{
		const double earthRadius_m = 6371000.0;
		double dLat = DegToRad(lat2_deg - lat1_deg);
		double dLon = DegToRad(lon2_deg - lon1_deg);
		double lat1 = DegToRad(lat1_deg);
		double lat2 = DegToRad(lat2_deg);
		double a = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2))
			+ (Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2));
		double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
		return earthRadius_m * c;
	}

	static double DegToRad(double deg) => deg * Math.PI / 180.0;
}
