using Microsoft.Maui.Controls.Shapes;

using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS.DTAC;

public class HorizontalTimetableButton : Border
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	const string NormalButtonText = "横型時刻表"; // 横型時刻表
	const string ETrainButtonText = "Ｅ電時刻表"; // Ｅ電時刻表

	readonly Label _label;

	public HorizontalTimetableButton()
	{
		logger.Trace("Creating...");

		AutomationId = "DTAC.HorizontalTimetableButton";
		HorizontalOptions = LayoutOptions.Fill;
		VerticalOptions = LayoutOptions.Fill;
		Shadow = new Shadow
		{
			Brush = Colors.Black,
			Offset = new Point(1, 1),
			Radius = 2,
			Opacity = 0.4f
		};

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
			Text = vm.UseETrainTimetableName ? ETrainButtonText : NormalButtonText,
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

	void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(EasterEggPageViewModel.UseETrainTimetableName))
			return;

		if (sender is not EasterEggPageViewModel vm)
			return;

		MainThread.BeginInvokeOnMainThread(() =>
		{
			_label.Text = vm.UseETrainTimetableName ? ETrainButtonText : NormalButtonText;
		});
	}
}
