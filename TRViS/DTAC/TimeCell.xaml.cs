using DependencyPropertyGenerator;
using TRViS.Models;

namespace TRViS.DTAC;

[DependencyProperty<TimeData>("TimeData")]
[DependencyProperty<Style>("DefaultLabelStyle")]
[DependencyProperty<Style>("SSLabelStyle")]
[DependencyProperty<Color>("TextColor")]
[DependencyProperty<string>("HHMM", IsReadOnly = true)]
public partial class TimeCell : Grid
{
	public TimeCell()
	{
		InitializeComponent();
	}

	partial void OnTimeDataChanged(TimeData? newValue)
	{
		string hhmm = "";
		if (newValue is null)
		{
			IsVisible = false;
		}
		else
		{
			IsVisible = true;

			hhmm = $"{newValue.Hour}";
			if (newValue.Hour is not null)
				hhmm += ".";

			hhmm += $"{newValue.Minute ?? 0:D02}";
		}

		HHMM = hhmm;
	}
}
