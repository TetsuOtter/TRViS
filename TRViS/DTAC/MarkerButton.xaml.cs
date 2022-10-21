using CommunityToolkit.Maui.Views;

using DependencyPropertyGenerator;

using TRViS.ViewModels;

namespace TRViS.DTAC;

[DependencyProperty<bool>("IsMarkModeToggled")]
public partial class MarkerButton : Frame
{
	DTACMarkerViewModel VM { get; } = new();

	public MarkerButton()
	{
		InitializeComponent();
	}

	async void TapGestureRecognizer_Tapped(object sender, EventArgs e)
	{
		if (Shell.Current.CurrentPage is not ViewHost page)
			return;

		if (IsMarkModeToggled)
		{
			IsMarkModeToggled = false;
			return;
		}

		IsMarkModeToggled = true;

		SelectMarkerPopup popup = new(VM)
		{
			Anchor = this,
		};

		await page.ShowPopupAsync(popup);
	}
}
