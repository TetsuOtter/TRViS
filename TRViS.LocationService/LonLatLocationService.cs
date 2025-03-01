
using TRViS.Services.LocationService;

namespace TRViS.Services;

public class LonLatLocationService : ILocationService
{
	public bool IsEnabled { get; set; }
	public event EventHandler<LocationStateChangedEventArgs>? LocationStateChanged;

	const int CURRENT_STATION_INDEX_NOT_SET = -1;
	const int DISTANCE_HISTORY_QUEUE_SIZE = 3;
	readonly Queue<double> DistanceHistoryQueue = new(DISTANCE_HISTORY_QUEUE_SIZE);

	private bool _CanUseService = false;
	public bool CanUseService
	{
		get => _CanUseService;
		private set
		{
			if (value == _CanUseService)
				return;

			_CanUseService = value;
			CanUseServiceChanged?.Invoke(this, value);
		}
	}
	public event EventHandler<bool>? CanUseServiceChanged;

	private StaLocationInfo[]? _staLocationInfo;
	public StaLocationInfo[]? StaLocationInfo
	{
		get => _staLocationInfo;
		set
		{
			if (value == _staLocationInfo)
				return;

			_staLocationInfo = value;
			CanUseService = _staLocationInfo?.Any(static v => v.HasLonLatLocation) ?? false;
			ResetLocationInfo();
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
		if (currentStationIndex <= 0)
			return CURRENT_STATION_INDEX_NOT_SET;

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
		if (!CanUseService)
		{
			return;
		}

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
		if (!CanUseService)
		{
			return;
		}

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

	/// <summary>
	/// 現在の位置情報を設定し、駅到着・駅出発の判定を行う。
	/// なお、指定回数分の平均距離にて判定を行うため、即時の判定ではない。
	/// </summary>
	/// <param name="lon_deg">現在の経度 [deg]</param>
	/// <param name="lat_deg">現在の経度 [deg]</param>
	/// <returns>判定対象の駅までの距離</returns>
	public double SetCurrentLocation(double lon_deg, double lat_deg)
	{
		if (!CanUseService || StaLocationInfo is null || CurrentStationIndex < 0 || StaLocationInfo.Length <= CurrentStationIndex)
			return double.NaN;

		double distance = double.NaN;
		if (IsRunningToNextStation)
		{
			// LastStationであれば、RunningToNextStationはfalseであるはずである。
			// -> 次の駅は必ず存在する
			int nextStationIndex = GetNextStationIndex(StaLocationInfo, CurrentStationIndex);
			if (nextStationIndex < 0)
			{
				// 入るはずはないけども、念のため。
				return double.NaN;
			}
			StaLocationInfo nextStation = StaLocationInfo[nextStationIndex];
			distance = Utils.CalculateDistance_m(nextStation, new LocationLonLat_deg(lon_deg, lat_deg));
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

			distance = Utils.CalculateDistance_m(currentStation, new LocationLonLat_deg(lon_deg, lat_deg));
			double distanceFromCurrentStationAverage = GetDistanceToStationAverage(currentStation, lon_deg, lat_deg);
			if (!double.IsNaN(distanceFromCurrentStationAverage)
				&& Utils.IsLeaved(currentStation, distanceFromCurrentStationAverage))
			{
				DistanceHistoryQueue.Clear();
				IsRunningToNextStation = true;
				LocationStateChanged?.Invoke(this, new(CurrentStationIndex, IsRunningToNextStation));
			}
		}

		return distance;
	}
}
