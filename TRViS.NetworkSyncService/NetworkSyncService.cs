using System;
using TRViS.Services;

namespace TRViS;

public class NetworkSyncService : ILocationService
{
	private bool _CanUseService = false;
	public bool CanUseService
	{
		get => _CanUseService;
		private set
		{
			if (_CanUseService == value)
				return;

			_CanUseService = value;
			CanUseServiceChanged?.Invoke(this, value);
		}
	}

	private StaLocationInfo[]? _staLocationInfo;
	public StaLocationInfo[]? StaLocationInfo
	{
		get => _staLocationInfo;
		set
		{
			if (_staLocationInfo == value)
				return;

			_staLocationInfo = value;
			ResetLocationInfo();
		}
	}

	public int CurrentStationIndex { get; private set; }

	public bool IsRunningToNextStation { get; private set; }

	public event EventHandler<bool>? CanUseServiceChanged;
	public event EventHandler<LocationStateChangedEventArgs>? LocationStateChanged;

	private NetworkSyncService()
	{
		// TODO: Impl
	}

	public static NetworkSyncService CreateFromUri(Uri uri)
	{
		// TODO: Impl
		return new();
	}

	public void ForceSetLocationInfo(
		int stationIndex,
		bool isRunningToNextStation
	)
	{
		CurrentStationIndex = stationIndex;
		IsRunningToNextStation = isRunningToNextStation;
		LocationStateChanged?.Invoke(this, new LocationStateChangedEventArgs(CurrentStationIndex, IsRunningToNextStation));
	}

	public void ResetLocationInfo()
	{
		CurrentStationIndex = 0;
		IsRunningToNextStation = false;
		LocationStateChanged?.Invoke(this, new LocationStateChangedEventArgs(CurrentStationIndex, IsRunningToNextStation));
	}
}
