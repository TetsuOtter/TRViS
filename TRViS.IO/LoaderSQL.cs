using SQLite;
using TRViS.IO.Models;

namespace TRViS.IO;

public class LoaderSQL : IDisposable
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
					w.Name,
					DateOnly.TryParse(w.AffectDate, out DateOnly date) ? date : DateOnly.FromDateTime(DateTime.Now),
					t.TrainNumber,
					t.MaxSpeed,
					t.SpeedType,
					t.NominalTractiveCapacity,
					t.CarCount,
					t.Destination,
					t.BeginRemarks,
					t.AfterRemarks,
					t.Remarks,
					t.BeforeDeparture,
					t.TrainInfo,
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
							s.Location,
							r.DriveTime_MM,
							r.DriveTime_SS,
							s.Name,
							r.IsOperationOnlyStop ?? false,
							r.IsPass ?? false,
							r.HasBracket ?? false,
							r.IsLastStop ?? false,
							GetTimeData(r.Arrive_HH, r.Arrive_MM, r.Arrive_SS, r.Arrive_Str),
							GetTimeData(r.Departure_HH, r.Departure_MM, r.Departure_SS, r.Departure_Str),
							tj?.Name,
							r.RunInLimit,
							r.RunOutLimit,
							r.Remarks
						)
					).ToArray(),
					t.Direction
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
