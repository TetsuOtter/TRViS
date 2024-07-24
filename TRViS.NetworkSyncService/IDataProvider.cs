using System.Threading;
using System.Threading.Tasks;

namespace TRViS;

public partial class NetworkSyncService
{
	public interface IDataProvider
	{
		Task<SyncedData> GetSyncedDataAsync(CancellationToken token);
	}
}
