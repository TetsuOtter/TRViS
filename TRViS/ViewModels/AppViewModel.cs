using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using TRViS.IO;
using TRViS.IO.Models;

namespace TRViS.ViewModels;

public partial class AppViewModel : ObservableObject
{
	[ObservableProperty]
	LoaderSQL? _Loader;

	[ObservableProperty]
	IReadOnlyList<TRViS.IO.Models.DB.WorkGroup>? _WorkGroupList;

	[ObservableProperty]
	IReadOnlyList<TRViS.IO.Models.DB.Work>? _WorkList;

	[ObservableProperty]
	IReadOnlyList<TRViS.IO.Models.DB.TrainData>? _DBTrainDataList;

	[ObservableProperty]
	TRViS.IO.Models.DB.WorkGroup? _SelectedWorkGroup;
	[ObservableProperty]
	TRViS.IO.Models.DB.Work? _SelectedWork;
	[ObservableProperty]
	TRViS.IO.Models.DB.TrainData? _SelectedDBTrainData;

	[ObservableProperty]
	TrainData? _SelectedTrainData;

	partial void OnLoaderChanged(LoaderSQL? value)
	{
		WorkGroupList = null;
		SelectedWorkGroup = null;

		if (value is not null)
			WorkGroupList = value.GetWorkGroupList();
	}

	partial void OnSelectedWorkGroupChanged(TRViS.IO.Models.DB.WorkGroup? value)
	{
		WorkList = null;
		SelectedWork = null;

		if (value is not null)
			WorkList = Loader?.GetWorkList(value.Id);
	}

	partial void OnSelectedWorkChanged(IO.Models.DB.Work? value)
	{
		DBTrainDataList = null;
		SelectedDBTrainData = null;

		if (value is not null)
			DBTrainDataList = Loader?.GetTrainDataList(value.Id);
	}

	partial void OnSelectedDBTrainDataChanged(IO.Models.DB.TrainData? value)
		=> SelectedTrainData = value is null ? null : Loader?.GetTrainData(value.Id);
}
