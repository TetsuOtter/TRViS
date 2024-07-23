using System.Text.Json;
using System.Text.RegularExpressions;

using TRViS.IO.Models;
using TRViS.IO.Models.DB;
using TRViS.JsonModels;

namespace TRViS.IO;

public class LoaderJson : ILoader
{
	Dictionary<string, WorkGroup> WorkGroups { get; } = [];
	Dictionary<string, Work> WorkData { get; } = [];
	Dictionary<string, (Models.DB.TrainData, TimetableRow[])> TrainData { get; } = [];

	Dictionary<string, string> WorkGroupIdByWorkId { get; } = [];
	Dictionary<string, string> WorkIdByTrainId { get; } = [];

	static readonly Regex timePatternRegex = new("^[0-9]{0,2}:[0-9]{0,2}:[0-9]{0,2}$", RegexOptions.Compiled);
	static TimeData GetTimeData(string? timeStr)
	{
		static int? toIntOrNull(in string s)
			=> int.TryParse(s, out int v) ? v : null;

		if (string.IsNullOrEmpty(timeStr) || !timePatternRegex.IsMatch(timeStr))
			return new TimeData(null, null, null, timeStr);

		string[] hhmmss = timeStr.Split(':', StringSplitOptions.None);

		return new TimeData(
			toIntOrNull(hhmmss[0]),
			toIntOrNull(hhmmss[1]),
			toIntOrNull(hhmmss[2]),
			null
		);
	}
	static string GenerateUniqueId<T>(IReadOnlyDictionary<string, T> dict)
	{
		for (int i = 0; i < 100; i++)
		{
			string tmpId = Guid.NewGuid().ToString();
			if (!dict.ContainsKey(tmpId))
				return tmpId;
		}
		throw new InvalidOperationException("Failed to generate a unique ID");
	}

	static readonly JsonSerializerOptions opts = new()
	{
		AllowTrailingCommas = true,
	};

	private LoaderJson(WorkGroupData[] workGroups)
	{
		if (workGroups is null)
			throw new ArgumentNullException(nameof(workGroups));

		for (int workGroupIndex = 0; workGroupIndex < workGroups.Length; workGroupIndex++)
		{
			WorkGroupData workGroup = workGroups[workGroupIndex];
			string workGroupId = workGroup.Id ?? GenerateUniqueId(WorkGroups);
			WorkGroups[workGroupId] = new()
			{
				Id = workGroupId,
				Name = workGroup.Name,
				DBVersion = workGroup.DBVersion,
			};

			WorkData[] workList = workGroup.Works;
			for (int workIndex = 0; workIndex < workList.Length; workIndex++)
			{
				WorkData workData = workList[workIndex];
				string workId = workData.Id ?? GenerateUniqueId(WorkData);
				WorkData[workId] = new()
				{
					WorkGroupId = workGroupId,
					Id = workId,
					Name = workData.Name,

					AffectDate = workData.AffectDate ?? string.Empty,
					AffixContent = null, // workData.AffixContent,
					AffixContentType = workData.AffixContentType,
					ETrainTimetableContent = null, // workData.ETrainTimetableContent,
					ETrainTimetableContentType = workData.ETrainTimetableContentType,
					HasETrainTimetable = workData.HasETrainTimetable,
					Remarks = workData.Remarks,
				};
				WorkGroupIdByWorkId[workId] = workGroupId;


				JsonModels.TrainData[] trainList = workData.Trains;
				for (int trainIndex = 0; trainIndex < trainList.Length; trainIndex++)
				{
					JsonModels.TrainData trainData = trainList[trainIndex];
					string trainId = trainData.Id ?? GenerateUniqueId(TrainData);
					TrainData[trainId] = (
						new()
						{
							AfterArrive = trainData.AfterArrive,
							AfterArrive_OnStationTrackCol = trainData.AfterArrive_OnStationTrackCol,
							AfterRemarks = trainData.AfterRemarks,
							BeforeDeparture = trainData.BeforeDeparture,
							BeforeDeparture_OnStationTrackCol = trainData.BeforeDeparture_OnStationTrackCol,
							BeginRemarks = trainData.BeginRemarks,
							CarCount = trainData.CarCount,
							ColorId = -1, // Not Implemented
							DayCount = trainData.DayCount,
							Destination = trainData.Destination,
							Id = trainData.Id!,
							Direction = trainData.Direction,
							IsRideOnMoving = trainData.IsRideOnMoving,
							MaxSpeed = trainData.MaxSpeed,
							NominalTractiveCapacity = trainData.NominalTractiveCapacity,
							Remarks = trainData.Remarks,
							SpeedType = trainData.SpeedType,
							TrainInfo = trainData.TrainInfo,
							TrainNumber = trainData.TrainNumber,
							WorkId = workId,
							WorkType = trainData.WorkType,
						},
						trainData.TimetableRows.Select((v, i) => new TimetableRow(
							Id: v.Id ?? i.ToString(),
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
						)).ToArray()
					);
					WorkIdByTrainId[trainId] = workId;
				}
			}
		}
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
	public static LoaderJson InitFromBytes(ReadOnlySpan<byte> json)
	{
		WorkGroupData[]? workGroups = JsonSerializer.Deserialize<WorkGroupData[]>(json, opts);

		return new LoaderJson(workGroups!);
	}

	public void Dispose()
	{
	}

	public Models.TrainData? GetTrainData(string trainId)
	{
		Work w = WorkData[WorkIdByTrainId[trainId]];
		var (t, timetableRow) = TrainData[trainId];

		return new Models.TrainData(
			Id: trainId,
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
			Rows: timetableRow,
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
		=> [.. WorkGroups.Values];

	public IReadOnlyList<Work> GetWorkList(string workGroupId)
		=> WorkData.Values.Where(v => v.WorkGroupId == workGroupId).ToArray();

	public IReadOnlyList<Models.DB.TrainData> GetTrainDataList(string workId)
		=> TrainData.Values.Where((v) => WorkIdByTrainId[v.Item1.Id] == workId).Select(v => v.Item1).ToArray();

	public IReadOnlyList<TrainDataGroup> GetTrainDataGroupList()
	{
		throw new NotImplementedException();
	}
}
