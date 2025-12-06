using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

using TRViS.IO.Models;
using TRViS.Services;
using NLog;

namespace TRViS.DTAC.ViewModels;

public partial class VerticalTimetableViewModel : ObservableObject
{
	private static readonly Logger logger = LoggerService.GetGeneralLogger();
	readonly LocationService LocationService = InstanceManager.LocationService;

	public VerticalTimetableViewModel()
	{
		LocationService.LocationStateChanged += OnLocationStateChanged;
	}

	public static readonly GridLength RowHeight = new(60);

	[ObservableProperty]
	public partial ObservableCollection<VerticalTimetableRowModel> CurrentRows { get; set; } = [];

	[ObservableProperty]
	public partial VerticalTimetableRowModel.LocationStates LocationMarkerState { get; set; } = VerticalTimetableRowModel.LocationStates.Undefined;

	[ObservableProperty]
	public partial int LocationMarkerPosition { get; set; } = -1;

	[ObservableProperty]
	public partial bool IsMarkingMode { get; set; } = false;

	[ObservableProperty]
	public partial bool IsRunStarted { get; set; } = false;

	[ObservableProperty]
	public partial string? AfterRemarksText { get; set; } = null;

	[ObservableProperty]
	public partial string? AfterArriveText { get; set; } = null;

	[ObservableProperty]
	public partial string? NextTrainId { get; set; } = null;

	[ObservableProperty]
	public partial bool IsLocationServiceEnabled { get; set; } = false;

	const double DOUBLE_TAP_DETECT_MS = 500;
	(VerticalTimetableRowModel row, DateTime time)? _lastTapInfo = null;

	partial void OnIsLocationServiceEnabledChanged(bool value)
	{
		LocationService.IsEnabled = value;
	}

	partial void OnIsMarkingModeChanged(bool value)
	{
		foreach (var row in CurrentRows)
		{
			row.IsMarkingMode = value;
		}
	}

	partial void OnIsRunStartedChanged(bool value)
	{
		if (!value)
		{
			logger.Info("IsRunStarted is changed to false -> reset location marker state");
			LocationMarkerPosition = -1;
			LocationMarkerState = VerticalTimetableRowModel.LocationStates.Undefined;
		}
		else if (LocationMarkerPosition < 0)
		{
			logger.Info("IsRunStarted is changed to true -> set LocationMarkerPosition to first row");
			LocationMarkerPosition = 0;
			LocationMarkerState = VerticalTimetableRowModel.LocationStates.AroundThisStation;
		}
		else
		{
			logger.Info("IsRunStarted is changed to true and LocationMarkerPosition is already set -> keep current position {0}", LocationMarkerPosition);
		}
	}

	partial void OnLocationMarkerPositionChanged(int value)
	{
		for (int i = 0; i < CurrentRows.Count; i++)
		{
			CurrentRows[i].IsLocationMarkerOnThisRow = (i == value);
		}
	}

	partial void OnLocationMarkerStateChanged(VerticalTimetableRowModel.LocationStates value)
	{
		if (value == VerticalTimetableRowModel.LocationStates.Undefined)
		{
			LocationMarkerPosition = -1;
		}
	}

	/// <summary>
	/// Updates timetable view with train data - extracts only display-necessary information
	/// </summary>
	public void SetTrainData(TrainData? trainData)
	{
		// Extract and set display information
		CurrentRows = new ObservableCollection<VerticalTimetableRowModel>(
			(trainData?.Rows ?? []).Select((row, index) => new VerticalTimetableRowModel
			{
				RowIndex = index,
				IsInfoRow = row.IsInfoRow,
				InfoText = row.IsInfoRow ? row.StationName : null,
				IsMarkingMode = IsMarkingMode,
				DriveTimeMM = row.DriveTimeMM?.ToString(),
				DriveTimeSS = row.DriveTimeSS?.ToString(),
				StationName = row.StationName,
				IsPass = row.IsPass,
				ArrivalTime = row.ArriveTime,
				HasBracket = row.HasBracket,
				DepartureTime = row.DepartureTime,
				IsLastStop = row.IsLastStop,
				IsOperationOnlyStop = row.IsOperationOnlyStop,
				TrackName = row.TrackName,
				RunInLimit = row.RunInLimit?.ToString(),
				RunOutLimit = row.RunOutLimit?.ToString(),
				Remarks = row.Remarks,
				MarkerColor = row.DefaultMarkerColor_RGB is not null ? Color.FromRgb((byte)((row.DefaultMarkerColor_RGB >> 16) & 0xFF), (byte)((row.DefaultMarkerColor_RGB >> 8) & 0xFF), (byte)(row.DefaultMarkerColor_RGB & 0xFF)) : null,
				MarkerText = row.DefaultMarkerText,
			})
		);
		AfterRemarksText = trainData?.AfterRemarks;
		AfterArriveText = trainData?.AfterArrive;
		NextTrainId = trainData?.NextTrainId;

		// Reset location marker state
		LocationMarkerPosition = -1;
		LocationMarkerState = VerticalTimetableRowModel.LocationStates.Undefined;

		// Reset run started state
		IsRunStarted = false;
	}

	private void OnLocationStateChanged(object? sender, LocationStateChangedEventArgs e)
	{
		if (!IsLocationServiceEnabled)
		{
			return;
		}
		if (e.NewStationIndex < 0)
		{
			IsLocationServiceEnabled = false;
			return;
		}
		if (CurrentRows.Count <= e.NewStationIndex)
		{
			IsLocationServiceEnabled = false;
			return;
		}

		LocationMarkerState = e.IsRunningToNextStation
			? VerticalTimetableRowModel.LocationStates.RunningToNextStation
			: VerticalTimetableRowModel.LocationStates.AroundThisStation;
		LocationMarkerPosition = e.NewStationIndex;
	}

	/// <summary>
	/// Handles row tap event with double tap detection - cycles through location marker states
	/// </summary>
	public void HandleRowTappedWithDoubleTapDetection(VerticalTimetableRowModel row, int rowViewListCount)
	{
		if (!IsRunStarted || row.IsInfoRow)
			return;
		if (IsLocationServiceEnabled)
		{
			DateTime dateTimeNow = DateTime.Now;
			if (_lastTapInfo is null
				|| _lastTapInfo.Value.row != row
				|| dateTimeNow.AddMilliseconds(DOUBLE_TAP_DETECT_MS) < _lastTapInfo.Value.time)
			{
				_lastTapInfo = (row, dateTimeNow);
				return;
			}
		}

		_lastTapInfo = null;
		if (IsLocationServiceEnabled)
		{
			InstanceManager.LocationService.ForceSetLocationInfo(row.RowIndex, false);
			return;
		}

		HandleRowTapped(row, rowViewListCount);
	}

	/// <summary>
	/// Handles row tap event - cycles through location marker states
	/// </summary>
	private void HandleRowTapped(VerticalTimetableRowModel row, int rowViewListCount)
	{
		if (row.IsInfoRow)
			return;

		// Cycle through location states
		switch (LocationMarkerState)
		{
			case VerticalTimetableRowModel.LocationStates.Undefined:
				LocationMarkerPosition = row.RowIndex;
				LocationMarkerState = VerticalTimetableRowModel.LocationStates.AroundThisStation;
				break;
			case VerticalTimetableRowModel.LocationStates.AroundThisStation:
				// Only transition to RunningToNextStation if tapping the same row and it's not the last row
				if (LocationMarkerPosition == row.RowIndex && row.RowIndex != rowViewListCount - 1)
				{
					LocationMarkerState = VerticalTimetableRowModel.LocationStates.RunningToNextStation;
				}
				else if (LocationMarkerPosition != row.RowIndex)
				{
					LocationMarkerPosition = row.RowIndex;
				}
				break;
			case VerticalTimetableRowModel.LocationStates.RunningToNextStation:
				if (LocationMarkerPosition == row.RowIndex)
				{
					LocationMarkerState = VerticalTimetableRowModel.LocationStates.AroundThisStation;
				}
				else
				{
					LocationMarkerPosition = row.RowIndex;
					LocationMarkerState = VerticalTimetableRowModel.LocationStates.AroundThisStation;
				}
				break;
		}
	}
}
