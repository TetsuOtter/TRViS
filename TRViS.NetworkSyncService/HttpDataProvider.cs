using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TRViS;

public partial class NetworkSyncService
{
	class HttpDataProvider(
		Uri uri,
		HttpClient httpClient
	) : IDataProvider
	{
		private readonly HttpClient _HttpClient = httpClient;
		private readonly Uri _Uri = uri;

		double lastLocation = 0;
		public Task<SyncedData> GetSyncedDataAsync(CancellationToken token)
		{
			lastLocation += 0.1;
			return Task.FromResult(new SyncedData(
				Location_m: lastLocation,
				Time_ms: 0,
				CanStart: false
			));
		}
	}
}
