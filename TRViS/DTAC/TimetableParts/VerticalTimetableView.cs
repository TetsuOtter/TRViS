using DependencyPropertyGenerator;

using TRViS.DTAC.Logic;
using TRViS.IO.Models;
using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS.DTAC;

[DependencyProperty<double>("ScrollViewHeight", DefaultValue = 0)]
public partial class VerticalTimetableView : Grid
{
	public class ScrollRequestedEventArgs(double PositionY) : EventArgs
	{
		public double PositionY { get; } = PositionY;
	}

	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	static public readonly GridLength RowHeight = new(60);

	public event EventHandler<ScrollRequestedEventArgs>? ScrollRequested;

	public DTACMarkerViewModel MarkerViewModel { get; } = InstanceManager.DTACMarkerViewModel;

	/// <summary>
	/// Initializes the timetable view with train data.
	/// Must be called when train data changes to set up rows and location service state.
	/// </summary>
	public void InitializeWithTrainData(TrainData? trainData)
	{
		if (trainData?.Rows == null)
		{
			logger.Debug("TrainData or Rows is null, skipping initialization");
			return;
		}

		try
		{
			logger.Info("InitializeWithTrainData: {0}", trainData.TrainNumber);

			// Initialize location service state with row count
			int rowCount = trainData.Rows.Length;
			TimetableLocationServiceFactory.InitializeTotalRows(LocationServiceState, rowCount);
			TimetableLocationServiceFactory.SetRowHeight(LocationServiceState, RowHeight.Value);

			// Set up rows in location service (Logic layer)
			InstanceManager.LocationService.SetTimetableRows(trainData.Rows);

			// Set up view rows (UI layer)
			// This is called on the main thread through SetRowViews
			MainThread.BeginInvokeOnMainThread(async () =>
			{
				await SetRowViewsAsync(trainData, trainData.Rows);
			});
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.InitializeWithTrainData");
			Utils.ExitWithAlert(ex);
		}
	}

	private void RowTapped(object? sender, EventArgs e)
	{
		if (sender is not BoxView boxView || boxView.BindingContext is not VerticalTimetableRow row)
			return;

		try
		{
			logger.Trace("Row {0} tapped", row.RowIndex);

			// Check if location service is enabled from row state
			// TODO: Get IsLocationServiceEnabled and IsRunStarted from parent PageState instead
			// For now, we skip the validation that requires these properties

			// Location service is disabled - allow manual selection
			logger.Info("Tapped {0} -> advance location state", row.RowIndex);
			TimetableLocationServiceFactory.AdvanceLocationState(LocationServiceState, LocationServiceState.CurrentRunningRow);
			RefreshUIFromState();
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.RowTapped");
			Utils.ExitWithAlert(ex);
		}
	}
}
