namespace TRViS.DTAC.Logic.Presenter;

/// <summary>
/// Flags indicating which sections of HakoPageState have changed.
/// </summary>
[Flags]
public enum HakoStateSection
{
	None = 0,
	AffectDate = 1,
	WorkInfo = 2,
	All = ~0,
}

/// <summary>
/// Event args carrying which sections of HakoPageState changed.
/// </summary>
public class HakoStateChangedEventArgs(HakoStateSection changed) : EventArgs
{
	public HakoStateSection Changed { get; } = changed;
}
