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

	// Human-readable label for the current Loader's source. Set atomically alongside
	// Loader via SetLoader() so the Home info card cannot momentarily show a stale
	// source between the two assignments. Cleared automatically when Loader becomes null.
	[ObservableProperty]
	string? _LoaderSourceLabel;

	/// <summary>
	/// Atomically replaces <see cref="Loader"/> and <see cref="LoaderSourceLabel"/>.
	/// Call sites should prefer this over assigning Loader directly so the Home info
	/// card metadata stays in sync with the active loader.
	/// </summary>
	public void SetLoader(ILoader? loader, string? sourceLabel)
	{
		LoaderSourceLabel = sourceLabel;
		Loader = loader;
	}

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
	}

	partial void OnLoaderChanged(ILoader? value)
	{
		SelectionManager.Loader = value;
		if (value is null)
			LoaderSourceLabel = null;
	}

	void OnTimetableUpdated(object? sender, TimetableData timetableData)
	{
		logger.Debug("TimetableUpdated: WorkGroupId={0}, WorkId={1}, TrainId={2}, Scope={3}",
			timetableData.WorkGroupId, timetableData.WorkId, timetableData.TrainId, timetableData.Scope);

		// 時刻表の変更スコープに応じて、表示継続可能か判定する
		bool canContinue = CanContinueCurrentTimetable(timetableData);

		if (!canContinue)
		{
			// 表示継続不可の場合は初期状態に戻す
			logger.Info("Timetable changed and cannot continue -> reset to initial state");
			SelectionManager.ResetToFirst();
		}

		// Loader のキャッシュが更新された可能性があるため、UI を再読み込み
		// 特に WebSocket の場合は Loader のデータが動的に更新されるため、毎回再読み込みする必要がある
		if (Loader is not null)
		{
			logger.Debug("Refreshing selection from Loader cache");
			SelectionManager.Refresh();
		}
	}

	bool CanContinueCurrentTimetable(TimetableData timetableData)
	{
		// 変更スコープに基づいて判定する
		return timetableData.Scope switch
		{
			// All：全体の情報が更新された場合は表示継続不可
			TimetableScopeType.All => false,

			// WorkGroup単位の変更：現在の選択がこのWorkGroupと異なる場合のみ継続可能
			TimetableScopeType.WorkGroup => SelectionManager.SelectedWorkGroup?.Id != timetableData.WorkGroupId,

			// Work単位の変更：現在の選択がこのWorkと異なる場合のみ継続可能
			TimetableScopeType.Work => SelectionManager.SelectedWork?.Id != timetableData.WorkId,

			// Train単位の変更：現在の選択がこのTrainと異なる場合のみ継続可能
			TimetableScopeType.Train => SelectionManager.SelectedTrainData?.Id != timetableData.TrainId,

			_ => true
		};
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
				SetLoader(loader, selectedFilePath is not null ? System.IO.Path.GetFileName(selectedFilePath) : null);
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
				SetLoader(loader, System.IO.Path.GetFileName(filePath));
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
