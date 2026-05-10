using System;
using System.Collections.Generic;
using System.Linq;

using TRViS.LocationService.Abstractions;

using static TRViS.NetworkSyncService.Internals.LocationCalcUtils;

namespace TRViS.NetworkSyncService.Internals;

/// <summary>
/// 緯度経度からの駅判定アルゴリズム。
/// <see cref="NetworkSyncServiceBase"/> がサーバーから受信した <c>Location_m</c> が NaN
/// で、緯度経度が代わりに配信されているときに使う。
/// 既存の <c>LonLatLocationService</c> と同じ平均距離ベースのヒューリスティックを実装する。
/// </summary>
internal sealed class LonLatStationDetector
{
	const int CURRENT_STATION_INDEX_NOT_SET = -1;
	const int DISTANCE_HISTORY_QUEUE_SIZE = 3;

	private readonly Queue<double> _distanceHistory = new(DISTANCE_HISTORY_QUEUE_SIZE);

	private StaLocationInfo[]? _staLocationInfo;

	public int CurrentStationIndex { get; private set; } = CURRENT_STATION_INDEX_NOT_SET;
	public bool IsRunningToNextStation { get; private set; }
	public bool IsFirstFix { get; private set; } = true;

	/// <summary>
	/// 駅情報を更新する。状態 (CurrentStationIndex / IsRunningToNextStation / 履歴) はリセットされる。
	/// </summary>
	public void SetStationLocations(StaLocationInfo[]? staLocationInfo)
	{
		_staLocationInfo = staLocationInfo;
		Reset();
	}

	public void Reset()
	{
		CurrentStationIndex = GetNextStationIndex(_staLocationInfo, CURRENT_STATION_INDEX_NOT_SET);
		IsRunningToNextStation = false;
		IsFirstFix = true;
		_distanceHistory.Clear();
	}

	/// <summary>
	/// 既存の駅 index と running フラグを外部状態から強制セットする。
	/// 平均距離履歴と「初回」フラグもクリアする。
	/// </summary>
	public void Sync(int currentStationIndex, bool isRunningToNextStation)
	{
		CurrentStationIndex = currentStationIndex;
		IsRunningToNextStation = isRunningToNextStation;
		IsFirstFix = false;
		_distanceHistory.Clear();
	}

	/// <summary>
	/// 緯度経度を一回分供給して状態を更新する。
	/// 戻り値は (CurrentStationIndex, IsRunningToNextStation) が変化したかどうか。
	/// </summary>
	public bool UpdateWithLonLat(double lon_deg, double lat_deg)
	{
		if (_staLocationInfo is null || _staLocationInfo.Length == 0)
			return false;

		int prevIndex = CurrentStationIndex;
		bool prevRunning = IsRunningToNextStation;

		if (IsFirstFix)
		{
			ApplyInitialFix(lon_deg, lat_deg);
			IsFirstFix = false;
		}
		else
		{
			ApplySubsequentFix(lon_deg, lat_deg);
		}

		return prevIndex != CurrentStationIndex || prevRunning != IsRunningToNextStation;
	}

	// ============================================================
	// 初回測位 (LonLatLocationService.ForceSetLocationInfo 相当)
	// ============================================================
	private void ApplyInitialFix(double lon_deg, double lat_deg)
	{
		StaLocationInfo[] stations = _staLocationInfo!;

		// 緯度経度を持たない駅は対象外
		double[] distanceArray = stations.Select(
			v => v.HasLonLatLocation
				? CalculateDistance_m(lon_deg, lat_deg, v.Location_lon_deg, v.Location_lat_deg)
				: double.NaN
		).ToArray();

		int nearestIndex = CURRENT_STATION_INDEX_NOT_SET;
		int firstIndex = CURRENT_STATION_INDEX_NOT_SET;
		int lastIndex = CURRENT_STATION_INDEX_NOT_SET;
		double nearestDistance = double.MaxValue;
		for (int i = 0; i < distanceArray.Length; i++)
		{
			if (double.IsNaN(distanceArray[i]))
				continue;
			if (firstIndex < 0)
				firstIndex = i;
			lastIndex = i;
			if (distanceArray[i] < nearestDistance)
			{
				nearestIndex = i;
				nearestDistance = distanceArray[i];
			}
		}

		if (nearestIndex < 0)
		{
			// 全ての駅で緯度経度が無い: なにもしない
			CurrentStationIndex = CURRENT_STATION_INDEX_NOT_SET;
			IsRunningToNextStation = false;
			return;
		}

		// 有効な駅が1つしかない場合
		if (firstIndex == lastIndex)
		{
			CurrentStationIndex = firstIndex;
			IsRunningToNextStation = false;
			return;
		}

		if (nearestIndex == firstIndex)
		{
			CurrentStationIndex = firstIndex;
			IsRunningToNextStation = IsLeaved(stations[nearestIndex], distanceArray[nearestIndex]);
		}
		else if (nearestIndex == lastIndex)
		{
			if (IsNearBy(stations[nearestIndex], distanceArray[nearestIndex]))
			{
				CurrentStationIndex = nearestIndex;
				IsRunningToNextStation = false;
			}
			else
			{
				CurrentStationIndex = GetPastStationIndex(stations, nearestIndex);
				IsRunningToNextStation = true;
			}
		}
		else
		{
			if (IsNearBy(stations[nearestIndex], distanceArray[nearestIndex]))
			{
				CurrentStationIndex = nearestIndex;
				IsRunningToNextStation = false;
			}
			else
			{
				IsRunningToNextStation = true;
				CurrentStationIndex = nearestIndex;
				int pastIndex = GetPastStationIndex(stations, nearestIndex);
				int nextIndex = GetNextStationIndex(stations, nearestIndex);
				if (pastIndex >= 0 && nextIndex >= 0
					&& distanceArray[pastIndex] < distanceArray[nextIndex])
				{
					CurrentStationIndex = pastIndex;
				}
			}
		}
	}

	// ============================================================
	// 連続測位 (LonLatLocationService.SetCurrentLocation 相当)
	// ============================================================
	private void ApplySubsequentFix(double lon_deg, double lat_deg)
	{
		StaLocationInfo[] stations = _staLocationInfo!;
		if (CurrentStationIndex < 0 || stations.Length <= CurrentStationIndex)
			return;

		if (IsRunningToNextStation)
		{
			int nextIndex = GetNextStationIndex(stations, CurrentStationIndex);
			if (nextIndex < 0)
				return;
			StaLocationInfo nextStation = stations[nextIndex];
			double distance = CalculateDistance_m(
				lon_deg, lat_deg, nextStation.Location_lon_deg, nextStation.Location_lat_deg);
			double avg = PushDistanceAndAverage(distance);
			if (!double.IsNaN(avg) && IsNearBy(nextStation, avg))
			{
				_distanceHistory.Clear();
				CurrentStationIndex = nextIndex;
				IsRunningToNextStation = false;
			}
		}
		else if (GetNextStationIndex(stations, CurrentStationIndex) >= 0)
		{
			StaLocationInfo currentStation = stations[CurrentStationIndex];
			double distance = CalculateDistance_m(
				lon_deg, lat_deg, currentStation.Location_lon_deg, currentStation.Location_lat_deg);
			double avg = PushDistanceAndAverage(distance);
			if (!double.IsNaN(avg) && IsLeaved(currentStation, avg))
			{
				_distanceHistory.Clear();
				IsRunningToNextStation = true;
			}
		}
	}

	private double PushDistanceAndAverage(double distance)
	{
		if (_distanceHistory.Count == DISTANCE_HISTORY_QUEUE_SIZE)
			_distanceHistory.Dequeue();
		_distanceHistory.Enqueue(distance);
		if (_distanceHistory.Count < DISTANCE_HISTORY_QUEUE_SIZE)
			return double.NaN;
		return _distanceHistory.Average();
	}

	private static int GetPastStationIndex(StaLocationInfo[] stations, int currentIndex)
	{
		if (currentIndex <= 0)
			return CURRENT_STATION_INDEX_NOT_SET;
		for (int i = currentIndex - 1; i >= 0; i--)
		{
			if (stations[i].HasLonLatLocation)
				return i;
		}
		return CURRENT_STATION_INDEX_NOT_SET;
	}

	private static int GetNextStationIndex(StaLocationInfo[]? stations, int currentIndex)
	{
		if (stations is null)
			return CURRENT_STATION_INDEX_NOT_SET;
		for (int i = currentIndex + 1; i < stations.Length; i++)
		{
			if (stations[i].HasLonLatLocation)
				return i;
		}
		return CURRENT_STATION_INDEX_NOT_SET;
	}
}
