using System.Collections.Specialized;
using System.ComponentModel;
using DependencyPropertyGenerator;

using TRViS.IO.Models;
using TRViS.ViewModels;

namespace TRViS.DTAC;

[DependencyProperty<bool>("IsBusy")]
[DependencyProperty<bool>("IsRunStarted")]
[DependencyProperty<TrainData>("SelectedTrainData")]
public partial class VerticalTimetableView : Grid
{
	public class ScrollRequestedEventArgs : EventArgs
	{
		public double PositionY { get; }

		public ScrollRequestedEventArgs(double PositionY)
		{
			this.PositionY = PositionY;
		}
	}

	static readonly GridLength RowHeight = new(60);

	public event EventHandler? IsBusyChanged;

	public event EventHandler<ScrollRequestedEventArgs>? ScrollRequested;

	public DTACMarkerViewModel MarkerViewModel { get; init; } = new();

	public static readonly Color CURRENT_LOCATION_MARKER_COLOR = new(0x00, 0x88, 0x00);
	BoxView CurrentLocationBoxView { get; } = new()
	{
		IsVisible = false,
		HeightRequest = RowHeight.Value / 2,
		WidthRequest = VerticalStylePage.RUN_TIME_COLUMN_WIDTH,
		Margin = new(0),
		VerticalOptions = LayoutOptions.Start,
		HorizontalOptions = LayoutOptions.Start,
		Color = CURRENT_LOCATION_MARKER_COLOR,
	};
	BoxView CurrentLocationLine { get; } = new()
	{
		IsVisible = false,
		HeightRequest = 4,
		Margin = new(0, -2),
		VerticalOptions = LayoutOptions.End,
		Color = CURRENT_LOCATION_MARKER_COLOR,
	};

	partial void OnSelectedTrainDataChanged(TrainData? newValue)
	{
		SetRowViews(newValue?.Rows);
	}

	partial void OnIsBusyChanged()
		=> IsBusyChanged?.Invoke(this, new());

	async void SetRowViews(TimetableRow[]? newValue)
	{
		await MainThread.InvokeOnMainThreadAsync(() =>
		{
			IsBusy = true;

			Children.Clear();
		});

		int newCount = newValue?.Length ?? 0;

		SetRowDefinitions(newCount);

		await Task.Run(async () =>
		{
			for (int i = 0; i < newCount; i++)
				await AddNewRow(newValue![i], i, i == (newCount - 1));
		});

		await MainThread.InvokeOnMainThreadAsync(() =>
		{
			Add(CurrentLocationBoxView);
			Add(CurrentLocationLine);
			IsBusy = false;
		});
	}

	async Task AddNewRow(TimetableRow? row, int index, bool isLastRow)
	{
		if (row is null)
			return;

		VerticalTimetableRow rowView = await MainThread.InvokeOnMainThreadAsync(() => new VerticalTimetableRow(row, MarkerViewModel, isLastRow));

		TapGestureRecognizer tapGestureRecognizer = new();
		tapGestureRecognizer.Tapped += RowTapped;
		rowView.GestureRecognizers.Add(tapGestureRecognizer);

		await MainThread.InvokeOnMainThreadAsync(() =>
		{
			Children.Add(rowView);

			Grid.SetRow(rowView, index);
		});
	}

	VerticalTimetableRow? _CurrentRunningRow = null;
	VerticalTimetableRow? CurrentRunningRow
	{
		get => _CurrentRunningRow;
		set
		{
			if (_CurrentRunningRow == value)
				return;

			if (_CurrentRunningRow is not null)
				_CurrentRunningRow.LocationState = VerticalTimetableRow.LocationStates.Undefined;

			_CurrentRunningRow = value;

			if (value is not null)
			{
				value.LocationState = VerticalTimetableRow.LocationStates.AroundThisStation;

				int rowCount = Grid.GetRow(value);

				Grid.SetRow(CurrentLocationBoxView, rowCount + 1);
				Grid.SetRow(CurrentLocationLine, rowCount);

				CurrentLocationBoxView.IsVisible = false;
				CurrentLocationLine.IsVisible = false;

				if (value.LocationState != VerticalTimetableRow.LocationStates.Undefined)
				{
					ScrollRequested?.Invoke(this, new(Math.Max(rowCount - 1, 0) * RowHeight.Value));
				}
			}
		}
	}

	private void RowTapped(object? sender, EventArgs e)
	{
		if (sender is not VerticalTimetableRow row)
			return;

		if (!IsRunStarted || !IsEnabled)
			return;

		switch (row.LocationState)
		{
			case VerticalTimetableRow.LocationStates.Undefined:
				CurrentRunningRow = row;
				break;
			case VerticalTimetableRow.LocationStates.AroundThisStation:
				row.LocationState = VerticalTimetableRow.LocationStates.RunningToNextStation;
				break;
			case VerticalTimetableRow.LocationStates.RunningToNextStation:
				row.LocationState = VerticalTimetableRow.LocationStates.AroundThisStation;
				break;
		}

		CurrentLocationBoxView.IsVisible = CurrentLocationLine.IsVisible
			= row.LocationState == VerticalTimetableRow.LocationStates.RunningToNextStation;
	}

	partial void OnIsRunStartedChanged(bool newValue)
	{
		CurrentRunningRow = newValue ? Children.FirstOrDefault(v => v is VerticalTimetableRow) as VerticalTimetableRow : null;
	}

	void SetRowDefinitions(int? newCount)
	{
		int currentCount = RowDefinitions.Count;
		HeightRequest = newCount * RowHeight.Value ?? 0;

		if (newCount is null || newCount <= 0)
			RowDefinitions.Clear();
		else if (currentCount < newCount)
		{
			for (int i = RowDefinitions.Count; i < newCount; i++)
				RowDefinitions.Add(new(RowHeight));
		}
		else if (newCount < currentCount)
		{
			for (int i = RowDefinitions.Count - 1; i >= newCount; i--)
				RowDefinitions.RemoveAt(i);
		}
	}
}
