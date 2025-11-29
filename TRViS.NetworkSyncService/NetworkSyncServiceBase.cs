using System;
using System.Threading;
using System.Threading.Tasks;

using TRViS.Services;

namespace TRViS.NetworkSyncService;

/// <summary>
/// Base class for NetworkSyncService manager that handles common functionality
/// for both HTTP and WebSocket implementations
/// </summary>
public abstract class NetworkSyncServiceBase : ILocationService, IDisposable
{
	public bool IsEnabled { get; set; }
	private bool _CanUseService = false;
	public bool CanUseService
	{
		get => _CanUseService;
		protected set
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

	private string? _WorkGroupId;
	public string? WorkGroupId
	{
		get => _WorkGroupId;
		set
		{
			if (_WorkGroupId == value)
				return;
			_WorkGroupId = value;
			OnWorkGroupIdChanged(value);
		}
	}

	private string? _WorkId;
	public string? WorkId
	{
		get => _WorkId;
		set
		{
			if (_WorkId == value)
				return;
			_WorkId = value;
			OnWorkIdChanged(value);
		}
	}

	private string? _TrainId;
	public string? TrainId
	{
		get => _TrainId;
		set
		{
			if (_TrainId == value)
				return;
			_TrainId = value;
			OnTrainIdChanged(value);
		}
	}

	public int CurrentStationIndex { get; private set; }

	public bool IsRunningToNextStation { get; private set; }

	public bool IsTimeServiceEnabled { get; private set; }
	public int CurrentTime_s { get; private set; }

	public event EventHandler<bool>? CanUseServiceChanged;
	public event EventHandler<LocationStateChangedEventArgs>? LocationStateChanged;
	public event EventHandler<int>? TimeChanged;
	public event EventHandler<TimetableData>? TimetableUpdated;

	protected bool _IsDisposed;

	protected NetworkSyncServiceBase()
	{
	}

	/// <summary>
	/// Get the latest synced data from the service
	/// </summary>
	protected abstract Task<SyncedData> GetSyncedDataAsync(CancellationToken token);

	/// <summary>
	/// Called when WorkGroupId property changes
	/// </summary>
	protected virtual void OnWorkGroupIdChanged(string? value) { }

	/// <summary>
	/// Called when WorkId property changes
	/// </summary>
	protected virtual void OnWorkIdChanged(string? value) { }

	/// <summary>
	/// Called when TrainId property changes
	/// </summary>
	protected virtual void OnTrainIdChanged(string? value) { }

	/// <summary>
	/// Poll for synced data. Used by HTTP implementation for periodic polling.
	/// WebSocket implementation handles updates via event-driven approach instead.
	/// </summary>
	public async Task TickAsync(CancellationToken? cancellationToken = null)
	{
		cancellationToken ??= CancellationToken.None;
		SyncedData result = await GetSyncedDataAsync(cancellationToken.Value);
		ProcessSyncedData(result);
	}

	/// <summary>
	/// Process synced data and update state. Called by TickAsync (HTTP) or directly (WebSocket).
	/// </summary>
	protected void ProcessSyncedData(SyncedData syncedData)
	{
		UpdateCurrentStationWithLocation(syncedData.Location_m);

		int currentTime_s = (int)(syncedData.Time_ms / 1000);
		if (CurrentTime_s != currentTime_s)
		{
			CurrentTime_s = currentTime_s;
			TimeChanged?.Invoke(this, CurrentTime_s);
		}

		CanUseService = syncedData.CanStart;
	}

	void UpdateCurrentStationWithLocation(double location_m)
	{
		if (StaLocationInfo is null || !IsEnabled || double.IsNaN(location_m))
			return;

		bool isIn(double threshold1, double threshold2)
		{
			if (threshold1 < threshold2)
				return threshold1 <= location_m && location_m < threshold2;
			else
				return threshold2 <= location_m && location_m < threshold1;
		}

		// 距離が逆戻りする可能性は考えない
		for (int i = 0; i < StaLocationInfo.Length; i++)
		{
			double staLocation_m = StaLocationInfo[i].Location_m;
			double staNearbyRadius_m = StaLocationInfo[i].NearbyRadius_m;
			if (isIn(staLocation_m - staNearbyRadius_m, staLocation_m + staNearbyRadius_m))
			{
				if (i != CurrentStationIndex || IsRunningToNextStation)
					ForceSetLocationInfo(i, false);
				return;
			}
			else if (i != 0 && isIn(StaLocationInfo[i - 1].Location_m, staLocation_m))
			{
				if (i - 1 != CurrentStationIndex || !IsRunningToNextStation)
					ForceSetLocationInfo(i - 1, true);
				return;
			}
		}

		double distanceFromFirstStation = Math.Abs(location_m - StaLocationInfo[0].Location_m);
		double distanceFromLastStation = Math.Abs(location_m - StaLocationInfo[^1].Location_m);
		if (distanceFromFirstStation < distanceFromLastStation)
		{
			if (0 != CurrentStationIndex || IsRunningToNextStation)
				ForceSetLocationInfo(0, false);
		}
		else
		{
			if (StaLocationInfo.Length - 1 != CurrentStationIndex || IsRunningToNextStation)
				ForceSetLocationInfo(StaLocationInfo.Length - 1, false);
		}
	}

	protected void RaiseTimetableUpdated(TimetableData timetableData)
	{
		// 時刻表の変更スコープに応じて、表示継続可能か判定する
		bool canContinue = CanContinueCurrentTimetable(timetableData);

		if (!canContinue)
		{
			// 表示継続不可の場合は初期状態に戻す
			ResetLocationInfo();
		}

		// 時刻表更新イベントを外部に通知
		TimetableUpdated?.Invoke(this, timetableData);
	}

	private bool CanContinueCurrentTimetable(TimetableData timetableData)
	{
		// 変更スコープに基づいて判定する
		return timetableData.Scope switch
		{
			// WorkGroup単位の変更：現在の選択がこのWorkGroupと異なる場合のみ継続可能
			TimetableScopeType.WorkGroup => _WorkGroupId != timetableData.WorkGroupId,

			// Work単位の変更：現在の選択がこのWorkと異なる場合のみ継続可能
			TimetableScopeType.Work => _WorkId != timetableData.WorkId,

			// Train単位の変更：現在の選択がこのTrainと異なる場合のみ継続可能
			TimetableScopeType.Train => _TrainId != timetableData.TrainId,

			_ => true
		};
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

	public abstract void Dispose();
}
