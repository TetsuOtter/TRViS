using TRViS.Utils;

namespace TRViS.Services;

/// <summary>
/// LocationService の AlertRequested イベントを購読し、MAUI の UI アラートを表示するアダプター。
/// </summary>
internal class LocationServiceAlertSubscriber : IDisposable
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	private readonly LocationService _locationService;

	public LocationServiceAlertSubscriber(LocationService locationService)
	{
		_locationService = locationService;
		_locationService.AlertRequested += OnAlertRequested;
	}

	private void OnAlertRequested(object? sender, UserAlertRequestedEventArgs e)
	{
		logger.Info("AlertRequested: Title={0}, Message={1}", e.Title, e.Message);
		_ = Util.DisplayAlertAsync(e.Title, e.Message, e.Cancel);
	}

	private bool _disposed;
	public void Dispose()
	{
		if (_disposed)
			return;
		_disposed = true;
		_locationService.AlertRequested -= OnAlertRequested;
	}
}
