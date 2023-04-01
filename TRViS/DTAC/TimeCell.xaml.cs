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
	public TimeCell()
	{
		InitializeComponent();

		InputTransparent = true;

		OnIsPassChanged(IsPass);
	}

	partial void OnIsPassChanged(bool newValue)
		=> TextColor = newValue ? Colors.Red : Colors.Black;

	partial void OnTimeDataChanged(TimeData? newValue)
	{
		string hhmm = "";
		IsStringVisible = false;
		IsArrowVisible = false;
		IsVisible = true;
		if (newValue is null)
			IsVisible = false;
		else if (newValue.Hour is not null || newValue.Minute is not null || newValue.Second is not null)
		{
			hhmm = string.Empty;
			if (newValue.Hour is not null)
				hhmm += $"{newValue.Hour % 24}.";

			hhmm += $"{newValue.Minute ?? 0:D02}";
		}
		else if (newValue.Text == "↓")
			IsArrowVisible = true;
		else
			IsStringVisible = true;

		HHMM = hhmm;
	}
}
