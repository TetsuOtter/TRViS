using CommunityToolkit.Mvvm.ComponentModel;

using TRViS.Controls;
using TRViS.IO.Models;
using TRViS.ViewModels;

namespace TRViS.Services;

public class ExceptionThrownEventArgs : EventArgs
{
	public Exception Exception { get; }

	public ExceptionThrownEventArgs(Exception ex)
	{
		this.Exception = ex;
	}
}

public partial class LocationService : ObservableObject, IDisposable
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

	[ObservableProperty]
	bool _IsEnabled;
	public event EventHandler<ValueChangedEventArgs<bool>>? IsEnabledChanged;

	public event EventHandler<bool>? CanUseServiceChanged
	{
		add => LonLatLocationService.CanUseServiceChanged += value;
		remove => LonLatLocationService.CanUseServiceChanged -= value;
	}

	readonly LonLatLocationService LonLatLocationService;

	public event EventHandler<LocationStateChangedEventArgs> LocationStateChanged
	{
		add => LonLatLocationService.LocationStateChanged += value;
		remove => LonLatLocationService.LocationStateChanged -= value;
	}

	public const double DefaultNearbyRadius_m = 200;

	public event EventHandler<Exception>? ExceptionThrown;

	CancellationTokenSource? gpsCancelation;

	public LocationService()
	{
		logger.Trace("Creating...");

		IsEnabled = false;
		LonLatLocationService = new();

		LocationStateChanged += (sender, e) =>
		{
			StaLocationInfo? newStaLocationInfo = LonLatLocationService.StaLocationInfo?.ElementAtOrDefault(e.NewStationIndex);
			LogView.Add($"LocationStateChanged: Station[{e.NewStationIndex}]@({newStaLocationInfo?.Location_lon_deg}, {newStaLocationInfo?.Location_lat_deg} & Radius:{newStaLocationInfo?.NearbyRadius_m}) IsRunningToNextStation:{e.IsRunningToNextStation}");
		};

		logger.Debug("LocationService is created");
	}

	partial void OnIsEnabledChanged(bool value)
	{
		// GPS停止
		if (!value)
		{
			logger.Info("IsEnabled is changed to false -> stop GPS");
			gpsCancelation?.Cancel();
		}
		else
		{
			logger.Info("IsEnabled is changed to true -> start GPS");
			Task.Run(StartGPS);
		}
		IsEnabledChanged?.Invoke(this, new ValueChangedEventArgs<bool>(!value, value));
	}

	public void SetTimetableRows(TimetableRow[]? timetableRows)
	{
		logger.Trace("Setting TimetableRows...");

		IsEnabled = false;
		LonLatLocationService.StaLocationInfo = timetableRows?.Where(v => !v.IsInfoRow).Select(v => new StaLocationInfo(v.Location.Location_m, v.Location.Longitude_deg, v.Location.Latitude_deg, v.Location.OnStationDetectRadius_m)).ToArray();
	}

	static EasterEggPageViewModel EasterEggPageViewModel { get; } = InstanceManager.EasterEggPageViewModel;
	static double lastInterval = 1;
	static TimeSpan lastIntervalTimeSpan = TimeSpan.FromSeconds(lastInterval);
	static TimeSpan Interval
	{
		get
		{
			if (lastInterval == EasterEggPageViewModel.LocationServiceInterval_Seconds)
				return lastIntervalTimeSpan;

			logger.Info("Interval is changed to {0}[s]", EasterEggPageViewModel.LocationServiceInterval_Seconds);
			lastInterval = EasterEggPageViewModel.LocationServiceInterval_Seconds;
			lastIntervalTimeSpan = TimeSpan.FromSeconds(lastInterval);
			return lastIntervalTimeSpan;
		}
	}

	public void ForceSetLocationInfo(int row, bool isRunningToNextStation)
	{
		if (!IsEnabled)
		{
			logger.Debug("IsEnabled is false -> do nothing");
			return;
		}

		logger.Debug("ForceSetLocationInfo({0}, {1})", row, isRunningToNextStation);
		LonLatLocationService.ForceSetLocationInfo(row, isRunningToNextStation);
		logger.Debug("Done");
	}

	private bool disposedValue;

	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			gpsCancelation?.Cancel();

			if (disposing)
			{
				gpsCancelation?.Dispose();
				gpsCancelation = null;
			}

			disposedValue = true;
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
