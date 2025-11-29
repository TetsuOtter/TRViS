using System;
using System.Threading;
using System.Threading.Tasks;

namespace TRViS.NetworkSyncService.DataProviders;

public interface IDataProvider
{
	string? WorkGroupId { get; set; }
	string? WorkId { get; set; }
	string? TrainId { get; set; }

	Task<SyncedData> GetSyncedDataAsync(CancellationToken token);

	/// <summary>
	/// 時刻表データが更新された時に発火するイベント
	/// </summary>
	event EventHandler<TimetableData>? TimetableUpdated;
}
