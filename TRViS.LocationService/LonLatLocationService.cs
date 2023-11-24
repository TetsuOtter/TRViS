
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
		CurrentStationIndex = GetNextStationIndex(StaLocationInfo ?? Array.Empty<StaLocationInfo>(), CURRENT_STATION_INDEX_NOT_SET);
		IsRunningToNextStation = false;
		DistanceHistoryQueue.Clear();

		if (invokeEvent)
		{
			LocationStateChanged?.Invoke(this, new(CurrentStationIndex, IsRunningToNextStation));
		}
	}

	static int GetPastStationIndex(in StaLocationInfo[] staLocationInfo, int currentStationIndex)
	{
		for (int i = currentStationIndex - 1; i >= 0; i--)
		{
			if (staLocationInfo[i].HasLonLatLocation)
				return i;
		}

		return CURRENT_STATION_INDEX_NOT_SET;
	}
	static int GetNextStationIndex(in StaLocationInfo[] staLocationInfo, int currentStationIndex)
	{
		for (int i = currentStationIndex + 1; i < staLocationInfo.Length; i++)
		{
			if (staLocationInfo[i].HasLonLatLocation)
				return i;
		}

		return CURRENT_STATION_INDEX_NOT_SET;
	}
	public void ForceSetLocationInfo(double lon_deg, double lat_deg)
	{
		ResetLocationInfo(false);
		if (StaLocationInfo is null)
		{
			LocationStateChanged?.Invoke(this, new(CurrentStationIndex, IsRunningToNextStation));
			return;
		}

		CurrentStationIndex = CURRENT_STATION_INDEX_NOT_SET;
		if (StaLocationInfo.Length <= 1)
		{
			if (StaLocationInfo.Length == 1 && StaLocationInfo[0].HasLonLatLocation)
			{
				CurrentStationIndex = 0;
			}

			LocationStateChanged?.Invoke(this, new(CurrentStationIndex, IsRunningToNextStation));
			return;
		}

		double[] distanceArray = StaLocationInfo.Select(
			v =>
				v.HasLonLatLocation
				? Utils.CalculateDistance_m(lon_deg, lat_deg, v.Location_lon_deg, v.Location_lat_deg)
				: double.NaN
		).ToArray();

		int nearestStationIndex = CURRENT_STATION_INDEX_NOT_SET;
		int firstStationIndex = CURRENT_STATION_INDEX_NOT_SET;
		int lastStationIndex = CURRENT_STATION_INDEX_NOT_SET;
		double nearestDistance = double.MaxValue;
		for (int i = 0; i < distanceArray.Length; i++)
		{
			if (double.IsNaN(distanceArray[i]))
				continue;

			if (firstStationIndex < 0)
				firstStationIndex = i;
			lastStationIndex = i;

			if (distanceArray[i] < nearestDistance)
			{
				nearestStationIndex = i;
				nearestDistance = distanceArray[i];
			}
		}

		// 全ての駅で位置情報が設定されていない場合
		if (nearestStationIndex < 0)
		{
			LocationStateChanged?.Invoke(this, new(CurrentStationIndex, IsRunningToNextStation));
			return;
		}

		// 有効な駅が1つしかない場合
		if (firstStationIndex == lastStationIndex)
		{
			CurrentStationIndex = firstStationIndex;
			IsRunningToNextStation = false;
			LocationStateChanged?.Invoke(this, new(CurrentStationIndex, IsRunningToNextStation));
			return;
		}

		if (nearestStationIndex == firstStationIndex)
		{
			// 最初の駅が一番近い場合
			CurrentStationIndex = firstStationIndex;
			IsRunningToNextStation = Utils.IsLeaved(StaLocationInfo[nearestStationIndex], distanceArray[nearestStationIndex]);
		}
		else if (nearestStationIndex == lastStationIndex)
		{
			// 最後の駅が一番近い場合
			if (Utils.IsNearBy(StaLocationInfo[nearestStationIndex], distanceArray[nearestStationIndex]))
			{
				CurrentStationIndex = nearestStationIndex;
				IsRunningToNextStation = false;
			}
			else
			{
				CurrentStationIndex = GetPastStationIndex(StaLocationInfo, nearestStationIndex);
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
				int pastStationIndex = GetPastStationIndex(StaLocationInfo, nearestStationIndex);
				int nextStationIndex = GetNextStationIndex(StaLocationInfo, nearestStationIndex);
				// 次の駅よりも前の駅の方が近い場合、おそらく前の駅からこの駅に向かっているところである
				if (distanceArray[pastStationIndex] < distanceArray[nextStationIndex])
				{
					CurrentStationIndex = pastStationIndex;
				}
			}
		}

		LocationStateChanged?.Invoke(this, new(CurrentStationIndex, IsRunningToNextStation));
	}

	public void ForceSetLocationInfo(int stationIndex, bool isRunningToNextStation)
	{
		ResetLocationInfo(false);
		if (StaLocationInfo is null)
		{
			LocationStateChanged?.Invoke(this, new(CurrentStationIndex, IsRunningToNextStation));
			return;
		}

		if (stationIndex < 0 || StaLocationInfo.Length <= stationIndex)
		{
			LocationStateChanged?.Invoke(this, new(CurrentStationIndex, IsRunningToNextStation));
			return;
		}

		bool isNextStationAvailable = 0 <= GetNextStationIndex(StaLocationInfo, stationIndex);
		// 現在駅に位置情報がセットされていない場合、「IsNearby」判定ができないため、次の駅に走行中であると仮定する
		// 但し、次の駅が存在しない場合は、次の駅のIsNearby判定ができないため、指定の駅に停車中であると仮定する
		IsRunningToNextStation = isNextStationAvailable && isRunningToNextStation;
		if (!StaLocationInfo[stationIndex].HasLonLatLocation)
		{
			IsRunningToNextStation = isNextStationAvailable;
		}

		CurrentStationIndex = stationIndex;
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
			// LastStationであれば、RunningToNextStationはfalseであるはずである。
			// -> 次の駅は必ず存在する
			int nextStationIndex = GetNextStationIndex(StaLocationInfo, CurrentStationIndex);
			StaLocationInfo nextStation = StaLocationInfo[nextStationIndex];
			double distanceToNextStationAverage = GetDistanceToStationAverage(nextStation, lon_deg, lat_deg);

			if (!double.IsNaN(distanceToNextStationAverage)
				&& Utils.IsNearBy(nextStation, distanceToNextStationAverage))
			{
				DistanceHistoryQueue.Clear();
				CurrentStationIndex = nextStationIndex;
				IsRunningToNextStation = false;
				LocationStateChanged?.Invoke(this, new(CurrentStationIndex, IsRunningToNextStation));
			}
		}
		else if (0 <= GetNextStationIndex(StaLocationInfo, CurrentStationIndex))
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
