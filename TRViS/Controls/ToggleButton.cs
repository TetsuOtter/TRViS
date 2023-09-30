using DependencyPropertyGenerator;

namespace TRViS.Controls;

[DependencyProperty<bool>("IsChecked", DefaultBindingMode = DefaultBindingMode.TwoWay)]
public partial class ToggleButton : ContentView
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
	public EventHandler<ValueChangedEventArgs<bool>>? IsCheckedChanged;

	public ToggleButton()
	{
		logger.Trace("Creating...");

		TapGestureRecognizer gestureRecognizer = new();
		gestureRecognizer.Tapped += (_, e) =>
		{
			Point? pt = e.GetPosition(this);
			logger.Debug("Tapped (Pont: {0}, IsEnabled: {1}, IsChecked Before: {2})", pt, IsEnabled, IsChecked);
			if (IsEnabled)
				IsChecked = !IsChecked;
		};

		this.GestureRecognizers.Add(gestureRecognizer);

		logger.Trace("Created");
	}

	partial void OnIsCheckedChanged(bool oldValue, bool newValue)
		=> IsCheckedChanged?.Invoke(this, new(oldValue, newValue));
}
