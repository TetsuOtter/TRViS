using DependencyPropertyGenerator;

using TRViS.IO.Models;
using TRViS.Services;

namespace TRViS.DTAC;

[DependencyProperty<TimeData>("TimeData")]
[DependencyProperty<bool>("IsPass")]
[DependencyProperty<bool>("IsArrowVisible", IsReadOnly = true)]
[DependencyProperty<bool>("IsStringVisible", IsReadOnly = true)]
[DependencyProperty<Color>("TextColor", IsReadOnly = true)]
[DependencyProperty<string>("HHMM", IsReadOnly = true)]
public partial class TimeCell : Grid
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	private readonly Label timeLabel_HHMM;
	private readonly Label timeLabel_SS;
	private readonly Label stringLabel;
	private readonly Image arrowImage;

	public TimeCell()
	{
		logger.Trace("Creating...");

		HorizontalOptions = LayoutOptions.Center;
		VerticalOptions = LayoutOptions.Center;

		ColumnDefinitions.Add(new ColumnDefinition(72));
		ColumnDefinitions.Add(new ColumnDefinition(24));

		timeLabel_HHMM = DTACElementStyles.Instance.TimetableLargeNumberLabel<Label>();
		timeLabel_HHMM.HorizontalOptions = LayoutOptions.End;
		timeLabel_HHMM.Margin = new(0);
		timeLabel_HHMM.SetBinding(Label.TextColorProperty, new Binding("TextColor", source: this));
		timeLabel_HHMM.SetBinding(Label.TextProperty, new Binding("HHMM", source: this));
		Grid.SetColumn(timeLabel_HHMM, 0);

		timeLabel_SS = DTACElementStyles.Instance.TimetableDefaultNumberLabel<Label>();
		timeLabel_SS.HorizontalOptions = LayoutOptions.Start;
		timeLabel_SS.SetBinding(Label.TextColorProperty, new Binding("TextColor", source: this));
		timeLabel_SS.SetBinding(Label.TextProperty, new Binding("TimeData.Second", source: this, stringFormat: "{0:D02}"));
		Grid.SetColumn(timeLabel_SS, 1);

		stringLabel = DTACElementStyles.Instance.TimetableLargeNumberLabel<Label>();
		stringLabel.HorizontalOptions = LayoutOptions.Center;
		stringLabel.VerticalOptions = LayoutOptions.Center;
		stringLabel.Padding = new(2);
		stringLabel.FontAttributes = FontAttributes.Bold;
		stringLabel.SetBinding(Label.IsVisibleProperty, new Binding("IsStringVisible", source: this));
		stringLabel.SetBinding(Label.TextColorProperty, new Binding("TextColor", source: this));
		stringLabel.SetBinding(Label.TextProperty, new Binding("TimeData.Text", source: this));
		Grid.SetColumnSpan(stringLabel, 2);

		arrowImage = new Image
		{
			Aspect = Aspect.AspectFit,
			HeightRequest = 22,
			WidthRequest = 22,
			Source = "dtac_pass_arrow.png",
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center
		};
		arrowImage.SetBinding(Image.IsVisibleProperty, new Binding("IsArrowVisible", source: this));
		Grid.SetColumnSpan(arrowImage, 2);

		Add(timeLabel_HHMM);
		Add(timeLabel_SS);
		Add(stringLabel);
		Add(arrowImage);

		InputTransparent = true;

		OnIsPassChanged(IsPass);

		logger.Trace("Created");
	}

	partial void OnIsPassChanged(bool newValue)
	{
		if (newValue)
		{
			logger.Trace("newValue: {0} -> Color set to red", newValue);
			TextColor = Colors.Red;
		}
		else
		{
			logger.Trace("newValue: {0} -> Color set to default", newValue);
			DTACElementStyles.Instance.TimetableTextColor.Apply(this, TextColorPropertyKey.BindableProperty);
		}
	}

	partial void OnTimeDataChanged(TimeData? newValue)
	{
		string hhmm = "";
		IsStringVisible = false;
		IsArrowVisible = false;
		IsVisible = true;
		if (newValue is null)
		{
			logger.Trace("newValue is null -> IsVisible set to false");
			IsVisible = false;
		}
		else if (newValue.Hour is not null || newValue.Minute is not null || newValue.Second is not null)
		{
			logger.Trace("newValue: {0} (one of Hour/Minute/Second is null)", newValue);
			hhmm = string.Empty;
			if (newValue.Hour is not null)
				hhmm += $"{newValue.Hour % 24}.";

			hhmm += $"{newValue.Minute ?? 0:D02}";
		}
		else if (newValue.Text == "↓")
		{
			logger.Trace("newValue: {0} (Text is ↓) -> AllowVisible set to true", newValue);
			IsArrowVisible = true;
		}
		else
		{
			logger.Trace("newValue was not null and Time not set and Text was not ↓ -> IsStringVisible set to true");
			IsStringVisible = true;
		}

		HHMM = hhmm;

		logger.Info("HHMM: {0}, IsStringVisible: {1}({2}), IsArrowVisible: {3}",
			HHMM,
			IsStringVisible,
			newValue?.Text,
			IsArrowVisible
		);
	}
}
