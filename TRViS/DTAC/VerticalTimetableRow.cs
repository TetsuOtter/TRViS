using System.ComponentModel;
using TRViS.ViewModels;

namespace TRViS.DTAC;

public partial class VerticalTimetableRow
{
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
				DTACElementStyles.TimetableTextColor.Apply(DriveTimeMM, Label.TextColorProperty);
				DTACElementStyles.TimetableTextColor.Apply(DriveTimeSS, Label.TextColorProperty);
				return;
			}

			DTACElementStyles.TimetableTextInvColor.Apply(DriveTimeMM, Label.TextColorProperty);
			DTACElementStyles.TimetableTextInvColor.Apply(DriveTimeSS, Label.TextColorProperty);

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
			MarkedColor = null;
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
