using System.Collections.Specialized;
using System.ComponentModel;
using DependencyPropertyGenerator;

using TRViS.IO.Models;

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
				await AddNewRow(newValue![i], i);
		});

		await MainThread.InvokeOnMainThreadAsync(() =>
		{
			IsBusy = false;
		});
	}

	async Task AddNewRow(TimetableRow? row, int index)
	{
		if (row is null)
			return;

		VerticalTimetableRow rowView = await MainThread.InvokeOnMainThreadAsync(() => new VerticalTimetableRow(row));

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

				if (value.LocationState != VerticalTimetableRow.LocationStates.Undefined)
				{
					int rowCount = Grid.GetRow(value);

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
