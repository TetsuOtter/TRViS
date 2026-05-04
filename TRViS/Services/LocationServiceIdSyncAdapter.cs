using System.ComponentModel;

using TRViS.ViewModels;

namespace TRViS.Services;

/// <summary>
/// AppViewModel の WorkGroup/Work/Train 選択変更を LocationService に同期するアダプター。
/// </summary>
internal class LocationServiceIdSyncAdapter : IDisposable
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	private readonly LocationService _locationService;
	private readonly AppViewModel _appViewModel;

	public LocationServiceIdSyncAdapter(LocationService locationService, AppViewModel appViewModel)
	{
		_locationService = locationService;
		_appViewModel = appViewModel;
		_appViewModel.PropertyChanged += OnAppViewModelPropertyChanged;
		_locationService.SetTargetIds(
			_appViewModel.SelectedWorkGroup?.Id,
			_appViewModel.SelectedWork?.Id,
			_appViewModel.SelectedTrainData?.Id
		);
	}

	private void OnAppViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(AppViewModel.SelectedWorkGroup):
			case nameof(AppViewModel.SelectedWork):
			case nameof(AppViewModel.SelectedTrainData):
				logger.Debug("AppViewModel.{0} changed -> SetTargetIds", e.PropertyName);
				_locationService.SetTargetIds(
					_appViewModel.SelectedWorkGroup?.Id,
					_appViewModel.SelectedWork?.Id,
					_appViewModel.SelectedTrainData?.Id
				);
				break;
		}
	}

	private bool _disposed;
	public void Dispose()
	{
		if (_disposed)
			return;
		_disposed = true;
		_appViewModel.PropertyChanged -= OnAppViewModelPropertyChanged;
	}
}
