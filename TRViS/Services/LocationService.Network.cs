using TRViS.Controls;
using TRViS.NetworkSyncService;

namespace TRViS.Services;

public partial class LocationService
{
	const int EXCEPTION_MAX = 10;
	async Task NetworkSyncServiceTask(NetworkSyncServiceManager networkService, CancellationToken token)
	{
		int exceptionCounter = 0;
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
				exceptionCounter = 0;
			}
			catch (TaskCanceledException exTask)
			{
				if (token.IsCancellationRequested)
				{
					logger.Debug("Cancellation is requested -> break");
					break;
				}

				logger.Debug("Task is canceled -> treat as exception");
				if (exceptionCounter++ <= EXCEPTION_MAX)
				{
					logger.Warn("Exception Counter: {0}", exceptionCounter);
					await Task.Delay(Interval, token);
					continue;
				}

				IsEnabled = false;
				serviceCancellation?.Cancel();
				logger.Warn("Switching to GpsPositioningTask");
				SetLonLatLocationService();
				ExceptionThrown?.Invoke(this, exTask);
				return;
			}
			catch (Exception ex)
			{
				logger.Error(ex, "NetworkSyncServiceTask Loop Failed");

				if (exceptionCounter++ <= EXCEPTION_MAX)
				{
					logger.Warn("Exception Counter: {0}", exceptionCounter);
					await Task.Delay(Interval, token);
					continue;
				}
				IsEnabled = false;
				serviceCancellation?.Cancel();
				logger.Warn("Switching to GpsPositioningTask");
				SetLonLatLocationService();
				ExceptionThrown?.Invoke(this, ex);
				return;
			}
		}
	}
}
