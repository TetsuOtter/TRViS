using Microsoft.Maui.Controls.Shapes;

using TRViS.MyAppCustomizables;
using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS.DTAC;

public class HorizontalTimetableButton : Border
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	internal const string HorizontalButtonText = "横型時刻表";
	internal const string TrainButtonText = "電車時刻表";
	internal const string ETrainButtonText = "Ｅ電時刻表";

	readonly Label _label;

	public HorizontalTimetableButton()
	{
		logger.Trace("Creating...");

		AutomationId = "DTAC.HorizontalTimetableButton";
		HorizontalOptions = LayoutOptions.Fill;
		VerticalOptions = LayoutOptions.Fill;
		// Shadow is assigned by SetShadowVisible(true) from PageHeader when the
		// button becomes user-visible. Leaving Shadow=null while hidden avoids
		// MAUI's PlatformWrapperView.drawShadow path on Android, which has been
		// observed to OOM the Glide LruBitmapPool when the View is laid out at
		// LayoutOptions.Fill before its 0-width column collapses it.

		Stroke = Colors.Transparent;
		StrokeShape = new RoundRectangle
		{
			CornerRadius = 8
		};
		Margin = new(2, 8);
		Padding = 0;
		DTACElementStyles.OpenCloseButtonBGColor.Apply(this, Border.BackgroundColorProperty);

		EasterEggPageViewModel vm = InstanceManager.EasterEggPageViewModel;
		_label = new Label
		{
			Text = GetButtonText(vm.HorizontalTimetableButtonLabel),
			FontSize = 28,
			FontFamily = DTACElementStyles.DefaultFontFamily,
			FontAttributes = FontAttributes.Bold,
			FontAutoScalingEnabled = false,
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.Center,
			Margin = 4,
		};
		Content = _label;
		DTACElementStyles.HorizontalTimetableButtonTextColor.Apply(_label, Label.TextColorProperty);

		vm.PropertyChanged += OnViewModelPropertyChanged;

		logger.Trace("Created");
	}

	internal static string GetButtonText(HorizontalTimetableButtonLabel label) => label switch
	{
		HorizontalTimetableButtonLabel.Train => TrainButtonText,
		HorizontalTimetableButtonLabel.ETrain => ETrainButtonText,
		_ => HorizontalButtonText,
	};

	static readonly Shadow s_shadow = new()
	{
		Brush = Colors.Black,
		Offset = new Point(1, 1),
		Radius = 2,
		Opacity = 0.4f
	};

	internal void SetShadowVisible(bool visible)
	{
		Shadow = visible ? s_shadow : null!;
	}

	void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(EasterEggPageViewModel.HorizontalTimetableButtonLabel))
			return;

		if (sender is not EasterEggPageViewModel vm)
			return;

		MainThread.BeginInvokeOnMainThread(() =>
		{
			_label.Text = GetButtonText(vm.HorizontalTimetableButtonLabel);
		});
	}
}
