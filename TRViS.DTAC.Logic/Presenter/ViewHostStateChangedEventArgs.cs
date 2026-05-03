namespace TRViS.DTAC.Logic.Presenter;

/// <summary>
/// Flags indicating which sections of ViewHostPageState have changed.
/// </summary>
[Flags]
public enum ViewHostStateSection
{
    None = 0,
    TitleText = 1,
    TimeLabel = 8,
    BgAppIcon = 64,
    All = ~0,
}

/// <summary>
/// Event args carrying which sections of ViewHostPageState changed.
/// </summary>
public class ViewHostStateChangedEventArgs(ViewHostStateSection changed) : EventArgs
{
    public ViewHostStateSection Changed { get; } = changed;
}
