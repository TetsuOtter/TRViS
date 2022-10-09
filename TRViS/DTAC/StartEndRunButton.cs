using DependencyPropertyGenerator;

namespace TRViS.DTAC;

[DependencyProperty<bool>("IsRunStarted", DefaultBindingMode = DefaultBindingMode.TwoWay)]
public partial class StartEndRunButton : Frame
{
	public event EventHandler? RunEnded;

	const string ICON_START_RUN = "\ue039";
	const string ICON_END_RUN = "\ue14b";
	const string TEXT_START_RUN = "運行開始";
	const string TEXT_END_RUN = "運行終了";
	static readonly Color GREEN = new(0, 0x80, 0);
	const float BUTTON_LUMINOUS_DELTA = 0.05f;

	Label IconLabel { get; } = new()
	{
		FontFamily = "MaterialIconsRegular",
		FontSize = 32,
		TextColor = Colors.White,
		FontAttributes = FontAttributes.Bold,
		VerticalOptions = LayoutOptions.Center,
		Margin = new(2),
		Padding = new(0),
	};
	Label TextLabel { get; } = new()
	{
		FontFamily = "Hiragino Sans",
		FontSize = 24,
		TextColor = Colors.White,
		FontAttributes = FontAttributes.Bold,
		VerticalOptions = LayoutOptions.Center,
		Margin = new(4),
		Padding = new(0),
	};

	public StartEndRunButton()
	{
		Padding = new(0);
		Background = null;
		CornerRadius = 6;

#if !IOS && !MACCATALYST
		// iOSとMacCatalystでは、MAUI側のバグによりGradientBrushが適用されない
		// ref: https://github.com/dotnet/maui/pull/7925
		// TODO: iOS/MacCatalystでもGradientBrushを適用する
		LinearGradientBrush brush = new()
		{
			StartPoint = new(0, 0),
			EndPoint = new(0, 1)
		};

		brush.GradientStops.Add(new(GREEN.AddLuminosity(BUTTON_LUMINOUS_DELTA), 0.1f));
		brush.GradientStops.Add(new(GREEN.AddLuminosity(-BUTTON_LUMINOUS_DELTA), 1.0f));

		Background = brush;
#else
		BackgroundColor = GREEN;
#endif

		Shadow = new()
		{
			Brush = new SolidColorBrush(Colors.Black),
			Offset = new(3, 3),
			Radius = 3,
			Opacity = 0.2f
		};

		TapGestureRecognizer tapGestureRecognizer = new();
		tapGestureRecognizer.Tapped += OnTapped;
		this.GestureRecognizers.Add(tapGestureRecognizer);

		HorizontalStackLayout layout = new()
		{
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			ScaleY = 0.95f,
		};

		layout.Add(IconLabel);
		layout.Add(TextLabel);

		OnIsRunStartedChanged(this.IsRunStarted);

		Content = layout;
	}

	partial void OnIsRunStartedChanged(bool newValue)
	{
		if (newValue)
		{
			IconLabel.Text = ICON_END_RUN;
			TextLabel.Text = TEXT_END_RUN;
		}
		else
		{
			IconLabel.Text = ICON_START_RUN;
			TextLabel.Text = TEXT_START_RUN;

			RunEnded?.Invoke(this, new());
		}
	}

	private void OnTapped(object? sender, EventArgs e)
	{
		IsRunStarted = !IsRunStarted;
	}
}
