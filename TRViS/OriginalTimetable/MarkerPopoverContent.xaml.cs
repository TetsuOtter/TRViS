using TR.Maui.AnchorPopover;
using TRViS.ViewModels;

namespace TRViS.OriginalTimetable;

// Static XAML-defined content for the V1 marker chooser popover. Built once
// and handed to AnchorPopover.ShowAsync — *no* imperative tree mutation (the
// previous V1 implementation crashed in ApplyStyleSheets on iPad MAUI when
// custom controls were Add()ed at runtime; this pattern follows MarkerButton
// + SelectMarkerPopup which Show without that NRE).
public partial class MarkerPopoverContent : ContentView
{
	IAnchorPopover? _popover;
	Action<MarkerKind>? _onSelected;

	public MarkerPopoverContent()
	{
		InitializeComponent();
	}

	internal void Configure(IAnchorPopover popover, MarkerKind current, Action<MarkerKind> onSelected)
	{
		_popover = popover;
		_onSelected = onSelected;
		ApplyAccent(current);
	}

	void ApplyAccent(MarkerKind current)
	{
		// Stroke the currently-selected option with the accent color so the
		// user sees which marker is active before they tap to change it.
		var accent = (Brush?)Application.Current?.Resources["OT_Accent"];
		var rule = (Brush?)Application.Current?.Resources["OT_Rule"];
		NoneBorder.Stroke = current == MarkerKind.None ? accent : rule;
		FlagBorder.Stroke = current == MarkerKind.Flag ? accent : rule;
		CautionBorder.Stroke = current == MarkerKind.Caution ? accent : rule;
		StarBorder.Stroke = current == MarkerKind.Star ? accent : rule;
		NoneBorder.StrokeThickness = current == MarkerKind.None ? 2 : 1;
		FlagBorder.StrokeThickness = current == MarkerKind.Flag ? 2 : 1;
		CautionBorder.StrokeThickness = current == MarkerKind.Caution ? 2 : 1;
		StarBorder.StrokeThickness = current == MarkerKind.Star ? 2 : 1;
	}

	async void OnNoneTapped(object? sender, TappedEventArgs e) => await PickAsync(MarkerKind.None);
	async void OnFlagTapped(object? sender, TappedEventArgs e) => await PickAsync(MarkerKind.Flag);
	async void OnCautionTapped(object? sender, TappedEventArgs e) => await PickAsync(MarkerKind.Caution);
	async void OnStarTapped(object? sender, TappedEventArgs e) => await PickAsync(MarkerKind.Star);

	async Task PickAsync(MarkerKind kind)
	{
		_onSelected?.Invoke(kind);
		if (_popover is not null)
		{
			await _popover.DismissAsync();
		}
	}
}
