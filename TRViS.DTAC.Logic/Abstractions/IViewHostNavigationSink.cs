namespace TRViS.DTAC.Logic.Abstractions;

/// <summary>
/// Accepts navigation notifications for the ViewHost page.
/// Implemented by adapters that need to write IsViewHostVisible back to the view model.
/// </summary>
public interface IViewHostNavigationSink
{
    /// <summary>
    /// Called when Shell navigation results in the ViewHost becoming current (or not).
    /// </summary>
    void NotifyNavigated(bool isCurrentPage);
}
