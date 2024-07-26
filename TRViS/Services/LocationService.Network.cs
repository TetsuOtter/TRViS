using TRViS.Controls;

namespace TRViS.Services;

public partial class LocationService
{
	async Task NetworkSyncServiceTask(NetworkSyncService networkService, CancellationToken token)
	{
		while (!token.IsCancellationRequested)
		{
			logger.Trace("NetworkSyncServiceTask Loop");

			if (token.IsCancellationRequested)
			{
				logger.Debug("Cancellation is requested -> break");
				break;
			}

			try
			{
				await Task.WhenAll(
					networkService.TickAsync(token),
					// Network経由の場合は負荷が少ないため、頻度が高くても問題ないはず
					Task.Delay(Interval / 5, token)
				);
			}
			catch (TaskCanceledException)
			{
				logger.Debug("Task is canceled -> break");
				break;
			}
			catch (Exception ex)
			{
				logger.Error(ex, "NetworkSyncServiceTask Loop Failed");
				IsEnabled = false;
				serviceCancellation?.Cancel();
				LogView.Add(LogView.Priority.Error, "NetworkSyncServiceTask Loop Failed:" + ex.ToString());

				object? o = _CurrentService;
				if (ReferenceEquals(o, networkService))
						SetLonLatLocationService();
				if (ExceptionThrown is null)
					throw;
				else
					ExceptionThrown.Invoke(this, ex);
				return;
			}
		}
	}
}
