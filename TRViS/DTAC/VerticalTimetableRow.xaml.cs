using System.ComponentModel;
using DependencyPropertyGenerator;
using TRViS.IO.Models;
using TRViS.ViewModels;

namespace TRViS.DTAC;

[DependencyProperty<bool>("IsMarkingMode", IsReadOnly = true)]
[DependencyProperty<Color>("MarkedColor", IsReadOnly = true)]
[DependencyProperty<string>("MarkedText", IsReadOnly = true)]
[DependencyProperty<DTACMarkerViewModel>("MarkerViewModel", IsReadOnly = true)]
public partial class VerticalTimetableRow : Grid
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

		CurrentLocationBoxView.IsVisible = false;
		CurrentLocationLine.IsVisible = false;

		MarkedColor = DefaultMarkButtonColor;
		IsMarkingMode = false;
	}

	public VerticalTimetableRow(TimetableRow rowData, DTACMarkerViewModel? markerViewModel = null) : this()
	{
		BindingContext = rowData;

		MarkerViewModel = markerViewModel;
	}

	void MarkerBoxClicked(object sender, EventArgs e)
	{
		if (MarkerViewModel is null)
			return;

		if (MarkedColor == DefaultMarkButtonColor)
		{
			MarkedColor = MarkerViewModel.SelectedColor;
			MarkedText = MarkerViewModel.SelectedText;
		}
		else
		{
			MarkedColor = DefaultMarkButtonColor;
			MarkedText = null;
		}
	}

	partial void OnMarkerViewModelChanged(DTACMarkerViewModel? oldValue, DTACMarkerViewModel? newValue)
	{
		if (oldValue is not null)
			oldValue.PropertyChanged -= OnMarkerViewModelValueChanged;

		if (newValue is not null)
			newValue.PropertyChanged += OnMarkerViewModelValueChanged;
	}

	private void OnMarkerViewModelValueChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is not DTACMarkerViewModel vm)
			return;

		if (e.PropertyName == nameof(DTACMarkerViewModel.IsToggled))
			IsMarkingMode = vm.IsToggled;
	}

	partial void OnMarkedColorChanged(Color? newValue)
	{
		BackgroundColor = newValue == DefaultMarkButtonColor ? null : newValue?.WithAlpha(BG_ALPHA);
	}

	partial void OnIsMarkingModeChanged(bool newValue)
	{
		MarkerBox.IsVisible = newValue || (MarkedColor != DefaultMarkButtonColor);
	}
}
