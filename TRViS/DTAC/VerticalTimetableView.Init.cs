using Microsoft.Maui.Controls.Shapes;

using TRViS.Controls;
using TRViS.IO.Models;

namespace TRViS.DTAC;

public partial class VerticalTimetableView
{
	public static readonly Color CURRENT_LOCATION_MARKER_COLOR = new(0x00, 0x88, 0x00);
	BoxView CurrentLocationBoxView { get; } = new()
	{
		IsVisible = false,
		HeightRequest = RowHeight.Value / 2,
		WidthRequest = DTACElementStyles.RUN_TIME_COLUMN_WIDTH,
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

	readonly BeforeAfterRemarks AfterRemarks;
	readonly BeforeDeparture_AfterArrive AfterArrive;

	readonly List<VerticalTimetableRow> RowViewList = new();

	public VerticalTimetableView()
	{
		AfterArrive = new(this, "着後");
		AfterRemarks = new(this);

		ColumnDefinitions = DTACElementStyles.TimetableColumnWidthCollection;

		LocationService.IsNearbyChanged += LocationService_IsNearbyChanged;
	}

	async void SetRowViews(TrainData? trainData, TimetableRow[]? newValue)
	{
		RowViewList.Clear();
		await MainThread.InvokeOnMainThreadAsync(() =>
		{
			IsBusy = true;

			Children.Clear();
		});

		int newCount = newValue?.Length ?? 0;

		SetRowDefinitions(newCount);

		AfterRemarks.SetRow(newCount);
		AfterArrive.SetRow(newCount + 1);

		await Task.Run(async () =>
		{
			int lastTimetableRowIndex = 0;
			for (int i = 0; i < newCount; i++)
			{
				if (newValue is not null && !newValue[i].IsInfoRow)
					lastTimetableRowIndex = i;
			}

			for (int i = 0; i < newCount; i++)
				await AddNewRow(newValue![i], i, i == lastTimetableRowIndex);
		});

		await MainThread.InvokeOnMainThreadAsync(() =>
		{
			AfterRemarks.Text = trainData?.AfterRemarks ?? string.Empty;
			AfterArrive.Text = trainData?.AfterArrive ?? string.Empty;
			AfterArrive.Text_OnStationTrackColumn = trainData?.AfterArriveOnStationTrackCol ?? string.Empty;

			AfterRemarks.AddToParent();
			AfterArrive.AddToParent();

			Add(CurrentLocationBoxView);
			Add(CurrentLocationLine);
			IsBusy = false;
		});
	}

	async Task AddNewRow(TimetableRow? row, int index, bool isLastRow)
	{
		if (row is null)
			return;

		if (row.IsInfoRow)
		{
			await MainThread.InvokeOnMainThreadAsync(() =>
			{
				HtmlAutoDetectLabel label = DTACElementStyles.LargeLabelStyle<HtmlAutoDetectLabel>();
				Line line = DTACElementStyles.HorizontalSeparatorLineStyle();

				label.Text = row.StationName;

				Grid.SetColumnSpan(label, 3);

				this.Add(
					label,
					column: 1,
					row: index
				);
				this.Add(
					line,
					column: 0,
					row: index
				);
			});

			return;
		}

		VerticalTimetableRow rowView = await MainThread.InvokeOnMainThreadAsync(() => new VerticalTimetableRow(row, MarkerViewModel, isLastRow));

		TapGestureRecognizer tapGestureRecognizer = new();
		tapGestureRecognizer.Tapped += RowTapped;
		rowView.GestureRecognizers.Add(tapGestureRecognizer);

		await MainThread.InvokeOnMainThreadAsync(() =>
		{
			Grid.SetColumnSpan(rowView, 8);
			Children.Add(rowView);

			Grid.SetRow(rowView, index);
		});
		RowViewList.Add(rowView);
	}

	void SetRowDefinitions(int newCount)
	{
		int currentCount = RowDefinitions.Count;

		if (newCount < 0)
			throw new ArgumentOutOfRangeException(nameof(newCount), "count must be 0 or more");

		// After Remarks
		newCount += 1;

		RowDefinitions.Remove(AfterArrive.RowDefinition);

		HeightRequest = (newCount * RowHeight.Value) + AfterArrive.RowDefinition.Height.Value;

		if (newCount <= 0)
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

		RowDefinitions.Add(AfterArrive.RowDefinition);
	}
}
