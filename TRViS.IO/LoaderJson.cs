using System.Text.Json;
using System.Text.RegularExpressions;

using TRViS.IO.Models;
using TRViS.IO.Models.DB;
using TRViS.IO.Models.Json;

namespace TRViS.IO;

public class LoaderJson : ILoader
{
	record Relation(Models.Json.WorkGroupData? Group, Models.Json.WorkData? Work, Models.Json.TrainData? Train);

	WorkGroupData[] WorkGroups { get; }

	List<Relation> WorkList { get; }
	List<Relation> TrainList { get; }

	static readonly Regex timePatternRegex = new("^[0-9]{0,2}:[0-9]{0,2}:[0-9]{0,2}$", RegexOptions.Compiled);
	static TimeData GetTimeData(string? timeStr)
	{
		static int? toIntOrNull(in string s)
			=> int.TryParse(s, out int v) ? v : null;

		if (string.IsNullOrEmpty(timeStr) || timePatternRegex.IsMatch(timeStr))
			return new TimeData(null, null, null, timeStr);

		string[] hhmmss = timeStr.Split(':', StringSplitOptions.None);

		return new TimeData(
			toIntOrNull(hhmmss[0]),
			toIntOrNull(hhmmss[1]),
			toIntOrNull(hhmmss[2]),
			null
		);
	}

	static readonly JsonSerializerOptions opts = new()
	{
		AllowTrailingCommas = true,
	};

	private LoaderJson(WorkGroupData[] workGroups)
	{
		if (workGroups is null)
			throw new ArgumentNullException(nameof(workGroups));

		WorkGroups = workGroups;

		WorkList = new();
		foreach (var v in WorkGroups)
			WorkList.AddRange(v.Works.Select(work => new Relation(v, work, null)));

		TrainList = new();
		foreach (var v in WorkList)
			TrainList.AddRange(v.Work?.Trains.Select(train => new Relation(v.Group, v.Work, train)) ?? Enumerable.Empty<Relation>());
	}

	public static Task<LoaderJson> InitFromFileAsync(string filePath)
		=> InitFromFileAsync(filePath, CancellationToken.None);
	public static async Task<LoaderJson> InitFromFileAsync(string filePath, CancellationToken token)
	{
		using FileStream stream = File.OpenRead(filePath);

		return await InitFromStreamAsync(stream, token);
	}
	public static async Task<LoaderJson> InitFromStreamAsync(Stream stream, CancellationToken token)
	{
		WorkGroupData[]? workGroups = await JsonSerializer.DeserializeAsync<WorkGroupData[]>(stream, opts, token);

		return new LoaderJson(workGroups!);
	}

	public void Dispose()
	{
	}

	public Models.TrainData? GetTrainData(int trainId)
	{
		if (TrainList.Count <= trainId)
			throw new ArgumentOutOfRangeException(nameof(trainId));

		Relation r = TrainList[trainId];

		if (r.Work is null || r.Train is null)
			throw new InvalidOperationException("Work or Train is not set");

		WorkData w = r.Work;
		Models.Json.TrainData t = r.Train;

		return new Models.TrainData(
			WorkName: r.Work.Name,
			AffectDate: DateOnly.TryParse(r.Work.AffectDate, out DateOnly date) ? date : null,
			TrainNumber: r.Train.TrainNumber,
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
			Rows: t.TimetableRows.Select(v => new TimetableRow(
				Location: new(v.Location_m, v.Longitude_deg, v.Latitude_deg, v.OnStationDetectRadius_m),
				DriveTimeMM: v.DriveTime_MM,
				DriveTimeSS: v.DriveTime_SS,
				StationName: v.StationName,
				IsOperationOnlyStop: v.IsOperationOnlyStop ?? false,
				IsPass: v.IsPass ?? false,
				HasBracket: v.HasBracket ?? false,
				IsLastStop: v.IsLastStop ?? false,
				ArriveTime: GetTimeData(v.Arrive),
				DepartureTime: GetTimeData(v.Departure),
				TrackName: v.TrackName,
				RunInLimit: v.RunInLimit,
				RunOutLimit: v.RunOutLimit,
				Remarks: v.Remarks,

				IsInfoRow: v.RecordType
					is (int)Models.DB.StationRecordType.InfoRow_ForAlmostTrain
					or (int)Models.DB.StationRecordType.InfoRow_ForSomeTrain,

				// TODO: マーカーのデフォルト設定のサポート
				DefaultMarkerColor_RGB: null,
				DefaultMarkerText: null
			)).ToArray(),
			Direction: t.Direction,
			AfterArrive: t.AfterArrive,
			BeforeDepartureOnStationTrackCol: t.BeforeDeparture_OnStationTrackCol,
			AfterArriveOnStationTrackCol: t.AfterArrive_OnStationTrackCol,
			DayCount: t.DayCount ?? 0,
			IsRideOnMoving: t.IsRideOnMoving,

			// TODO: E電時刻表用の線色設定のサポート
			LineColor_RGB: null
		);
	}

	public IReadOnlyList<WorkGroup> GetWorkGroupList()
		=> WorkGroups
			.Select((v, i) => new WorkGroup()
			{
				Id = i,
				Name = v.Name,

				DBVersion = v.DBVersion,
			})
			.ToArray();

	public IReadOnlyList<Work> GetWorkList(int workGroupId)
	{
		if (WorkGroups.Length <= workGroupId)
			throw new ArgumentOutOfRangeException(nameof(workGroupId));

		return WorkGroups[workGroupId].Works
			.Select((v) =>
			new Work()
			{
				WorkGroupId = workGroupId,
				Id = WorkList.IndexOf(new Relation(WorkGroups[workGroupId], v, null)),
				Name = v.Name,

				AffectDate = v.AffectDate ?? string.Empty,
				AffixContent = null, // v.AffixContent,
				AffixContentType = v.AffixContentType,
				ETrainTimetableContent = null, // v.ETrainTimetableContent,
				ETrainTimetableContentType = v.ETrainTimetableContentType,
				HasETrainTimetable = v.HasETrainTimetable,
				Remarks = v.Remarks,
			})
			.ToList();
	}

	public IReadOnlyList<Models.DB.TrainData> GetTrainDataList(int workId)
	{
		if (WorkList.Count <= workId)
			throw new ArgumentOutOfRangeException(nameof(workId));

		return WorkList[workId].Work?.Trains
			.Select(v =>
			new Models.DB.TrainData()
			{
				AfterArrive = v.AfterArrive,
				AfterArrive_OnStationTrackCol = v.AfterArrive_OnStationTrackCol,
				AfterRemarks = v.AfterRemarks,
				BeforeDeparture = v.BeforeDeparture,
				BeforeDeparture_OnStationTrackCol = v.BeforeDeparture_OnStationTrackCol,
				BeginRemarks = v.BeginRemarks,
				CarCount = v.CarCount,
				ColorId = -1, // Not Implemented
				DayCount = v.DayCount,
				Destination = v.Destination,
				Id = TrainList.IndexOf(WorkList[workId] with { Train = v }),
				Direction = v.Direction,
				IsRideOnMoving = v.IsRideOnMoving,
				MaxSpeed = v.MaxSpeed,
				NominalTractiveCapacity = v.NominalTractiveCapacity,
				Remarks = v.Remarks,
				SpeedType = v.SpeedType,
				TrainInfo = v.TrainInfo,
				TrainNumber = v.TrainNumber,
				WorkId = workId,
				WorkType = v.WorkType,
			})
			.ToList() ?? new List<Models.DB.TrainData>();
	}

	public IReadOnlyList<TrainDataGroup> GetTrainDataGroupList()
	{
		throw new NotImplementedException();
	}
}
