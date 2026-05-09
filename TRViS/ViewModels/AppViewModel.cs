using System.Text.Json.Serialization;

using CommunityToolkit.Mvvm.ComponentModel;

using TRViS.Core;
using TRViS.IO;
using TRViS.IO.Models;
using TRViS.NetworkSyncService;
using TRViS.Services;
using TRViS.Utils;

namespace TRViS.ViewModels;

public partial class AppViewModel : ObservableObject
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	public TimetableSelectionManager SelectionManager { get; } = new();

	[ObservableProperty]
	ILoader? _Loader;

	public IReadOnlyList<WorkGroup>? WorkGroupList => SelectionManager.WorkGroupList;
	public IReadOnlyList<Work>? WorkList => SelectionManager.WorkList;
	public IReadOnlyList<TrainData>? OrderedTrainDataList => SelectionManager.OrderedTrainDataList;

	public WorkGroup? SelectedWorkGroup
	{
		get => SelectionManager.SelectedWorkGroup;
		set => SelectionManager.SelectedWorkGroup = value;
	}

	public Work? SelectedWork
	{
		get => SelectionManager.SelectedWork;
		set => SelectionManager.SelectedWork = value;
	}

	public TrainData? SelectedTrainData
	{
		get => SelectionManager.SelectedTrainData;
		set => SelectionManager.SelectedTrainData = value;
	}

	bool _IsBgAppIconVisible = true;
	public bool IsBgAppIconVisible
	{
		get => _IsBgAppIconVisible;
		set
		{
			if (_IsBgAppIconVisible == value)
				return;
			// 不正利用・誤認防止のため、ライトモード時はアイコン背景を強制表示する。
			if (CurrentAppTheme == AppTheme.Light && value == false)
				return;
			SetProperty(ref _IsBgAppIconVisible, value);
		}
	}

	[ObservableProperty]
	double _WindowHeight;

	[ObservableProperty]
	double _WindowWidth;

	public event EventHandler<ValueChangedEventArgs<AppTheme>>? CurrentAppThemeChanged;
	AppTheme _SystemAppTheme;
	public AppTheme SystemAppTheme => _SystemAppTheme;
	AppTheme _CurrentAppTheme;
	public AppTheme CurrentAppTheme
	{
		get => _CurrentAppTheme;
		set
		{
			if (value == AppTheme.Unspecified)
				value = _SystemAppTheme;

			if (_CurrentAppTheme == value)
				return;

			AppTheme tmp = _CurrentAppTheme;
			_CurrentAppTheme = value;
			CurrentAppThemeChanged?.Invoke(this, new(tmp, value));
			// 不正利用・誤認防止のため、ライトモード時はアイコン背景を強制表示する。
			if (value == AppTheme.Light)
				IsBgAppIconVisible = true;
		}
	}

	[JsonSourceGenerationOptions(WriteIndented = false)]
	[JsonSerializable(typeof(List<string>))]
	internal partial class StringListJsonSourceGenerationContext : JsonSerializerContext
	{
	}

	public AppViewModel()
	{
		SelectionManager.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName);

		if (Application.Current is not null)
		{
			_CurrentAppTheme = Application.Current.RequestedTheme;

			// does not fire -> https://github.com/dotnet/maui/pull/11199
			// will be resolved with net8
			Application.Current.RequestedThemeChanged += (s, e) =>
			{
				_SystemAppTheme = e.RequestedTheme;

				if (Application.Current.UserAppTheme == AppTheme.Unspecified)
					CurrentAppTheme = e.RequestedTheme;
			};
		}

		_ExternalResourceUrlHistory = AppPreferenceService.GetFromJson(AppPreferenceKeys.ExternalResourceUrlHistory, [], out _, StringListJsonSourceGenerationContext.Default.ListString);
	}

	internal void SubscribeToLocationService(TRViS.Services.LocationService locationService)
	{
		locationService.TimetableUpdated += OnTimetableUpdated;
		locationService.TrainSelectionRequested += OnTrainSelectionRequested;
		locationService.HeaderColorChangeRequested += OnHeaderColorChangeRequested;
		locationService.TimeFormatChangeRequested += OnTimeFormatChangeRequested;
		// NotificationReceived / OperationCommandReceived / ServerInfo / DiagramInfo は
		// LocationService 側で受信される。OperationCommand の動作 (位置情報 ON/OFF) は
		// LocationService が直接適用する。Notification / ServerInfo / DiagramInfo の UI 表示は
		// 個別画面側で必要に応じて購読する。
	}

	partial void OnLoaderChanged(ILoader? value)
	{
		SelectionManager.Loader = value;
	}

	void OnTimetableUpdated(object? sender, TimetableData timetableData)
	{
		logger.Debug("TimetableUpdated: WorkGroupId={0}, WorkId={1}, TrainId={2}, Scope={3}",
			timetableData.WorkGroupId, timetableData.WorkId, timetableData.TrainId, timetableData.Scope);

		// リアルタイム編集対応: 自スコープと一致する更新では選択を維持し、
		// 異なるスコープの更新では現在の表示は無関係なのでそのまま継続する。
		// SelectionManager.Refresh() が各階層で選択 Id を保持しつつ最新データを反映する。
		// - 既存選択が新ペイロードに存在する → 同じ Id の最新インスタンスに差し替え
		// - 既存選択が消えた階層から先 → 先頭にフォールバック
		// この挙動は Scope.All / WorkGroup / Work / Train すべてのケースをカバーする。
		if (Loader is not null)
		{
			logger.Debug("Refreshing selection from Loader cache");
			SelectionManager.Refresh();
		}
	}

	/// <summary>
	/// サーバーから送られた SelectTrain コマンドを反映する。
	/// WorkGroupId / WorkId / TrainId に対応する階層を選択する。
	/// </summary>
	void OnTrainSelectionRequested(object? sender, SelectTrainCommand cmd)
	{
		logger.Info("OnTrainSelectionRequested: WorkGroupId={0}, WorkId={1}, TrainId={2}",
			cmd.WorkGroupId, cmd.WorkId, cmd.TrainId);

		if (cmd.WorkGroupId is not null)
		{
			var wg = SelectionManager.WorkGroupList?.FirstOrDefault(w => w.Id == cmd.WorkGroupId);
			if (wg is not null && SelectionManager.SelectedWorkGroup?.Id != wg.Id)
				SelectionManager.SelectedWorkGroup = wg;
		}

		if (cmd.WorkId is not null)
		{
			var work = SelectionManager.WorkList?.FirstOrDefault(w => w.Id == cmd.WorkId);
			if (work is not null && SelectionManager.SelectedWork?.Id != work.Id)
				SelectionManager.SelectedWork = work;
		}

		if (cmd.TrainId is not null)
		{
			var train = SelectionManager.OrderedTrainDataList?.FirstOrDefault(t => t.Id == cmd.TrainId);
			if (train is not null && SelectionManager.SelectedTrainData?.Id != train.Id)
				SelectionManager.SelectedTrainData = train;
		}
	}

	/// <summary>
	/// サーバーから指示されたヘッダの色 (RGB)。null は端末既定。
	/// View 側はこの値を購読してタイトルバー色を変更する。
	/// </summary>
	[ObservableProperty]
	int? _HeaderColorOverride_RGB;

	void OnHeaderColorChangeRequested(object? sender, HeaderColorCommand cmd)
	{
		HeaderColorOverride_RGB = cmd.ResetToDefault ? null : cmd.Color_RGB;
	}

	/// <summary>
	/// サーバーから指示されたタイトルバー時刻表示フォーマット ("HH:mm:ss" 等)。
	/// null は端末既定 ("HH:mm:ss" を内部既定とする)。
	/// </summary>
	[ObservableProperty]
	string? _HeaderTimeFormat;

	void OnTimeFormatChangeRequested(object? sender, TimeFormatCommand cmd)
	{
		HeaderTimeFormat = cmd.Format;
	}

	/// <summary>
	/// Attempts to load default timetable file with privacy policy check.
	/// If privacy policy is not accepted, returns false to indicate policy screen is needed first.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token for async operations</param>
	/// <returns>Tuple (success, requiresFileSelection, selectedFilePath, errorMessage)</returns>
	public async Task<(bool success, bool requiresFileSelection, string? selectedFilePath, string? errorMessage)> TryLoadDefaultTimetableAsync(
		CancellationToken cancellationToken = default)
	{
		try
		{
			// Check privacy policy first
			var firebaseSetting = InstanceManager.FirebaseSettingViewModel;
			if (!firebaseSetting.IsPrivacyPolicyAccepted)
			{
				logger.Info("Privacy policy not accepted yet - file loading deferred");
				return (false, false, null, "PrivacyPolicyNotAccepted");
			}

			// Try to load default timetable
			(var loader, var selectedFilePath, var requiresFileSelection) =
				await DefaultTimetableFileLoader.TryLoadDefaultTimetableAsync(cancellationToken);

			if (loader is not null)
			{
				logger.Info("Successfully loaded default timetable: {0}", selectedFilePath);
				Loader = loader;
				return (true, false, selectedFilePath, null);
			}

			if (requiresFileSelection)
			{
				logger.Info("Multiple JSON files found - user selection required");
				return (true, true, null, null);
			}

			// No files found or failed to load
			logger.Info("No default timetable file found");
			return (false, false, null, null);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Error in TryLoadDefaultTimetableAsync");
			return (false, false, null, ex.Message);
		}
	}

	/// <summary>
	/// Loads a specific timetable file after user selection
	/// </summary>
	/// <param name="filePath">Full path to the file to load</param>
	/// <param name="cancellationToken">Cancellation token for async operations</param>
	/// <returns>True if successfully loaded, false otherwise</returns>
	public async Task<bool> LoadSelectedTimetableFileAsync(
		string filePath,
		CancellationToken cancellationToken = default)
	{
		try
		{
			logger.Info("Loading selected timetable file: {0}", filePath);
			var loader = await DefaultTimetableFileLoader.LoadTimetableFileAsync(filePath, cancellationToken);

			if (loader is not null)
			{
				Loader = loader;
				logger.Trace("Successfully loaded selected timetable file");
				return true;
			}

			logger.Warn("Failed to load selected timetable file");
			return false;
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Error in LoadSelectedTimetableFileAsync");
			return false;
		}
	}
}
