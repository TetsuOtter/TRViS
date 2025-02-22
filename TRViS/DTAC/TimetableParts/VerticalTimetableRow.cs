using System.ComponentModel;

using TRViS.ViewModels;

namespace TRViS.DTAC;

public partial class VerticalTimetableRow
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
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
			try
			{
				if (_LocationState == value)
				{
					logger.Trace("LocationState is already {0}, so skipping...", value);
					return;
				}

				if (!IsEnabled || value == LocationStates.Undefined)
				{
					logger.Info("IsEnabled is false or newValue is Undefined -> set LocationState to Undefined");
					_LocationState = LocationStates.Undefined;
					DTACElementStyles.TimetableTextColor.Apply(DriveTimeMM, Label.TextColorProperty);
					DTACElementStyles.TimetableTextColor.Apply(DriveTimeSS, Label.TextColorProperty);
					return;
				}

				DTACElementStyles.TimetableTextInvColor.Apply(DriveTimeMM, Label.TextColorProperty);
				DTACElementStyles.TimetableTextInvColor.Apply(DriveTimeSS, Label.TextColorProperty);

				// 最終行の場合は、次の駅に進まないようにする。
				if (IsLastRow && value == LocationStates.RunningToNextStation)
				{
					logger.Info("IsLastRow is true and newValue is RunningToNextStation -> do not change LocationState");
					return;
				}

				logger.Info("LocationState is changed to {0}", value);

				_LocationState = value;
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableRow.LocationState");
				Utils.ExitWithAlert(ex);
			}
		}
	}

	public bool IsLastRow { get; }

	void MarkerBoxClicked(object? sender, EventArgs e)
	{
		if (MarkerViewModel is null || !IsMarkingMode)
			return;

		if (MarkedColor is null)
		{
			logger.Info("MarkedColor is null -> marker is not set -> set it (color: {0}, text: {1})",
				MarkerViewModel.SelectedColor,
				MarkerViewModel.SelectedText
			);
			MarkedColor = MarkerViewModel.SelectedColor;
			MarkerBox.Text = MarkerViewModel.SelectedText;
		}
		else
		{
			logger.Info("MarkedColor is not null -> marker is already set -> clear it (current color: {0}, text: {1})",
				MarkedColor,
				MarkerBox.Text
			);
			MarkedColor = null;
			MarkerBox.Text = null;
		}
	}

	private void OnMarkerViewModelValueChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is not DTACMarkerViewModel vm)
			return;

		if (e.PropertyName == nameof(DTACMarkerViewModel.IsToggled))
		{
			logger.Trace("MarkerViewModel.IsToggled is changed to {0}", vm.IsToggled);
			IsMarkingMode = vm.IsToggled;
		}
	}
}
