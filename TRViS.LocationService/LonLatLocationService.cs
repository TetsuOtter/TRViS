
using TRViS.Services.LocationService;

namespace TRViS.Services;

public class LonLatLocationService : ILocationService
{
	public event EventHandler<LocationStateChangedEventArgs>? LocationStateChanged;

	const int CURRENT_STATION_INDEX_NOT_SET = -1;
	const int DISTANCE_HISTORY_QUEUE_SIZE = 3;
	readonly Queue<double> DistanceHistoryQueue = new(DISTANCE_HISTORY_QUEUE_SIZE);

	private StaLocationInfo[]? _staLocationInfo;
	public StaLocationInfo[]? StaLocationInfo
	{
		get => _staLocationInfo;
		set
		{
			if (value == _staLocationInfo)
				return;

			_staLocationInfo = value;
			ResetLocationInfo();
			LocationStateChanged?.Invoke(this, new(CurrentStationIndex, IsRunningToNextStation));
		}
	}

	public int CurrentStationIndex { get; private set; } = CURRENT_STATION_INDEX_NOT_SET;

	public bool IsRunningToNextStation { get; private set; }

	public void ResetLocationInfo()
		=> ResetLocationInfo(true);
	void ResetLocationInfo(bool invokeEvent)
	{
		CurrentStationIndex = StaLocationInfo is null ? CURRENT_STATION_INDEX_NOT_SET : 0;
		IsRunningToNextStation = false;
		DistanceHistoryQueue.Clear();

		if (invokeEvent)
		{
			LocationStateChanged?.Invoke(this, new(CurrentStationIndex, IsRunningToNextStation));
		}
	}

	public void ForceSetLocationInfo(double lon_deg, double lat_deg)
	{
		ResetLocationInfo(false);
		if (StaLocationInfo is null)
			return;

		if (StaLocationInfo.Length <= 1)
		{
			CurrentStationIndex = StaLocationInfo.Length - 1;
			IsRunningToNextStation = false;
			return;
		}

		double[] distanceArray = StaLocationInfo.Select(v => Utils.CalculateDistance_m(lon_deg, lat_deg, v.Location_lon_deg, v.Location_lat_deg)).ToArray();

		int nearestStationIndex = -1;
		double nearestDistance = double.MaxValue;
		for (int i = 0; i < distanceArray.Length; i++)
		{
			if (distanceArray[i] < nearestDistance)
			{
				nearestStationIndex = i;
				nearestDistance = distanceArray[i];
			}
		}

		if (nearestStationIndex == 0)
		{
			// 最初の駅が一番近い場合
			CurrentStationIndex = 0;
			IsRunningToNextStation = Utils.IsLeaved(StaLocationInfo[nearestStationIndex], distanceArray[nearestStationIndex]);
		}
		else if (nearestStationIndex == StaLocationInfo.Length - 1)
		{
			// 最後の駅が一番近い場合
			if (Utils.IsNearBy(StaLocationInfo[nearestStationIndex], distanceArray[nearestStationIndex]))
			{
				CurrentStationIndex = nearestStationIndex;
				IsRunningToNextStation = false;
			}
			else
			{
				CurrentStationIndex = nearestStationIndex - 1;
				IsRunningToNextStation = true;
			}
		}
		else
		{
			// 途中の駅が一番近い場合
			if (Utils.IsNearBy(StaLocationInfo[nearestStationIndex], distanceArray[nearestStationIndex]))
			{
				CurrentStationIndex = nearestStationIndex;
				IsRunningToNextStation = false;
			}
			else
			{
				IsRunningToNextStation = true;
				CurrentStationIndex = nearestStationIndex;
				// 次の駅よりも前の駅の方が近い場合、おそらく前の駅からこの駅に向かっているところである
				if (distanceArray[nearestStationIndex - 1] < distanceArray[nearestStationIndex + 1])
				{
					CurrentStationIndex = nearestStationIndex - 1;
				}
			}
		}

		LocationStateChanged?.Invoke(this, new(CurrentStationIndex, IsRunningToNextStation));
	}

	double GetDistanceToStationAverage(in StaLocationInfo staLocationInfo, double lon_deg, double lat_deg)
	{
		double distanceToStation = Utils.CalculateDistance_m(lon_deg, lat_deg, staLocationInfo.Location_lon_deg, staLocationInfo.Location_lat_deg);

		if (DistanceHistoryQueue.Count == DISTANCE_HISTORY_QUEUE_SIZE)
			DistanceHistoryQueue.Dequeue();
		DistanceHistoryQueue.Enqueue(distanceToStation);

		if (DistanceHistoryQueue.Count < DISTANCE_HISTORY_QUEUE_SIZE)
			return double.NaN;

		return DistanceHistoryQueue.Average();
	}

	public void SetCurrentLocation(double lon_deg, double lat_deg)
	{
		if (StaLocationInfo is null || CurrentStationIndex < 0 || StaLocationInfo.Length <= CurrentStationIndex)
			return;

		if (IsRunningToNextStation)
		{
			StaLocationInfo nextStation = StaLocationInfo[CurrentStationIndex + 1];
			double distanceToNextStationAverage = GetDistanceToStationAverage(nextStation, lon_deg, lat_deg);

			if (!double.IsNaN(distanceToNextStationAverage)
				&& Utils.IsNearBy(nextStation, distanceToNextStationAverage))
			{
				DistanceHistoryQueue.Clear();
				CurrentStationIndex++;
				IsRunningToNextStation = false;
				LocationStateChanged?.Invoke(this, new(CurrentStationIndex, IsRunningToNextStation));
			}
		}
		else if (CurrentStationIndex < StaLocationInfo.Length - 1)
		{
			StaLocationInfo currentStation = StaLocationInfo[CurrentStationIndex];

			double distanceFromCurrentStationAverage = GetDistanceToStationAverage(currentStation, lon_deg, lat_deg);
			if (!double.IsNaN(distanceFromCurrentStationAverage)
				&& Utils.IsLeaved(currentStation, distanceFromCurrentStationAverage))
			{
				DistanceHistoryQueue.Clear();
				IsRunningToNextStation = true;
				LocationStateChanged?.Invoke(this, new(CurrentStationIndex, IsRunningToNextStation));
			}
		}
	}
}
