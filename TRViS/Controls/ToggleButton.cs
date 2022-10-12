using DependencyPropertyGenerator;

namespace TRViS.Controls;

[DependencyProperty<bool>("IsChecked", DefaultBindingMode = DefaultBindingMode.TwoWay)]
public partial class ToggleButton : ContentView
{
	public ToggleButton()
	{
		TapGestureRecognizer gestureRecognizer = new();
		gestureRecognizer.Tapped += (_, _) =>
		{
			if (IsEnabled)
				IsChecked = !IsChecked;
		};

		this.GestureRecognizers.Add(gestureRecognizer);
	}
}
