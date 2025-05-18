
using TRViS.Services.LocationService;

namespace TRViS.Services;

public class LonLatLocationService : ILocationService
{
	private readonly NLog.Logger locationServiceLogger;
	public bool IsEnabled { get; set; }
	public event EventHandler<LocationStateChangedEventArgs>? LocationStateChanged;

	const int CURRENT_STATION_INDEX_NOT_SET = -1;
	const int DISTANCE_HISTORY_QUEUE_SIZE = 3;
	readonly Queue<double> DistanceHistoryQueue = new(DISTANCE_HISTORY_QUEUE_SIZE);

	public LonLatLocationService(NLog.Logger logger)
	{
		locationServiceLogger = logger;
		locationServiceLogger.Info("LonLatLocationService Created");
	}

	private bool _CanUseService = false;
	public bool CanUseService
	{
		get => _CanUseService;
		private set
		{
			locationServiceLogger.Info("CanUseService Set to: {0}", value);
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
			if (value is null)
			{
				locationServiceLogger.Info("StaLocationInfo Changed to: null");
			}
			else
			{
				locationServiceLogger.Info("StaLocationInfo Changed to length: {0}", value.Length);
				for (int i = 0; i < value.Length; i++)
				{
					locationServiceLogger.Info("StaLocationInfo[{0}]: {1}", i, _staLocationInfo?[i]);
				}
			}
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

		locationServiceLogger.Info("ResetLocationInfo: CurrentStationIndex: {0}, IsRunningToNextStation: {1}", CurrentStationIndex, IsRunningToNextStation);

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
			locationServiceLogger.Error("ForceSetLocationInfo({0}, {1}): CanUseService is false", lat_deg, lon_deg);
			return;
		}

		ResetLocationInfo(false);
		if (StaLocationInfo is null)
		{
			locationServiceLogger.Error("ForceSetLocationInfo({0}, {1}): StaLocationInfo is null", lat_deg, lon_deg);
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

			locationServiceLogger.Error("ForceSetLocationInfo({0}, {1}): StaLocationInfo.Length <= 1", lat_deg, lon_deg);
			LocationStateChanged?.Invoke(this, new(CurrentStationIndex, IsRunningToNextStation));
			return;
		}

		double[] distanceArray = StaLocationInfo.Select(
			v =>
				v.HasLonLatLocation
				? Utils.CalculateDistance_m(lon_deg, lat_deg, v.Location_lon_deg, v.Location_lat_deg)
				: double.NaN
		).ToArray();

		locationServiceLogger.Info("ForceSetLocationInfo({0}, {1}): distanceArray: {2}", lat_deg, lon_deg, string.Join(", ", distanceArray));

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
			locationServiceLogger.Error("ForceSetLocationInfo({0}, {1}): All stations have no location information", lat_deg, lon_deg);
			LocationStateChanged?.Invoke(this, new(CurrentStationIndex, IsRunningToNextStation));
			return;
		}

		// 有効な駅が1つしかない場合
		if (firstStationIndex == lastStationIndex)
		{
			locationServiceLogger.Warn("ForceSetLocationInfo({0}, {1}): Only one station has location information", lat_deg, lon_deg);
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

		locationServiceLogger.Info("ForceSetLocationInfo({0}, {1}) COMPLETE: CurrentStationIndex: {2}, IsRunningToNextStation: {3}", lat_deg, lon_deg, CurrentStationIndex, IsRunningToNextStation);
		LocationStateChanged?.Invoke(this, new(CurrentStationIndex, IsRunningToNextStation));
	}

	public void ForceSetLocationInfo(int stationIndex, bool isRunningToNextStation)
	{
		locationServiceLogger.Info("ForceSetLocationInfo(stationIndex: {0}, isRunningToNextStation: {1})", stationIndex, isRunningToNextStation);
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

	double GetDistanceToStationAverage(double distanceToStation)
	{
		if (DistanceHistoryQueue.Count == DISTANCE_HISTORY_QUEUE_SIZE)
			DistanceHistoryQueue.Dequeue();
		DistanceHistoryQueue.Enqueue(distanceToStation);

		if (DistanceHistoryQueue.Count < DISTANCE_HISTORY_QUEUE_SIZE)
		{
			locationServiceLogger.Debug("GetDistanceToStationAverage({0}): DistanceHistoryQueue.Count < {1}", distanceToStation, DISTANCE_HISTORY_QUEUE_SIZE);
			return double.NaN;
		}

		return DistanceHistoryQueue.Average();
	}

	/// <summary>
	/// 現在の位置情報を設定し、駅到着・駅出発の判定を行う。
	/// なお、指定回数分の平均距離にて判定を行うため、即時の判定ではない。
	/// </summary>
	/// <param name="lon_deg">現在の経度 [deg]</param>
	/// <param name="lat_deg">現在の経度 [deg]</param>
	/// <param name="useAverageDistance">平均距離を使用するかどうか</param>
	/// <returns>判定対象の駅までの距離</returns>
	public double SetCurrentLocation(double lon_deg, double lat_deg, bool useAverageDistance = true)
	{
		if (!CanUseService || StaLocationInfo is null || CurrentStationIndex < 0 || StaLocationInfo.Length <= CurrentStationIndex)
		{
			locationServiceLogger.Error("SetCurrentLocation({0}, {1}): CanUseService is false or StaLocationInfo is null or CurrentStationIndex is invalid", lat_deg, lon_deg);
			return double.NaN;
		}

		double distance = double.NaN;
		if (IsRunningToNextStation)
		{
			// LastStationであれば、RunningToNextStationはfalseであるはずである。
			// -> 次の駅は必ず存在する
			int nextStationIndex = GetNextStationIndex(StaLocationInfo, CurrentStationIndex);
			if (nextStationIndex < 0)
			{
				locationServiceLogger.Error("SetCurrentLocation({0}, {1}): IsRunningToNextStation=true: Next station index is invalid", lat_deg, lon_deg);
				// 入るはずはないけども、念のため。
				return double.NaN;
			}
			StaLocationInfo nextStation = StaLocationInfo[nextStationIndex];
			distance = Utils.CalculateDistance_m(nextStation, new LocationLonLat_deg(lon_deg, lat_deg));
			double distanceToNextStationAverage = GetDistanceToStationAverage(distance);
			locationServiceLogger.Info("SetCurrentLocation({0}, {1}): IsRunningToNextStation=true: nextStation={2}, distance={3}, distanceToNextStationAverage={4}, useAverageDistance={5}", lat_deg, lon_deg, nextStation, distance, distanceToNextStationAverage, useAverageDistance);
			if (!useAverageDistance)
			{
				distanceToNextStationAverage = distance;
			}

			if (!double.IsNaN(distanceToNextStationAverage)
				&& Utils.IsNearBy(nextStation, distanceToNextStationAverage))
			{
				DistanceHistoryQueue.Clear();
				CurrentStationIndex = nextStationIndex;
				IsRunningToNextStation = false;
				locationServiceLogger.Info("SetCurrentLocation({0}, {1}): IsRunningToNextStation=true: Arrived at next station: CurrentStationIndex={2}", lat_deg, lon_deg, CurrentStationIndex);
				LocationStateChanged?.Invoke(this, new(CurrentStationIndex, IsRunningToNextStation));
			}
		}
		else if (0 <= GetNextStationIndex(StaLocationInfo, CurrentStationIndex))
		{
			StaLocationInfo currentStation = StaLocationInfo[CurrentStationIndex];

			distance = Utils.CalculateDistance_m(currentStation, new LocationLonLat_deg(lon_deg, lat_deg));
			double distanceFromCurrentStationAverage = GetDistanceToStationAverage(distance);
			locationServiceLogger.Info("SetCurrentLocation({0}, {1}): IsRunningToNextStation=false: currentStation={2}, distance={3}, distanceFromCurrentStationAverage={4}, useAverageDistance={5}", lat_deg, lon_deg, currentStation, distance, distanceFromCurrentStationAverage, useAverageDistance);
			if (!useAverageDistance)
			{
				distanceFromCurrentStationAverage = distance;
			}
			if (!double.IsNaN(distanceFromCurrentStationAverage)
				&& Utils.IsLeaved(currentStation, distanceFromCurrentStationAverage))
			{
				DistanceHistoryQueue.Clear();
				IsRunningToNextStation = true;
				locationServiceLogger.Info("SetCurrentLocation({0}, {1}): IsRunningToNextStation=false: Departed from current station: CurrentStationIndex={2}", lat_deg, lon_deg, CurrentStationIndex);
				LocationStateChanged?.Invoke(this, new(CurrentStationIndex, IsRunningToNextStation));
			}
		}

		return distance;
	}
}
