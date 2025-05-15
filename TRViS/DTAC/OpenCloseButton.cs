using DependencyPropertyGenerator;

namespace TRViS.DTAC;

[DependencyProperty<bool>("IsOpen", DefaultBindingMode = DefaultBindingMode.TwoWay)]
[DependencyProperty<string>("TextWhenOpen")]
[DependencyProperty<string>("TextWhenClosed")]
public partial class OpenCloseButton : Button
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	public event EventHandler<ValueChangedEventArgs<bool>>? IsOpenChanged;

	public OpenCloseButton()
	{
		logger.Trace("Creating...");

		Text = IsOpen ? TextWhenOpen : TextWhenClosed;
		CornerRadius = 4;
		Padding = 0;
		BorderWidth = 0;
		FontFamily = "MaterialIconsRegular";
		FontSize = 40;
		FontAutoScalingEnabled = false;
		DTACElementStyles.OpenCloseButtonBGColor.Apply(this, BackgroundColorProperty);
		DTACElementStyles.OpenCloseButtonTextColor.Apply(this, TextColorProperty);

		Shadow = new()
		{
			Brush = Colors.Black,
			Offset = new(1, 1),
			Radius = 2,
			Opacity = 0.4f
		};

		Clicked += (_, e) => IsOpen = !IsOpen;

		logger.Trace("Created");
	}

	partial void OnIsOpenChanged(bool oldValue, bool newValue)
	{
		logger.Info("IsOpen changed from {0} to {1}", oldValue, newValue);
		try
		{
			IsOpenChanged?.Invoke(this, new(oldValue, newValue));
			Text = newValue ? TextWhenOpen : TextWhenClosed;
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "OpenCloseButton.OnIsOpenChanged");
			Utils.ExitWithAlert(ex);
		}
	}

	partial void OnTextWhenOpenChanged(string? newValue)
	{
		if (IsOpen)
		{
			Text = newValue;
		}
	}
	partial void OnTextWhenClosedChanged(string? newValue)
	{
		if (!IsOpen)
		{
			Text = newValue;
		}
	}
}
