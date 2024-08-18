using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using TRViS.IO;
using TRViS.IO.Models;
using TRViS.Services;

namespace TRViS.ViewModels;

public partial class AppViewModel : ObservableObject
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

	[ObservableProperty]
	ILoader? _Loader;

	[ObservableProperty]
	IReadOnlyList<TRViS.IO.Models.DB.WorkGroup>? _WorkGroupList;

	[ObservableProperty]
	IReadOnlyList<TRViS.IO.Models.DB.Work>? _WorkList;

	[ObservableProperty]
	TRViS.IO.Models.DB.WorkGroup? _SelectedWorkGroup;
	[ObservableProperty]
	TRViS.IO.Models.DB.Work? _SelectedWork;

	[ObservableProperty]
	TrainData? _SelectedTrainData;

	[ObservableProperty]
	double _WindowHeight;

	[ObservableProperty]
	double _WindowWidth;

	public event EventHandler<ValueChangedEventArgs<AppTheme>>? CurrentAppThemeChanged;
	AppTheme _SystemAppTheme;
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
		}
	}

	public AppViewModel()
	{
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

		_ExternalResourceUrlHistory = AppPreferenceService.GetFromJson<List<string>>(AppPreferenceKeys.ExternalResourceUrlHistory, [], out _);
	}

	partial void OnLoaderChanged(ILoader? value)
	{
		SelectedWorkGroup = null;
		WorkGroupList = value?.GetWorkGroupList();
		SelectedWorkGroup = WorkGroupList?.FirstOrDefault();
	}

	partial void OnSelectedWorkGroupChanged(TRViS.IO.Models.DB.WorkGroup? value)
	{
		WorkList = null;
		SelectedWork = null;

		if (value is not null)
		{
			WorkList = Loader?.GetWorkList(value.Id);

			SelectedWork = WorkList?.FirstOrDefault();
		}
	}

	partial void OnSelectedWorkChanged(IO.Models.DB.Work? value)
	{
		logger.Debug("Work: {0}", value?.Id ?? "null");
		if (value is not null)
		{
			string? trainId = Loader?.GetTrainDataList(value.Id)?.FirstOrDefault()?.Id;
			logger.Debug("FirstTrainId: {0}", trainId ?? "null");
			var selectedTrainData = trainId is null ? null : Loader?.GetTrainData(trainId);
			SelectedTrainData = selectedTrainData;
			logger.Debug("SelectedTrainData: {0} ({1})", SelectedTrainData?.Id ?? "null", selectedTrainData?.Id ?? "null");
		}
		else
		{
			SelectedTrainData = null;
		}
	}
}
