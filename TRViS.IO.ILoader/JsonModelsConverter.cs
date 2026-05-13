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

		DateOnly? affectDate = StringToDateOnlyUtil.StringToDateOnlyOrNull(jsonWork.AffectDate);
		// JSON 上の AffectDate が日付として解釈できなかった場合は、その文字列をそのまま
		// AffectDateText に格納する。空文字列は無視。
		string? affectDateText = (affectDate is null && !string.IsNullOrEmpty(jsonWork.AffectDate))
			? jsonWork.AffectDate
			: null;

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
			// JSON 上は base64 文字列。空・不正値は内容なしとして扱う。
			ETrainTimetableContent: DecodeBase64OrNull(jsonWork.ETrainTimetableContent),
			AffectDateText: affectDateText
		);
	}

	/// <remarks>
	/// Note: This method is duplicated in TRViS.IO.Loaders.LoaderJson due to the project
	/// dependency structure (TRViS.IO.ILoader cannot reference TRViS.IO).
	/// </remarks>
	private static byte[]? DecodeBase64OrNull(string? s)
	{
		if (string.IsNullOrEmpty(s))
			return null;
		try { return Convert.FromBase64String(s); }
		catch (FormatException) { return null; }
	}

	/// <summary>
	/// JsonModels.TrainData を TRViS.IO.Models.TrainData に変換します。
	/// 親 WorkData が存在しない場合の既定オーバーロード。
	/// WorkName / AffectDate は <c>null</c> になるので、可能な限り親情報を渡せるオーバーロードを使うこと。
	/// </summary>
	public static TrainData ConvertTrain(JsonModels.TrainData trainJson)
		=> ConvertTrain(trainJson, workName: null, affectDate: null);

	/// <summary>
	/// JsonModels.TrainData を TRViS.IO.Models.TrainData に変換します。
	/// 親 WorkData が手元にあるときは、その <see cref="JsonModels.WorkData.Name"/> /
	/// <see cref="JsonModels.WorkData.AffectDate"/> を引き継いで埋めます。
	/// JsonModels.TrainData 自体は WorkName / AffectDate を持たないため、
	/// 呼び出し側で親文脈を渡さない限り IO.Models.TrainData 側のこれらは null になる。
	/// LoaderJson の単一ファイルロードと挙動を揃えるためのフックです。
	/// </summary>
	public static TrainData ConvertTrain(JsonModels.TrainData trainJson, JsonModels.WorkData? parentWork)
		=> ConvertTrain(
			trainJson,
			workName: parentWork?.Name,
			affectDate: StringToDateOnlyUtil.StringToDateOnlyOrNull(parentWork?.AffectDate));

	/// <summary>
	/// JsonModels.TrainData を TRViS.IO.Models.TrainData に変換します。
	/// WorkName / AffectDate を呼び出し側で解決済みの値として渡したい場合に使います
	/// (例: Train スコープ単独更新で、既にキャッシュ済みの Work から値を引き継ぐ)。
	/// </summary>
	public static TrainData ConvertTrain(JsonModels.TrainData trainJson, string? workName, DateOnly? affectDate)
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
				IsInfoRow: v.RecordType
					is (int)StationRecordType.InfoRow_ForAlmostTrain
					or (int)StationRecordType.InfoRow_ForSomeTrain,
				DefaultMarkerColor_RGB: HexStringToRgbInt(v.MarkerColor),
				DefaultMarkerText: v.MarkerText
			))];
		}

		return new TrainData(
			Id: trainId,
			Direction: trainJson.Direction < 0 ? Direction.Inbound : Direction.Outbound,
			WorkName: workName,
			AffectDate: affectDate,
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
			IsRideOnMoving: trainJson.IsRideOnMoving,
			LineColor_RGB: HexStringToRgbInt(trainJson.Color),
			// Empty string == null: matches LoaderJson semantics (LoaderJson.cs) and the
			// IsNullOrEmpty checks in NextTrainButtonPresenter / VerticalTimetableView /
			// VerticalTimetableDataSourceAdapter so the button visibility is consistent
			// across loader implementations.
			NextTrainId: string.IsNullOrEmpty(trainJson.NextTrainId) ? null : trainJson.NextTrainId
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

	/// <summary>
	/// Converts a hex color string (e.g., "3366CC" or "CC") to an RGB integer.
	/// Returns null if the input is null, empty, or invalid.
	/// </summary>
	/// <remarks>
	/// Note: This method is duplicated in TRViS.IO.Utils.Utils due to the project dependency structure.
	/// TRViS.IO.ILoader cannot reference TRViS.IO to avoid circular dependencies.
	/// </remarks>
	private static int? HexStringToRgbInt(string? hexString)
	{
		if (string.IsNullOrEmpty(hexString))
			return null;

		// Remove leading '#' if present
		if (hexString.StartsWith('#'))
			hexString = hexString.Substring(1);

		if (int.TryParse(hexString, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out int result))
			return result;

		return null;
	}

	[GeneratedRegex("^[0-9]{0,2}:[0-9]{0,2}:[0-9]{0,2}$", RegexOptions.Compiled)]
	private static partial Regex TimePatternRegexGenerator();
}
