using SQLite;
using TRViS.IO.Models;

namespace TRViS.IO;

public class LoaderSQL : ILoader, IDisposable
{
	SQLiteConnection Connection { get; }

	public LoaderSQL(string path)
	{
		Connection = new(path);
	}

	public IReadOnlyList<TrainDataGroup> GetTrainDataGroupList()
	{
		List<TrainDataGroup> result = new();

		var res =
			from g in Connection.Table<Models.DB.WorkGroup>()
			join n in Connection.Table<Models.DB.Work>()
			on g.Id equals n.WorkGroupId
			join t in Connection.Table<Models.DB.TrainData>()
			on n.Id equals t.WorkId
			select new
			{
				GroupId = g.Id,
				GroupName = g.Name,
				WorkId = n.Id,
				WorkName = n.Name,
				TrainId = t.Id,
				TrainNumber = t.TrainNumber
			};

		foreach (var group in res.GroupBy(v => v.GroupId))
		{
			List<TrainDataFileInfo> fileInfo = new();
			foreach (var work in group)
				fileInfo.Add(new(work.WorkId.ToString(), work.TrainId.ToString(), work.WorkName, work.TrainNumber));
			result.Add(new(group.Key.ToString(), group.FirstOrDefault()?.GroupName ?? "N/A", fileInfo.ToArray()));
		}

		return result;
	}

	static TimeData? GetTimeData(int? hh, int? mm, int? ss, string? str)
		=> hh is null && mm is null && ss is null && string.IsNullOrEmpty(str) ? null : new(hh, mm, ss, str);

	public TrainData? GetTrainData(int trainId)
		=> (from t in Connection.Table<Models.DB.TrainData>()
				where t.Id == trainId
				join w in Connection.Table<Models.DB.Work>()
				on t.WorkId equals w.Id
				select new TrainData(
					WorkName: w.Name,
					AffectDate: Utils.StringToDateOnlyOrNull(w.AffectDate),
					TrainNumber: t.TrainNumber,
					MaxSpeed: t.MaxSpeed,
					SpeedType: t.SpeedType,
					NominalTractiveCapacity: t.NominalTractiveCapacity,
					CarCount: t.CarCount,
					Destination: t.Destination,
					BeginRemarks: t.BeginRemarks,
					AfterRemarks: t.AfterRemarks,
					Remarks: t.Remarks,
					BeforeDeparture: t.BeforeDeparture,
					TrainInfo: t.TrainInfo,
					(
						from r in Connection.Table<Models.DB.TimetableRowData>()
						where r.TrainId == trainId
						join s in Connection.Table<Models.DB.Station>()
						on r.StationId equals s.Id
						join n in Connection.Table<Models.DB.StationTrack>()
						on r.StationTrackId equals n.Id into track
						from tj in track.DefaultIfEmpty()
						orderby t.Direction >= 0 ? s.Location : (s.Location * -1)
						select new TimetableRow(
							Location: new(s.Location, s.Location_Lon_deg, s.Location_Lat_deg, s.OnStationDetectRadius_m),
							DriveTimeMM: r.DriveTime_MM,
							DriveTimeSS: r.DriveTime_SS,
							StationName: s.Name,
							IsOperationOnlyStop: r.IsOperationOnlyStop ?? false,
							IsPass: r.IsPass ?? false,
							HasBracket: r.HasBracket ?? false,
							IsLastStop: r.IsLastStop ?? false,
							ArriveTime: GetTimeData(r.Arrive_HH, r.Arrive_MM, r.Arrive_SS, r.Arrive_Str),
							DepartureTime: GetTimeData(r.Departure_HH, r.Departure_MM, r.Departure_SS, r.Departure_Str),
							TrackName: tj?.Name,
							RunInLimit: r.RunInLimit,
							RunOutLimit: r.RunOutLimit,
							Remarks: r.Remarks,

							IsInfoRow: s.RecordType
								is (int)Models.DB.StationRecordType.InfoRow_ForAlmostTrain
								or (int)Models.DB.StationRecordType.InfoRow_ForAlmostTrain,

							// TODO: マーカーのデフォルト設定のサポート
							DefaultMarkerColor_RGB: null,
							DefaultMarkerText: null
						)
					).ToArray(),
					Direction: t.Direction,
					AfterArrive: t.AfterArrive,
					BeforeDepartureOnStationTrackCol: t.BeforeDeparture_OnStationTrackCol,
					AfterArriveOnStationTrackCol: t.AfterArrive_OnStationTrackCol,
					DayCount: t.DayCount ?? 0,
					IsRideOnMoving: t.IsRideOnMoving,

					// TODO: E電時刻表用の線色設定のサポート
					LineColor_RGB: null
					)
				).FirstOrDefault();

	public IReadOnlyList<Models.DB.WorkGroup> GetWorkGroupList()
		=> Connection.Table<Models.DB.WorkGroup>().ToList();

	public IReadOnlyList<Models.DB.Work> GetWorkList(int workGroupId)
		=> Connection.Table<Models.DB.Work>().Where(v => v.WorkGroupId == workGroupId).ToList();

	public IReadOnlyList<Models.DB.TrainData> GetTrainDataList(int workId)
		=> Connection.Table<Models.DB.TrainData>().Where(v => v.WorkId == workId).ToList();

	public void Dispose()
	{
		Connection.Dispose();
	}
}
