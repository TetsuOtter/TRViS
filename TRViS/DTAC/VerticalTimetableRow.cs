using System.ComponentModel;
using TRViS.ViewModels;

namespace TRViS.DTAC;

public partial class VerticalTimetableRow
{
	static readonly Color DefaultMarkButtonColor = new(0xFA, 0xFA, 0xFA);
	const float BG_ALPHA = 0.3f;

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
				_LocationState = LocationStates.Undefined;
				if (DriveTimeMM is not null)
					DriveTimeMM.TextColor = Colors.Black;
				if (DriveTimeSS is not null)
					DriveTimeSS.TextColor = Colors.Black;
				return;
			}

			if (DriveTimeMM is not null)
				DriveTimeMM.TextColor = Colors.White;
			if (DriveTimeSS is not null)
				DriveTimeSS.TextColor = Colors.White;

			// 最終行の場合は、次の駅に進まないようにする。
			if (IsLastRow && value == LocationStates.RunningToNextStation)
				return;

			_LocationState = value;
		}
	}

	public bool IsLastRow { get; }

	void MarkerBoxClicked(object? sender, EventArgs e)
	{
		if (MarkerViewModel is null)
			return;

		if (MarkedColor is null)
		{
			MarkedColor = MarkerViewModel.SelectedColor;
			MarkerBox.Text = MarkerViewModel.SelectedText;
		}
		else
		{
			MarkedColor = DefaultMarkButtonColor;
			MarkerBox.Text = null;
		}
	}

	private void OnMarkerViewModelValueChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is not DTACMarkerViewModel vm)
			return;

		if (e.PropertyName == nameof(DTACMarkerViewModel.IsToggled))
			IsMarkingMode = vm.IsToggled;
	}
}
