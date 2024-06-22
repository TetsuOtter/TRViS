using DependencyPropertyGenerator;
using TRViS.IO.Models;

namespace TRViS.DTAC;

[DependencyProperty<TimeData>("TimeData")]
[DependencyProperty<bool>("IsPass")]
[DependencyProperty<bool>("IsArrowVisible", IsReadOnly = true)]
[DependencyProperty<bool>("IsStringVisible", IsReadOnly = true)]
[DependencyProperty<Color>("TextColor", IsReadOnly = true)]
[DependencyProperty<string>("HHMM", IsReadOnly = true)]
public partial class TimeCell : Grid
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

	public TimeCell()
	{
		logger.Trace("Creating...");

		InitializeComponent();

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
			DTACElementStyles.TimetableTextColor.Apply(this, TextColorPropertyKey.BindableProperty);
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
