using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

using TRViS.IO.Models;
using TRViS.Services;
using NLog;

namespace TRViS.DTAC.ViewModels;

public partial class VerticalTimetableViewModel : ObservableObject
{
	private static readonly Logger logger = LoggerService.GetGeneralLogger();

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

	partial void OnIsMarkingModeChanged(bool value)
	{
		foreach (var row in CurrentRows)
		{
			row.IsMarkingMode = value;
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
}
