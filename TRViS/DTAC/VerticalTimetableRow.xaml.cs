using TRViS.IO.Models;

namespace TRViS.DTAC;

public partial class VerticalTimetableRow : Grid
{
	public enum LocationStates
	{
		Undefined,
		AroundThisStation,
		RunningToNextStation,
	}

	VerticalTimetableRow.LocationStates _LocationState = LocationStates.Undefined;
	public VerticalTimetableRow.LocationStates LocationState
	{
		get => _LocationState;
		set
		{
			if (_LocationState == value)
				return;

			if (!IsEnabled || value == LocationStates.Undefined)
			{
				CurrentLocationBoxView.IsVisible = false;
				CurrentLocationLine.IsVisible = false;
				_LocationState = LocationStates.Undefined;
				return;
			}

			_LocationState = value;

			switch (value)
			{
				case LocationStates.AroundThisStation:
					CurrentLocationBoxView.IsVisible = true;
					CurrentLocationBoxView.Margin = new(0);
					CurrentLocationLine.IsVisible = false;
					break;

				case LocationStates.RunningToNextStation:
					CurrentLocationBoxView.IsVisible = true;
					CurrentLocationBoxView.Margin = new(0, -30);
					CurrentLocationLine.IsVisible = true;
					break;
			}
		}
	}

	public VerticalTimetableRow()
	{
		InitializeComponent();
	}

	public VerticalTimetableRow(TimetableRow rowData)
	{
		InitializeComponent();

		BindingContext = rowData;
	}
}
