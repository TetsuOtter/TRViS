namespace TRViS.DTAC;

/// <summary>
/// In-app replacement for <c>TR.Maui.AnchorPopover.IAnchorPopover</c>'s dismiss
/// handle. The AnchorPopover package (built against MAUI 9) is binary-incompatible
/// with the project's MAUI 10 on Windows — calling its <c>ShowAsync</c> throws
/// <c>MissingMethodException</c> for <c>ElementExtensions.ToPlatform</c> and
/// crashes the app (#266 retired its own usage for the same reason; #273 covers
/// the two remaining ones: QuickSwitchPopup and SelectMarkerPopup).
///
/// Popups are now rendered as an in-page overlay owned by <see cref="ViewHost"/>,
/// which implements this interface so popup content can request its own dismissal
/// (e.g. SelectMarkerPopup's Close button) without referencing the page type.
/// </summary>
public interface IPagePopupHost
{
	/// <summary>Dismisses the currently displayed in-page popup.</summary>
	Task DismissAsync();
}
