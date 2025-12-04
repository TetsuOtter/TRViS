using CommunityToolkit.Mvvm.ComponentModel;

namespace TRViS.DTAC.ViewModels;

public partial class VerticalTimetableColumnVisibilityState : ObservableObject
{
	[ObservableProperty]
	public partial bool TrainNumber { get; set; } = true;
	[ObservableProperty]
	public partial bool MaxSpeed { get; set; } = true;
	[ObservableProperty]
	public partial bool SpeedType { get; set; } = true;
	[ObservableProperty]
	public partial bool NominalTractiveCapacity { get; set; } = true;

	[ObservableProperty]
	public partial bool RunTime { get; set; } = true;
	[ObservableProperty]
	public partial bool StationName { get; set; } = true;
	[ObservableProperty]
	public partial bool ArrivalTime { get; set; } = true;
	[ObservableProperty]
	public partial bool DepartureTime { get; set; } = true;
	[ObservableProperty]
	public partial bool TrackName { get; set; } = true;
	[ObservableProperty]
	public partial bool RunInOutLimit { get; set; } = true;
	[ObservableProperty]
	public partial bool Remarks { get; set; } = true;
	[ObservableProperty]
	public partial bool Marker { get; set; } = true;

	public VerticalTimetableColumnVisibilityState(int width)
	{
		UpdateState(width);
	}

	const int IPAD_MINI_WIDTH = 768;

	public void UpdateState(int width)
	{
		TrainNumber = width switch
		{
			< IPAD_MINI_WIDTH => false,
			_ => true,
		};
		MaxSpeed = width switch
		{
			< IPAD_MINI_WIDTH => false,
			_ => true,
		};
		SpeedType = width switch
		{
			< IPAD_MINI_WIDTH => false,
			_ => true,
		};
		NominalTractiveCapacity = width switch
		{
			< IPAD_MINI_WIDTH => false,
			_ => true,
		};

		RunTime = width switch
		{
			< IPAD_MINI_WIDTH => false,
			_ => true,
		};
		StationName = width switch
		{
			< IPAD_MINI_WIDTH => true,
			_ => true,
		};
		ArrivalTime = width switch
		{
			< IPAD_MINI_WIDTH => true,
			_ => true,
		};
		DepartureTime = width switch
		{
			< IPAD_MINI_WIDTH => true,
			_ => true,
		};
		TrackName = width switch
		{
			< IPAD_MINI_WIDTH => false,
			_ => true,
		};
		RunInOutLimit = width switch
		{
			< IPAD_MINI_WIDTH => false,
			_ => true,
		};
		Remarks = width switch
		{
			< IPAD_MINI_WIDTH => true,
			_ => true,
		};
		Marker = width switch
		{
			< IPAD_MINI_WIDTH => false,
			_ => true,
		};
	}
}
