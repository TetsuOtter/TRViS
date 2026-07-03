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
		SyncTargetIds();
	}

	private void OnAppViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(AppViewModel.SelectedWorkGroup):
			case nameof(AppViewModel.SelectedWork):
			case nameof(AppViewModel.SelectedTrainData):
			// 検索列車の表示切替時も、サーバーへ通知する対象 ID を更新する
			// (検索列車の SyncedData がサーバーから配信されるように)。
			case nameof(AppViewModel.IsDisplayingSearchedTrain):
				logger.Debug("AppViewModel.{0} changed -> SetTargetIds", e.PropertyName);
				SyncTargetIds();
				break;
		}
	}

	private void SyncTargetIds()
	{
		// 検索表示中は検索列車の WG/W/T、それ以外は所定の選択を通知する。
		if (_appViewModel.IsDisplayingSearchedTrain)
		{
			_locationService.SetTargetIds(
				_appViewModel.SearchedWorkGroupId,
				_appViewModel.SearchedWorkId,
				_appViewModel.SearchedTrainId
			);
		}
		else
		{
			_locationService.SetTargetIds(
				_appViewModel.SelectedWorkGroup?.Id,
				_appViewModel.SelectedWork?.Id,
				_appViewModel.SelectedTrainData?.Id
			);
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
