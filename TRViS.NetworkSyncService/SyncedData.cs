namespace TRViS.NetworkSyncService;

public class SyncedData(
	double Location_m,
	long Time_ms,
	bool CanStart,
	double? Latitude_deg = null,
	double? Longitude_deg = null,
	double? Accuracy_m = null
)
{
	public double Location_m { get; } = Location_m;
	public long Time_ms { get; } = Time_ms;
	public bool CanStart { get; } = CanStart;

	/// <summary>
	/// サーバーから配信された緯度。null の場合は緯度経度の配信なし。
	/// </summary>
	public double? Latitude_deg { get; } = Latitude_deg;

	/// <summary>
	/// サーバーから配信された経度。null の場合は緯度経度の配信なし。
	/// </summary>
	public double? Longitude_deg { get; } = Longitude_deg;

	/// <summary>
	/// サーバーから配信された緯度経度の精度 [m]。緯度経度が無い場合や精度情報が無い場合は null。
	/// </summary>
	public double? Accuracy_m { get; } = Accuracy_m;
}
