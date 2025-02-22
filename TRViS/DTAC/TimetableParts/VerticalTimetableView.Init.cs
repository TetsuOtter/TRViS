using Microsoft.Maui.Controls.Shapes;

using TRViS.Controls;
using TRViS.DTAC.TimetableParts;
using TRViS.IO.Models;
using TRViS.Services;

namespace TRViS.DTAC;

public partial class VerticalTimetableView
{
	public static readonly Color CURRENT_LOCATION_MARKER_COLOR = new(0x00, 0x88, 0x00);
	BoxView CurrentLocationBoxView { get; } = new()
	{
		IsVisible = false,
		HeightRequest = RowHeight.Value,
		WidthRequest = DTACElementStyles.RUN_TIME_COLUMN_WIDTH,
		Margin = new(0),
		VerticalOptions = LayoutOptions.End,
		HorizontalOptions = LayoutOptions.Start,
		Color = CURRENT_LOCATION_MARKER_COLOR,
		InputTransparent = true,
		ZIndex = DTACElementStyles.TimetableRowLocationBoxZIndex,
	};
	BoxView CurrentLocationLine { get; } = new()
	{
		IsVisible = false,
		HeightRequest = 4,
		Margin = new(0, -2),
		VerticalOptions = LayoutOptions.End,
		Color = CURRENT_LOCATION_MARKER_COLOR,
		InputTransparent = true,
		ZIndex = DTACElementStyles.TimetableRowLocationBoxZIndex,
	};

	readonly AfterRemarks AfterRemarks;
	readonly BeforeDeparture_AfterArrive AfterArrive;
	readonly NextTrainButton NextTrainButton = new();

	readonly List<VerticalTimetableRow> RowViewList = new();

	public VerticalTimetableView()
	{
		logger.Trace("Creating...");

		AfterArrive = new(this, "着後");
		AfterRemarks = new(this);

		Grid.SetColumnSpan(NextTrainButton, 8);

		ColumnDefinitions = DTACElementStyles.TimetableColumnWidthCollection;

		Grid.SetColumnSpan(CurrentLocationLine, 8);

		LocationService.LocationStateChanged += LocationService_LocationStateChanged;
		LocationService.IsEnabledChanged += (_, e) => IsLocationServiceEnabled = e.NewValue;
		LocationService.ExceptionThrown += (s, e) =>
		{
			MainThread.BeginInvokeOnMainThread(() => Shell.Current.DisplayAlert("Location Service Error", e.ToString(), "OK"));
		};

		logger.Trace("Created");
	}

	Line TopSeparatorLine { get; } = DTACElementStyles.TimetableRowHorizontalSeparatorLineStyle();
	List<Line> SeparatorLines { get; } = new();
	Task AddSeparatorLines()
	{
		if (!MainThread.IsMainThread)
			return MainThread.InvokeOnMainThreadAsync(AddSeparatorLines);

		logger.Trace("MainThread: Insert Separator Lines");

		try
		{
			bool isChildrenCleared = !Children.Contains(TopSeparatorLine);
			int initialSeparatorLinesListLength = SeparatorLines.Count;
			for (int i = initialSeparatorLinesListLength; i < RowDefinitions.Count; i++)
			{
				SeparatorLines.Add(DTACElementStyles.TimetableRowHorizontalSeparatorLineStyle());
			}
			for (int i = initialSeparatorLinesListLength - 1; RowDefinitions.Count <= i; i--)
			{
				Line line = SeparatorLines[i];
				SeparatorLines.RemoveAt(i);
				Children.Remove(line);
			}

			if (isChildrenCleared)
			{
				TopSeparatorLine.VerticalOptions = LayoutOptions.Start;
				DTACElementStyles.AddHorizontalSeparatorLineStyle(this, TopSeparatorLine, 0);
				for (int i = 0; i < SeparatorLines.Count; i++)
				{
					DTACElementStyles.AddHorizontalSeparatorLineStyle(this, SeparatorLines[i], i);
				}
			}
			else
			{
				for (int i = initialSeparatorLinesListLength; i < RowDefinitions.Count; i++)
				{
					DTACElementStyles.AddHorizontalSeparatorLineStyle(this, SeparatorLines[i], i);
				}
			}

			logger.Trace("MainThread: Insert Separator Lines Complete");
			return Task.CompletedTask;
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.AddSeparatorLines");
			Utils.ExitWithAlert(ex);
			return Task.FromException(ex);
		}
	}

	int RowsCount = 0;
	async void SetRowViews(TrainData? trainData, TimetableRow[]? newValue)
	{
		logger.Info("Setting RowViews... (Current RowViewList.Count: {0})", RowViewList.Count);
		RowViewList.Clear();

		logger.Trace("Starting ClearOldRowViews Task...");
		await MainThread.InvokeOnMainThreadAsync(() =>
		{
			try
			{
				logger.Trace("MainThread: Clearing old RowViews...");
				IsBusy = true;

				Children.Clear();

				logger.Trace("MainThread: Clearing old RowViews Complete");

				Add(CurrentLocationBoxView);
				Add(CurrentLocationLine);

				logger.Trace("MainThread: Insert CurrentLocationMarker Complete");
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.SetRowViews");
				Utils.ExitWithAlert(ex);
			}
		});
		logger.Trace("ClearOldRowViews Task Complete");

		int newCount = newValue?.Length ?? 0;
		bool hasAfterArrive = trainData?.AfterArrive is not null;
		bool hasNextTrainButton = trainData?.NextTrainId is not null;
		logger.Debug("newCount: {0}, hasAfterArrive: {1}, hasNextTrainButton: {2}", newCount, hasAfterArrive, hasNextTrainButton);
		try
		{
			RowsCount = newCount;
			SetRowDefinitions(newCount, hasAfterArrive, hasNextTrainButton);

			await AddSeparatorLines();

			AfterRemarks.SetRow(newCount);
			AfterArrive.SetRow(newCount + 1);
			if (hasAfterArrive)
				Grid.SetRow(NextTrainButton, newCount + 2);
			else
				Grid.SetRow(NextTrainButton, newCount + 1);

			logger.Trace("Starting RowViewInit Task...");
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.SetRowViews (SetRowDefinitions etc failed)");
			await Utils.ExitWithAlert(ex);
		}

		await Task.Run(async () =>
		{
			try
			{
				logger.Trace("Task: Finding last Station Row...");
				int lastTimetableRowIndex = 0;
				for (int i = 0; i < newCount; i++)
				{
					if (newValue is not null && !newValue[i].IsInfoRow)
						lastTimetableRowIndex = i;
				}

				logger.Trace("Task: last Station row is {0}, so Adding new RowViews...", lastTimetableRowIndex);
				for (int i = 0; i < newCount; i++)
					await AddNewRow(newValue![i], i, i == lastTimetableRowIndex);
				logger.Trace("Task: RowViewInit Complete");
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.SetRowViews (AddNewRow failed)");
				await Utils.ExitWithAlert(ex);
			}
		});
		logger.Trace("RowViewInit Task Complete");

		logger.Trace("Starting FooterInsertion Task...");
		await MainThread.InvokeOnMainThreadAsync(() =>
		{
			try
			{
				logger.Trace("MainThread: Inserting Footer...");

				if (trainData?.AfterRemarks is not null)
				{
					AfterRemarks.Text = trainData.AfterRemarks;
					AfterRemarks.AddToParent();
				}

				if (trainData?.AfterArrive is not null)
				{
					AfterArrive.Text = trainData.AfterArrive;
					AfterArrive.Text_OnStationTrackColumn = trainData.AfterArriveOnStationTrackCol ?? string.Empty;
					AfterArrive.AddToParent();
				}

				if (trainData?.NextTrainId is not null)
				{
					NextTrainButton.NextTrainId = trainData.NextTrainId;
					this.Children.Add(NextTrainButton);
				}
				logger.Trace("NextTrainId: {0}", trainData?.NextTrainId);

				IsBusy = false;

				logger.Trace("MainThread: FooterInsertion Complete");
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.SetRowViews (FooterInsertion failed)");
				Utils.ExitWithAlert(ex);
			}
		});
		logger.Trace("FooterInsertion Task Complete");

		logger.Info("RowViews are set");
	}

	Task AddNewRow(TimetableRow? row, int index, bool isLastRow)
	{
		if (row is null)
		{
			logger.Trace("row is null -> skipping...");
			return Task.CompletedTask;
		}

		return MainThread.InvokeOnMainThreadAsync(() =>
		{
			logger.Debug("MainThread: Adding new Row (index: {0}, isLastRow: {1}, isInfoRow: {2}, Text: {3})",
				index,
				isLastRow,
				row.IsInfoRow,
				row.StationName
			);

			try
			{
				if (row.IsInfoRow)
				{
					HtmlAutoDetectLabel label = DTACElementStyles.LargeLabelStyle<HtmlAutoDetectLabel>();

					label.Text = row.StationName;

					Grid.SetColumn(label, 1);
					Grid.SetRow(label, index);
					Grid.SetColumnSpan(label, 3);
					Add(label);

					DTACElementStyles.AddTimetableRowHorizontalSeparatorLineStyle(this, index);
				}
				else
				{
					VerticalTimetableRow rowView = new(this, index, row, MarkerViewModel, isLastRow);

					TapGestureRecognizer tapGestureRecognizer = new();
					tapGestureRecognizer.Tapped += RowTapped;
					rowView.GestureRecognizers.Add(tapGestureRecognizer);

					RowViewList.Add(rowView);
				}
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.AddNewRow");
				Utils.ExitWithAlert(ex);
			}
		});
	}

	void SetRowDefinitions(int newCount, bool hasAfterArrive = false, bool hasNextTrainButton = false)
	{
		int currentCount = RowDefinitions.Count;
		logger.Debug("Count {0} -> {1}", currentCount, newCount);

		if (newCount < 0)
			throw new ArgumentOutOfRangeException(nameof(newCount), "count must be 0 or more");

		if (DeviceInfo.Current.Idiom == DeviceIdiom.Phone || DeviceInfo.Current.Idiom == DeviceIdiom.Unknown)
		{
			// AfterRemarks
			newCount += 1;
			if (hasAfterArrive)
				newCount += 1;
			if (hasNextTrainButton)
				newCount += 1;
		}
		else
		{
			int minCount = (int)Math.Floor(ScrollViewHeight / RowHeight.Value);
			int additionalRowsCount = Math.Max(2, (int)Math.Ceiling(ScrollViewHeight / RowHeight.Value) - 2);
			logger.Debug("additionalRowsCount: {0}", additionalRowsCount);

			newCount += additionalRowsCount;
			newCount = Math.Max(minCount, newCount);
		}

		HeightRequest = newCount * RowHeight.Value;
		logger.Debug("HeightRequest: {0}", HeightRequest);

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
	}

	partial void OnScrollViewHeightChanged(double newValue)
	{
		logger.Debug("ScrollViewHeight: {0}", newValue);

		if (DeviceInfo.Current.Idiom == DeviceIdiom.Phone || DeviceInfo.Current.Idiom == DeviceIdiom.Unknown)
			return;

		try
		{
			SetRowDefinitions(RowsCount);
			AddSeparatorLines();
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.OnScrollViewHeightChanged");
			Utils.ExitWithAlert(ex);
		}

		logger.Debug("RowDefinitions.Count changed to: {0}", RowDefinitions.Count);
	}
}
