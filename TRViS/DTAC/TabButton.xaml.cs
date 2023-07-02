using System.ComponentModel;
using DependencyPropertyGenerator;
using TRViS.ViewModels;

namespace TRViS.DTAC;

[DependencyProperty<DTACViewHostViewModel.Mode>("CurrentMode", DefaultBindingMode = DefaultBindingMode.TwoWay)]
[DependencyProperty<DTACViewHostViewModel.Mode>("TargetMode")]
[DependencyProperty<bool>("IsSelected", IsReadOnly = true)]
[DependencyProperty<string>("Text")]
public partial class TabButton : ContentView
{
	public static readonly Color BASE_COLOR_DISABLED = new(0xDD, 0xDD, 0xDD);

	public TabButton()
	{
		InitializeComponent();

		UpdateIsSelectedProperty();
		DTACElementStyles.TimetableTextColor.Apply(ButtonLabel, Label.TextColorProperty);
	}

	partial void OnCurrentModeChanged()
		=> UpdateIsSelectedProperty();
	partial void OnTargetModeChanged()
		=> UpdateIsSelectedProperty();

	void UpdateIsSelectedProperty()
		=> IsSelected = (CurrentMode == TargetMode);

	partial void OnTextChanged(string? newValue)
		=> ButtonLabel.Text = newValue;

	partial void OnIsSelectedChanged(bool newValue)
	{
		BottomLine.IsVisible = newValue;

		if (newValue)
		{
			DTACElementStyles.DefaultBGColor.Apply(BaseBox, BoxView.ColorProperty);
			BaseBox.Shadow.Opacity = 0.2f;
		}
		else
		{
			DTACElementStyles.TabButtonBGColor.Apply(BaseBox, BoxView.ColorProperty);
			BaseBox.Shadow.Opacity = 0;
		}
	}

	void BaseBox_Tapped(object sender, EventArgs e)
	{
		if (IsSelected || !IsEnabled)
			return;

		CurrentMode = TargetMode;
	}
}
