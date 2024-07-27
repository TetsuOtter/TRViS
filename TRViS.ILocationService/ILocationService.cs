using System;

namespace TRViS.Services;

public interface ILocationService
{
	bool IsEnabled { get; set; }
	bool CanUseService { get; }
	event EventHandler<bool>? CanUseServiceChanged;

	event EventHandler<LocationStateChangedEventArgs>? LocationStateChanged;

	StaLocationInfo[]? StaLocationInfo { get; set; }

	int CurrentStationIndex { get; }

	bool IsRunningToNextStation { get; }

	void ResetLocationInfo();

	void ForceSetLocationInfo(int stationIndex, bool isRunningToNextStation);
}
