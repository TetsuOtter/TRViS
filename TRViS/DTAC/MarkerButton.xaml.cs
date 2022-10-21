using CommunityToolkit.Maui.Views;

using DependencyPropertyGenerator;

using TRViS.ViewModels;

namespace TRViS.DTAC;

[DependencyProperty<DTACMarkerViewModel>("MarkerSettings")]
public partial class MarkerButton : Frame
{
	public MarkerButton()
	{
		InitializeComponent();
	}

	partial void OnMarkerSettingsChanged(DTACMarkerViewModel? newValue)
	{
		BindingContext = newValue;
	}

	async void TapGestureRecognizer_Tapped(object sender, EventArgs e)
	{
		if (Shell.Current.CurrentPage is not ViewHost page || MarkerSettings is null)
			return;

		if (MarkerSettings.IsToggled)
		{
			MarkerSettings.IsToggled = false;
			return;
		}

		MarkerSettings.IsToggled = true;

		SelectMarkerPopup popup = new(MarkerSettings)
		{
			Anchor = this,
		};

		await page.ShowPopupAsync(popup);
	}
}
