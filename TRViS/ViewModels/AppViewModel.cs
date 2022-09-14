using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using TRViS.IO.Models;

namespace TRViS.ViewModels;

public partial class AppViewModel : ObservableObject
{
	public ObservableCollection<TrainData> TrainDataList { get; } = new();

	[ObservableProperty]
	TrainData? _SelectedTrainData;
}
