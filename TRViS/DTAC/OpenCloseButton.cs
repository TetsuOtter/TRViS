using DependencyPropertyGenerator;
using Microsoft.AppCenter.Crashes;

namespace TRViS.DTAC;

[DependencyProperty<bool>("IsOpen", DefaultBindingMode = DefaultBindingMode.TwoWay)]
[DependencyProperty<string>("TextWhenOpen")]
[DependencyProperty<string>("TextWhenClosed")]
public partial class OpenCloseButton : Button
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
	public event EventHandler<ValueChangedEventArgs<bool>>? IsOpenChanged;

	public OpenCloseButton()
	{
		logger.Trace("Creating...");

		this.SetBinding(TextProperty, new Binding()
		{
			Source = this,
			Path = nameof(TextWhenClosed)
		});

		DataTrigger dataTrigger = new(typeof(OpenCloseButton))
		{
			Binding = new Binding()
			{
				Source = this,
				Path = nameof(IsOpen)
			},
			Value = true,
		};

		dataTrigger.Setters.Add(new()
		{
			Property = TextProperty,
			Value = new Binding()
			{
				Source = this,
				Path = nameof(TextWhenOpen)
			}
		});

		Triggers.Add(dataTrigger);

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
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			Crashes.TrackError(ex);
			Utils.ExitWithAlert(ex);
		}
	}
}
