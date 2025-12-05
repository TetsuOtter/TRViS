using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

using TRViS.IO.Models;
using TRViS.DTAC.TimetableParts;
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
	public partial VerticalTimetableRow? CurrentRunningRow { get; set; } = null;

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
	(VerticalTimetableRow row, DateTime time)? _lastTapInfo = null;
	private VerticalTimetableRow? _previousCurrentRunningRow;

	partial void OnIsLocationServiceEnabledChanged(bool value)
	{
		LocationService.IsEnabled = value;
	}

	static bool IsHapticEnabled { get; set; } = true;

	public void SetCurrentRunningRow(int index, VerticalTimetableRow? value)
	{
		if (LocationMarkerPosition == index || CurrentRunningRow == value)
		{
			return;
		}

		MainThread.BeginInvokeOnMainThread(() =>
		{
			try
			{
				_previousCurrentRunningRow = CurrentRunningRow;
				if (_previousCurrentRunningRow is not null)
				{
					_previousCurrentRunningRow.Model.IsLocationMarkerOnThisRow = false;
					LocationMarkerState = VerticalTimetableRowModel.LocationStates.Undefined;
				}

				CurrentRunningRow = value;

				if (value is not null)
				{
					LocationMarkerPosition = index;
				}
				else
				{
					LocationMarkerPosition = -1;
				}
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableViewModel.SetCurrentRunningRow");
				Utils.ExitWithAlert(ex);
			}
		});
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
		CurrentRunningRow = null;

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

		// Clear previous row's marker state
		_previousCurrentRunningRow = CurrentRunningRow;
		if (_previousCurrentRunningRow is not null)
		{
			_previousCurrentRunningRow.Model.IsLocationMarkerOnThisRow = false;
		}

		// Set new row's marker state
		LocationMarkerState = e.IsRunningToNextStation
			? VerticalTimetableRowModel.LocationStates.RunningToNextStation
			: VerticalTimetableRowModel.LocationStates.AroundThisStation;
		LocationMarkerPosition = e.NewStationIndex;
		// CurrentRunningRow is set by the view
	}

	/// <summary>
	/// Handles row tap event with double tap detection - cycles through location marker states
	/// </summary>
	public void HandleRowTappedWithDoubleTapDetection(VerticalTimetableRow row, int rowViewListCount)
	{
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
			InstanceManager.LocationService.ForceSetLocationInfo(row.Model.RowIndex, false);
			return;
		}

		// Handle row tap through ViewModel
		HandleRowTapped(row, rowViewListCount);
	}

	/// <summary>
	/// Handles row tap event - cycles through location marker states
	/// </summary>
	public void HandleRowTapped(VerticalTimetableRow? row, int rowViewListCount)
	{
		if (row is null)
			return;

		// If tapped a different row, set CurrentRunningRow
		if (CurrentRunningRow != row)
		{
			CurrentRunningRow = row;
			LocationMarkerState = VerticalTimetableRowModel.LocationStates.AroundThisStation;
			return;
		}

		// Cycle through location states for the same row
		switch (LocationMarkerState)
		{
			case VerticalTimetableRowModel.LocationStates.Undefined:
				CurrentRunningRow = row;
				LocationMarkerState = VerticalTimetableRowModel.LocationStates.AroundThisStation;
				break;
			case VerticalTimetableRowModel.LocationStates.AroundThisStation:
				// Don't transition to RunningToNextStation if it's the last row
				if (row.Model.RowIndex != rowViewListCount - 1)
				{
					LocationMarkerState = VerticalTimetableRowModel.LocationStates.RunningToNextStation;
				}
				break;
			case VerticalTimetableRowModel.LocationStates.RunningToNextStation:
				LocationMarkerState = VerticalTimetableRowModel.LocationStates.AroundThisStation;
				break;
		}
	}

	/// <summary>
	/// Sets CurrentRunningRow from LocationMarkerPosition
	/// </summary>
	public void SetCurrentRunningRowFromLocationMarkerPosition(List<VerticalTimetableRow> rowViewList)
	{
		// Clear previous row's marker state
		_previousCurrentRunningRow?.Model.IsLocationMarkerOnThisRow = false;

		// Set new row's marker state
		if (LocationMarkerPosition >= 0 && LocationMarkerPosition < rowViewList.Count)
		{
			_previousCurrentRunningRow = CurrentRunningRow;
			CurrentRunningRow = rowViewList[LocationMarkerPosition];
			CurrentRunningRow.Model.IsLocationMarkerOnThisRow = true;
		}
		else
		{
			_previousCurrentRunningRow = CurrentRunningRow;
			CurrentRunningRow = null;
		}
	}
}
