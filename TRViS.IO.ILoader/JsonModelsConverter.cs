using System.Text.RegularExpressions;

using TRViS.IO.Models;

using JsonModels = TRViS.JsonModels;

namespace TRViS.IO;

/// <summary>
/// JsonModels の型を TRViS.IO.Models の型に変換するユーティリティ
/// </summary>
public static partial class JsonModelsConverter
{
	private static readonly Regex TimePatternRegex = TimePatternRegexGenerator();

	/// <summary>
	/// JsonModels.WorkGroupData を TRViS.IO.Models.WorkGroup に変換します
	/// </summary>
	public static WorkGroup ConvertWorkGroup(JsonModels.WorkGroupData workGroupJson)
	{
		string workGroupId = string.IsNullOrEmpty(workGroupJson.Id)
			? Guid.NewGuid().ToString()
			: workGroupJson.Id;

		return new WorkGroup(
			Id: workGroupId,
			Name: workGroupJson.Name,
			DBVersion: workGroupJson.DBVersion
		);
	}

	/// <summary>
	/// JsonModels.WorkData[] を TRViS.IO.Models.Work[] に変換します
	/// </summary>
	public static Work[] ConvertWorks(JsonModels.WorkData[] worksJson, string workGroupId)
	{
		var works = new List<Work>();

		foreach (var jsonWork in worksJson)
		{
			works.Add(ConvertWork(jsonWork, workGroupId));
		}

		return [.. works];
	}

	/// <summary>
	/// JsonModels.WorkData を TRViS.IO.Models.Work に変換します
	/// </summary>
	public static Work ConvertWork(JsonModels.WorkData jsonWork, string workGroupId)
	{
		string workId = string.IsNullOrEmpty(jsonWork.Id)
			? Guid.NewGuid().ToString()
			: jsonWork.Id;

		DateOnly? affectDate = null;
		if (!string.IsNullOrEmpty(jsonWork.AffectDate))
		{
			affectDate = DateOnly.Parse(jsonWork.AffectDate);
		}

		return new(
			Id: workId,
			WorkGroupId: workGroupId,
			Name: jsonWork.Name,
			AffectDate: affectDate,
			AffixContentType: jsonWork.AffixContentType,
			AffixContent: null,  // JSONには含まれない
			Remarks: jsonWork.Remarks,
			HasETrainTimetable: jsonWork.HasETrainTimetable,
			ETrainTimetableContentType: jsonWork.ETrainTimetableContentType,
			ETrainTimetableContent: null  // JSONには含まれない
		);
	}

	/// <summary>
	/// JsonModels.TrainData を TRViS.IO.Models.TrainData に変換します
	/// </summary>
	public static TrainData ConvertTrain(JsonModels.TrainData trainJson)
	{
		string trainId = string.IsNullOrEmpty(trainJson.Id)
			? Guid.NewGuid().ToString()
			: trainJson.Id;

		// TimetableRowsを変換
		TimetableRow[]? rows = null;
		if (trainJson.TimetableRows is not null && trainJson.TimetableRows.Length > 0)
		{
			rows = [.. trainJson.TimetableRows.Select((v, i) => new TimetableRow(
				Id: v.Id ?? i.ToString(),
				Location: new(v.Location_m, v.Longitude_deg, v.Latitude_deg, v.OnStationDetectRadius_m),
				DriveTimeMM: v.DriveTime_MM,
				DriveTimeSS: v.DriveTime_SS,
				StationName: v.StationName,
				IsOperationOnlyStop: v.IsOperationOnlyStop ?? false,
				IsPass: v.IsPass ?? false,
				HasBracket: v.HasBracket ?? false,
				IsLastStop: v.IsLastStop ?? false,
				ArriveTime: ConvertTimeData(v.Arrive),
				DepartureTime: ConvertTimeData(v.Departure),
				TrackName: v.TrackName,
				RunInLimit: v.RunInLimit,
				RunOutLimit: v.RunOutLimit,
				Remarks: v.Remarks,
				IsInfoRow: false,  // JSONModelsにはRecordTypeが含まれない
				DefaultMarkerColor_RGB: null,
				DefaultMarkerText: null
			))];
		}

		return new TrainData(
			Id: trainId,
			Direction: trainJson.Direction < 0 ? Direction.Inbound : Direction.Outbound,
			TrainNumber: trainJson.TrainNumber,
			MaxSpeed: trainJson.MaxSpeed,
			SpeedType: trainJson.SpeedType,
			NominalTractiveCapacity: trainJson.NominalTractiveCapacity,
			CarCount: trainJson.CarCount,
			Destination: trainJson.Destination,
			BeginRemarks: trainJson.BeginRemarks,
			AfterRemarks: trainJson.AfterRemarks,
			Remarks: trainJson.Remarks,
			BeforeDeparture: trainJson.BeforeDeparture,
			TrainInfo: trainJson.TrainInfo,
			Rows: rows,
			AfterArrive: trainJson.AfterArrive,
			DayCount: trainJson.DayCount ?? 0,
			IsRideOnMoving: trainJson.IsRideOnMoving
		);
	}

	/// <summary>
	/// "HH:MM:SS"形式の時刻文字列をTimeDataに変換します
	/// </summary>
	private static TimeData ConvertTimeData(string? timeStr)
	{
		static int? ToIntOrNull(string s)
			=> int.TryParse(s, out int v) ? v : null;

		if (string.IsNullOrEmpty(timeStr))
			return new TimeData(null, null, null, null);

		if (!TimePatternRegex.IsMatch(timeStr))
			return new TimeData(null, null, null, timeStr);

		string[] hhmmss = timeStr.Split(':', StringSplitOptions.None);

		return new TimeData(
			ToIntOrNull(hhmmss[0]),
			ToIntOrNull(hhmmss[1]),
			ToIntOrNull(hhmmss[2]),
			null
		);
	}

	[GeneratedRegex("^[0-9]{0,2}:[0-9]{0,2}:[0-9]{0,2}$", RegexOptions.Compiled)]
	private static partial Regex TimePatternRegexGenerator();
}
