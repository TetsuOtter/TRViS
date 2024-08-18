using DependencyPropertyGenerator;
using Microsoft.AppCenter.Crashes;

namespace TRViS.Controls;

[DependencyProperty<bool>("IsChecked", DefaultBindingMode = DefaultBindingMode.TwoWay)]
[DependencyProperty<bool>("IsRadio", DefaultBindingMode = DefaultBindingMode.OneWay)]
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
			try
			{
				Point? pt = e.GetPosition(this);
				logger.Debug("Tapped (Pont: {0}, IsEnabled: {1}, IsChecked Before: {2})", pt, IsEnabled, IsChecked);
				if (IsEnabled)
					IsChecked = IsRadio || !IsChecked;
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				Crashes.TrackError(ex);
				Utils.ExitWithAlert(ex);
			}
		};

		this.GestureRecognizers.Add(gestureRecognizer);

		logger.Trace("Created");
	}

	partial void OnIsCheckedChanged(bool oldValue, bool newValue)
	{
		try
		{
			logger.Debug("OnIsCheckedChanged: {0} -> {1}", oldValue, newValue);
			IsCheckedChanged?.Invoke(this, new(oldValue, newValue));
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			Crashes.TrackError(ex);
			Utils.ExitWithAlert(ex);
		}
	}
}
