using System.Text.Json;
using System.Text.RegularExpressions;

using TRViS.IO.Models;
using TRViS.JsonModels;

namespace TRViS.IO;

public class LoaderJson : ILoader
{
	Dictionary<string, WorkGroup> WorkGroups { get; } = [];
	Dictionary<string, Work> WorkData { get; } = [];
	Dictionary<string, Models.TrainData> TrainData { get; } = [];

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
	static string GenerateUniqueId(IReadOnlyList<string> idList)
	{
		for (int i = 0; i < 100; i++)
		{
			string tmpId = Guid.NewGuid().ToString();
			if (!idList.Contains(tmpId))
				return tmpId;
		}
		throw new InvalidOperationException("Failed to generate a unique ID");
	}
	static string GenerateUniqueId(IEnumerable<string> idList)
	{
		HashSet<string> idSet = idList is HashSet<string> hs ? hs : [.. idList];
		for (int i = 0; i < 100; i++)
		{
			string tmpId = Guid.NewGuid().ToString();
			if (!idSet.Contains(tmpId))
				return tmpId;
		}
		throw new InvalidOperationException("Failed to generate a unique ID");
	}

	static readonly JsonSerializerOptions opts = new()
	{
		AllowTrailingCommas = true,
		PropertyNameCaseInsensitive = true,
	};

	private LoaderJson(WorkGroupData[] workGroups)
	{
		if (workGroups is null)
			throw new ArgumentNullException(nameof(workGroups));

		Dictionary<WorkGroupData, string> workGroupIdDict = [];
		Dictionary<WorkData, string> workIdDict = [];
		Dictionary<JsonModels.TrainData, string> trainIdDict = [];
		for (int i = 0; i < workGroups.Length; i++)
		{
			string? workGroupId = workGroups[i].Id;
			workGroupIdDict[workGroups[i]] = string.IsNullOrEmpty(workGroupId) ? GenerateUniqueId(workGroupIdDict.Values) : workGroupId;

			for (int j = 0; j < workGroups[i].Works.Length; j++)
			{
				WorkData workData = workGroups[i].Works[j];
				string? workId = workData.Id;
				workIdDict[workData] = string.IsNullOrEmpty(workId) ? GenerateUniqueId(workIdDict.Values) : workId;

				for (int k = 0; k < workData.Trains.Length; k++)
				{
					JsonModels.TrainData trainData = workData.Trains[k];
					string? trainId = trainData.Id;
					trainIdDict[trainData] = string.IsNullOrEmpty(trainId) ? GenerateUniqueId(trainIdDict.Values) : trainId;

					// TimetableRowIdは内部で使用しないため、ここでの生成は不要
				}
			}
		}

		for (int workGroupIndex = 0; workGroupIndex < workGroups.Length; workGroupIndex++)
		{
			WorkGroupData workGroup = workGroups[workGroupIndex];
			string workGroupId = workGroupIdDict[workGroup];
			WorkGroups[workGroupId] = new(
				Id: workGroupId,
				Name: workGroup.Name,
				DBVersion: workGroup.DBVersion
			);
			System.Diagnostics.Debug.WriteLine($"WorkGroup: {workGroupId} {workGroup.Name}");

			WorkData[] workList = workGroup.Works;
			for (int workIndex = 0; workIndex < workList.Length; workIndex++)
			{
				WorkData workData = workList[workIndex];
				string workId = workIdDict[workData];
				WorkData[workId] = new(
					WorkGroupId: workGroupId,
					Id: workId,
					Name: workData.Name,

					AffectDate: Utils.StringToDateOnlyOrNull(workData.AffectDate),
					AffixContent: null, // workData.AffixContent,
					AffixContentType: workData.AffixContentType,
					ETrainTimetableContent: null, // workData.ETrainTimetableContent,
					ETrainTimetableContentType: workData.ETrainTimetableContentType,
					HasETrainTimetable: workData.HasETrainTimetable,
					Remarks: workData.Remarks
				);
				WorkGroupIdByWorkId[workId] = workGroupId;
				System.Diagnostics.Debug.WriteLine($"\tWork: {workId} {workData.Name} (WorkGroupId: {workGroupId})");

				JsonModels.TrainData[] trainList = workData.Trains;
				for (int trainIndex = 0; trainIndex < trainList.Length; trainIndex++)
				{
					JsonModels.TrainData trainData = trainList[trainIndex];
					string trainId = trainIdDict[trainData];
					TimetableRow[] rows = [.. trainData.TimetableRows.Select(static (v, i) => new TimetableRow(
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

						DefaultMarkerColor_RGB: Utils.HexStringToRgbInt(v.MarkerColor),
						DefaultMarkerText: v.MarkerText
					))];
					TrainData[trainId] = new(
							AfterArrive: trainData.AfterArrive,
							AfterRemarks: trainData.AfterRemarks,
							BeforeDeparture: trainData.BeforeDeparture,
							BeginRemarks: trainData.BeginRemarks,
							CarCount: trainData.CarCount,
							LineColor_RGB: null, // Not Implemented
							DayCount: trainData.DayCount ?? 0,
							Destination: trainData.Destination,
							Id: trainId,
							Direction: trainData.Direction < 0 ? Direction.Inbound : Direction.Outbound,
							IsRideOnMoving: trainData.IsRideOnMoving,
							MaxSpeed: trainData.MaxSpeed,
							NominalTractiveCapacity: trainData.NominalTractiveCapacity,
							Remarks: trainData.Remarks,
							SpeedType: trainData.SpeedType,
							TrainInfo: trainData.TrainInfo,
							TrainNumber: trainData.TrainNumber,
							// WorkType: trainData.WorkType,
							WorkName: workData.Name,
							AffectDate: Utils.StringToDateOnlyOrNull(workData.AffectDate),
							// NextTrainId logic:
							// - If explicitly set to empty string: no next train (null)
							// - If explicitly set to a value: use that value
							// - If not set (null): use default behavior (next train in list)
							NextTrainId: trainData.NextTrainId is not null
								? (trainData.NextTrainId == "" ? null : trainData.NextTrainId)
								: (trainIndex != trainList.Length - 1 ? trainIdDict[trainList[trainIndex + 1]] : null),
							Rows: rows
						);
					System.Diagnostics.Debug.WriteLine($"\t\tTrain: {trainId} {trainData.TrainNumber} (WorkId: {workId})");
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

	public Models.TrainData? GetTrainData(string trainId) => TrainData[trainId];

	public IReadOnlyList<WorkGroup> GetWorkGroupList()
		=> [.. WorkGroups.Values];

	public IReadOnlyList<Work> GetWorkList(string workGroupId)
		=> [.. WorkData.Values.Where(v => v.WorkGroupId == workGroupId)];

	public IReadOnlyList<Models.TrainData> GetTrainDataList(string workId)
		=> [.. TrainData.Values.Where((v) => WorkIdByTrainId[v.Id] == workId)];
}
