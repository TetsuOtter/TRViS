using System.Collections.Specialized;
using System.ComponentModel;
using DependencyPropertyGenerator;

using TRViS.IO.Models;

namespace TRViS.DTAC;

[DependencyProperty<bool>("IsBusy")]
[DependencyProperty<TrainData>("SelectedTrainData")]
public partial class VerticalTimetableView : Grid
{
	static readonly GridLength RowHeight = new(60);

	public event EventHandler? IsBusyChanged;

	partial void OnSelectedTrainDataChanged(TrainData? newValue)
	{
		SetRowViews(newValue?.Rows);
	}

	partial void OnIsBusyChanged()
		=> IsBusyChanged?.Invoke(this, new());

	void SetRowViews(TimetableRow[]? newValue)
	{
		IsBusy = true;
		Children.Clear();

		int? newCount = newValue?.Length;

		SetRowDefinitions(newCount);

		if (newCount is null || newCount <= 0)
			return;


		int i = 0;
		foreach (var rowData in newValue!)
		{
			VerticalTimetableRow rowView = new()
			{
				BindingContext = rowData
			};

			Children.Add(rowView);

			Grid.SetRow(rowView, i);

			i++;
		}

		IsBusy = false;
	}

	void SetRowDefinitions(int? newCount)
	{
		int currentCount = RowDefinitions.Count;
		HeightRequest = newCount * RowHeight.Value ?? 0;

		if (newCount is null)
			RowDefinitions.Clear();
		else if (currentCount < newCount)
		{
			for (int i = RowDefinitions.Count; i < newCount; i++)
				RowDefinitions.Add(new(RowHeight));
		}
		else if (newCount < currentCount)
		{
			for (int i = RowDefinitions.Count - 1; i >= newCount; i++)
				RowDefinitions.RemoveAt(i);
		}
	}
}
