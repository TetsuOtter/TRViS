namespace TRViS;

public partial class NetworkSyncService
{
	public class SyncedData(
		double Location_m,
		long Time_ms,
		bool CanStart
	) {
		public double Location_m { get; } = Location_m;
		public long Time_ms { get; } = Time_ms;
		public bool CanStart { get; } = CanStart;
	}
}
