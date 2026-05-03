namespace TRViS.DTAC.Logic.Presenter;

/// <summary>
/// Flags indicating which sections of VerticalPageState have changed
/// </summary>
[Flags]
public enum VerticalPageStateSection
{
	None = 0,
	Destination = 1,
	TrainInfoArea = 2,
	NextDayIndicator = 4,
	ActivityIndicator = 16,
	TimetableView = 32,
	LocationService = 128,
	PageHeader = 256,
	TrainDisplayInfo = 512,
	RowStates = 1024,
	All = ~0
}

/// <summary>
/// Event args carrying which sections of VerticalPageState changed
/// </summary>
public class VerticalPageStateChangedEventArgs(VerticalPageStateSection changed) : EventArgs
{
	public VerticalPageStateSection Changed { get; } = changed;
}
