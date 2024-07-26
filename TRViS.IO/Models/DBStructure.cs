using SQLite;

namespace TRViS.IO.Models.DB;

public enum ContentType
{
	Text,
	URI,
	PNG,
	PDF,
	JPG,
}

public enum StationRecordType
{
	NormalStation_ShownOnETimetable,
	NormalStation_NotShownOnETimetable,
	InfoRow_ForAlmostTrain,
	InfoRow_ForSomeTrain,
}

[Table("work_group")]
public class WorkGroup : IEquatable<WorkGroup>
{
	[PrimaryKey, Column("id"), NotNull, Unique]
	public string Id { get; set; } = string.Empty;

	[Column("name"), NotNull]
	public string Name { get; set; } = string.Empty;

	[Column("db_version")]
	public int? DBVersion { get; set; } = 0;

	public bool Equals(WorkGroup? obj)
	{
		if (obj is null)
			return false;

		return (
			Id == obj.Id
			&&
			Name == obj.Name
			&&
			DBVersion == obj.DBVersion
		);
	}
	public override bool Equals(object? obj)
		=> Equals(obj as WorkGroup);

	public override int GetHashCode()
		=> HashCode.Combine(Id, Name, DBVersion);

	public override string ToString()
		=> $"WorkGroup(Id={Id}, Name={Name}, DBVersion={DBVersion})";
}

[Table("work")]
public class Work : IHasRemarksProperty, IEquatable<Work>
{
	[PrimaryKey, Column("id"), NotNull, Unique]
	public string Id { get; set; } = string.Empty;

	[Column("work_group_id"), NotNull]
	public string WorkGroupId { get; set; } = string.Empty;

	[Column("name"), NotNull]
	public string Name { get; set; } = string.Empty;

	[Column("affect_date"), NotNull]
	public string AffectDate { get; set; } = string.Empty;

	[Column("affix_content_type")]
	public int? AffixContentType { get; set; }

	[Column("affix_content")]
	public byte[]? AffixContent { get; set; }

	[Column("remarks")]
	public string? Remarks { get; set; }

	[Column("has_e_train_timetable")]
	public bool? HasETrainTimetable { get; set; }

	[Column("e_train_timetable_content_type")]
	public int? ETrainTimetableContentType { get; set; }

	[Column("e_train_timetable_content")]
	public byte[]? ETrainTimetableContent { get; set; }

	public bool Equals(Work? obj)
	{
		if (obj is null)
			return false;

		return (
			Id == obj.Id
			&&
			WorkGroupId == obj.WorkGroupId
			&&
			Name == obj.Name
			&&
			AffectDate == obj.AffectDate
			&&
			AffixContentType == obj.AffixContentType
			&&
			Utils.IsArrayEquals(AffixContent, obj.AffixContent)
			&&
			Remarks == obj.Remarks
			&&
			HasETrainTimetable == obj.HasETrainTimetable
			&&
			ETrainTimetableContentType == obj.ETrainTimetableContentType
			&&
			Utils.IsArrayEquals(ETrainTimetableContent, obj.ETrainTimetableContent)
		);
	}

	public override bool Equals(object? obj)
		=> Equals(obj as Work);

	public override int GetHashCode()
	{
		HashCode hashCode = new();
		hashCode.Add(Id);
		hashCode.Add(WorkGroupId);
		hashCode.Add(Name);
		hashCode.Add(AffectDate);
		hashCode.Add(AffixContentType);
		if (AffixContent is not null)
			hashCode.AddBytes(AffixContent.AsSpan());
		else
			hashCode.Add(AffixContent);
		hashCode.Add(Remarks);
		hashCode.Add(HasETrainTimetable);
		hashCode.Add(ETrainTimetableContentType);
		if (AffixContent is not null)
			hashCode.AddBytes(ETrainTimetableContent.AsSpan());
		else
			hashCode.Add(ETrainTimetableContent);
		return hashCode.ToHashCode();
	}

	public override string ToString()
		=> $"Work[{WorkGroupId} / {Id}](Name='{Name}', AffectDate={AffectDate}, AffixContentType={AffixContentType}(len:{AffixContent?.Length}), Remarks='{Remarks}', HasETrainTimetable={HasETrainTimetable}(ContentType={ETrainTimetableContentType}, len={ETrainTimetableContent?.Length}))";
}

[Table("station")]
public class Station : IEquatable<Station>
{
	[PrimaryKey, Column("id"), NotNull, Unique]
	public string Id { get; set; } = string.Empty;

	[Column("work_group_id"), NotNull]
	public string WorkGroupId { get; set; } = string.Empty;

	[Column("name"), NotNull]
	public string Name { get; set; } = string.Empty;

	[Column("location"), NotNull]
	public double Location { get; set; }

	[Column("location_lon_deg")]
	public double? Location_Lon_deg { get; set; }

	[Column("location_lat_deg")]
	public double? Location_Lat_deg { get; set; }

	[Column("on_station_detect_radius_m")]
	public double? OnStationDetectRadius_m { get; set; }

	[Column("full_name")]
	public string? FullName { get; set; }

	[Column("record_type")]
	public int? RecordType { get; set; }

	public bool Equals(Station? obj)
	{
		if (obj is null)
			return false;

		return (
			Id == obj.Id
			&&
			WorkGroupId == obj.WorkGroupId
			&&
			Name == obj.Name
			&&
			Location == obj.Location
			&&
			Location_Lon_deg == obj.Location_Lon_deg
			&&
			Location_Lat_deg == obj.Location_Lat_deg
			&&
			OnStationDetectRadius_m == obj.OnStationDetectRadius_m
			&&
			FullName == obj.FullName
			&&
			RecordType == obj.RecordType
		);
	}

	public override bool Equals(object? obj)
		=> Equals(obj as Station);

	public override int GetHashCode()
	{
		HashCode hashCode = new();
		hashCode.Add(Id);
		hashCode.Add(WorkGroupId);
		hashCode.Add(Name);
		hashCode.Add(Location);
		hashCode.Add(Location_Lon_deg);
		hashCode.Add(Location_Lat_deg);
		hashCode.Add(OnStationDetectRadius_m);
		hashCode.Add(FullName);
		hashCode.Add(RecordType);
		return hashCode.ToHashCode();
	}

	public override string ToString()
		=> $"Station[{WorkGroupId} / {Id}](Name='{Name}'(FullName='{FullName}', RecordType={RecordType}), Location={Location}({Location_Lon_deg}, {Location_Lat_deg}) with {OnStationDetectRadius_m}[m])";
}

[Table("station_track")]
public class StationTrack : IEquatable<StationTrack>
{
	[PrimaryKey, Column("id"), NotNull, Unique]
	public string Id { get; set; } = string.Empty;

	[Column("station_id"), NotNull]
	public string StationId { get; set; } = string.Empty;

	[Column("name"), NotNull]
	public string Name { get; set; } = string.Empty;

	public bool Equals(StationTrack? obj)
	{
		if (obj is null)
			return false;

		return (
			Id == obj.Id
			&&
			StationId == obj.StationId
			&&
			Name == obj.Name
		);
	}

	public override bool Equals(object? obj)
		=> Equals(obj as StationTrack);

	public override int GetHashCode()
	 => HashCode.Combine(Id, StationId, Name);

	public override string ToString()
		=> $"StationTrack[{StationId} / {Id}](Name='{Name}')";
}

[Table("train_data")]
public class TrainData : IHasRemarksProperty, IEquatable<TrainData>
{
	[PrimaryKey, Column("id"), NotNull, Unique]
	public string Id { get; set; } = string.Empty;

	[Column("work_id"), NotNull]
	public string WorkId { get; set; } = string.Empty;

	[Column("train_number"), NotNull]
	public string TrainNumber { get; set; } = string.Empty;

	[Column("max_speed")]
	public string? MaxSpeed { get; set; }

	[Column("speed_type")]
	public string? SpeedType { get; set; }

	[Column("ntc")]
	public string? NominalTractiveCapacity { get; set; }

	[Column("car_count")]
	public int? CarCount { get; set; }

	[Column("destination")]
	public string? Destination { get; set; }

	[Column("begin_remarks")]
	public string? BeginRemarks { get; set; }

	[Column("after_remarks")]
	public string? AfterRemarks { get; set; }

	[Column("remarks")]
	public string? Remarks { get; set; }

	[Column("before_departure")]
	public string? BeforeDeparture { get; set; }

	[Column("train_info")]
	public string? TrainInfo { get; set; }

	[Column("direction"), NotNull]
	public int Direction { get; set; }

	[Column("work_type")]
	public int? WorkType { get; set; }

	[Column("after_arrive")]
	public string? AfterArrive { get; set; }

	[Column("before_departure_on_station_track_col")]
	public string? BeforeDeparture_OnStationTrackCol { get; set; }

	[Column("after_arrive_on_station_track_col")]
	public string? AfterArrive_OnStationTrackCol { get; set; }

	[Column("day_count")]
	public int? DayCount { get; set; }

	[Column("is_ride_on_moving")]
	public bool? IsRideOnMoving { get; set; }

	[Column("color_id")]
	public int? ColorId { get; set; }

	public bool Equals(TrainData? obj)
	{
		if (obj is null)
			return false;

		return (
			Id == obj.Id
			&&
			WorkId == obj.WorkId
			&&
			TrainNumber == obj.TrainNumber
			&&
			MaxSpeed == obj.MaxSpeed
			&&
			SpeedType == obj.SpeedType
			&&
			NominalTractiveCapacity == obj.NominalTractiveCapacity
			&&
			CarCount == obj.CarCount
			&&
			Destination == obj.Destination
			&&
			BeginRemarks == obj.BeginRemarks
			&&
			AfterRemarks == obj.AfterRemarks
			&&
			Remarks == obj.Remarks
			&&
			BeforeDeparture == obj.BeforeDeparture
			&&
			TrainInfo == obj.TrainInfo
			&&
			Direction == obj.Direction
			&&
			WorkType == obj.WorkType
			&&
			AfterArrive == obj.AfterArrive
			&&
			BeforeDeparture_OnStationTrackCol == obj.BeforeDeparture_OnStationTrackCol
			&&
			AfterArrive_OnStationTrackCol == obj.AfterArrive_OnStationTrackCol
			&&
			DayCount == obj.DayCount
			&&
			IsRideOnMoving == obj.IsRideOnMoving
			&&
			ColorId == obj.ColorId
		);
	}
	
	public override bool Equals(object? obj)
		=> Equals(obj as TrainData);

	public override int GetHashCode()
	{
		HashCode hashCode = new();
		hashCode.Add(Id);
		hashCode.Add(WorkId);
		hashCode.Add(TrainNumber);
		hashCode.Add(MaxSpeed);
		hashCode.Add(SpeedType);
		hashCode.Add(NominalTractiveCapacity);
		hashCode.Add(CarCount);
		hashCode.Add(Destination);
		hashCode.Add(BeginRemarks);
		hashCode.Add(AfterRemarks);
		hashCode.Add(Remarks);
		hashCode.Add(BeforeDeparture);
		hashCode.Add(TrainInfo);
		hashCode.Add(Direction);
		hashCode.Add(WorkType);
		hashCode.Add(AfterArrive);
		hashCode.Add(BeforeDeparture_OnStationTrackCol);
		hashCode.Add(AfterArrive_OnStationTrackCol);
		hashCode.Add(DayCount);
		hashCode.Add(IsRideOnMoving);
		hashCode.Add(ColorId);
		return hashCode.ToHashCode();
	}

	public override string ToString()
		=> $"TrainData[{WorkId} / {Id}](TrainNumber='{TrainNumber}', MaxSpeed='{MaxSpeed}', SpeedType='{SpeedType}', NominalTractiveCapacity='{NominalTractiveCapacity}', CarCount={CarCount}, Destination='{Destination}', BeginRemarks='{BeginRemarks}', AfterRemarks='{AfterRemarks}', Remarks='{Remarks}', BeforeDeparture='{BeforeDeparture}', TrainInfo='{TrainInfo}', Direction={Direction}, WorkType={WorkType}, AfterArrive='{AfterArrive}', BeforeDeparture_OnStationTrackCol='{BeforeDeparture_OnStationTrackCol}', AfterArrive_OnStationTrackCol='{AfterArrive_OnStationTrackCol}', DayCount={DayCount}, IsRideOnMoving={IsRideOnMoving}, ColorId={ColorId})";
}

[Table("timetable_row")]
public class TimetableRowData : IEquatable<TimetableRowData>
{
	[PrimaryKey, Column("id"), NotNull, Unique]
	public string Id { get; set; } = string.Empty;

	[Column("train_id"), NotNull]
	[Indexed(Name = "sqlite_autoindex_timetablerow_1", Order = 1, Unique = true)]
	public string TrainId { get; set; } = string.Empty;

	[Column("station_id"), NotNull]
	[Indexed(Name = "sqlite_autoindex_timetablerow_1", Order = 2, Unique = true)]
	public string StationId { get; set; } = string.Empty;

	[Column("drive_time_mm")]
	public int? DriveTime_MM { get; set; }
	[Column("drive_time_ss")]
	public int? DriveTime_SS { get; set; }

	[Column("is_operation_only_stop")]
	public bool? IsOperationOnlyStop { get; set; }
	[Column("is_pass")]
	public bool? IsPass { get; set; }
	[Column("has_bracket")]
	public bool? HasBracket { get; set; }
	[Column("is_last_stop")]
	public bool? IsLastStop { get; set; }

	[Column("arrive_hh")]
	public int? Arrive_HH { get; set; }
	[Column("arrive_mm")]
	public int? Arrive_MM { get; set; }
	[Column("arrive_ss")]
	public int? Arrive_SS { get; set; }
	[Column("arrive_str")]
	public string? Arrive_Str { get; set; }

	[Column("departure_hh")]
	public int? Departure_HH { get; set; }
	[Column("departure_mm")]
	public int? Departure_MM { get; set; }
	[Column("departure_ss")]
	public int? Departure_SS { get; set; }
	[Column("departure_str")]
	public string? Departure_Str { get; set; }

	[Column("station_track_id")]
	public string? StationTrackId { get; set; }

	[Column("run_in_limit")]
	public int? RunInLimit { get; set; }
	[Column("run_out_limit")]
	public int? RunOutLimit { get; set; }

	[Column("remarks")]
	public string? Remarks { get; set; }

	[Column("marker_color_id")]
	public string? MarkerColorId { get; set; }

	[Column("marker_text")]
	public string? MarkerText { get; set; }

	[Column("work_type")]
	public int? WorkType { get; set; }

	public bool Equals(TimetableRowData? obj)
	{
		if (obj is null)
			return false;

		return (
			Id == obj.Id
			&&
			TrainId == obj.TrainId
			&&
			StationId == obj.StationId
			&&
			DriveTime_MM == obj.DriveTime_MM
			&&
			DriveTime_SS == obj.DriveTime_SS
			&&
			IsOperationOnlyStop == obj.IsOperationOnlyStop
			&&
			IsPass == obj.IsPass
			&&
			HasBracket == obj.HasBracket
			&&
			IsLastStop == obj.IsLastStop
			&&
			Arrive_HH == obj.Arrive_HH
			&&
			Arrive_MM == obj.Arrive_MM
			&&
			Arrive_SS == obj.Arrive_SS
			&&
			Arrive_Str == obj.Arrive_Str
			&&
			Departure_HH == obj.Departure_HH
			&&
			Departure_MM == obj.Departure_MM
			&&
			Departure_SS == obj.Departure_SS
			&&
			Departure_Str == obj.Departure_Str
			&&
			StationTrackId == obj.StationTrackId
			&&
			RunInLimit == obj.RunInLimit
			&&
			RunOutLimit == obj.RunOutLimit
			&&
			Remarks == obj.Remarks
			&&
			MarkerColorId == obj.MarkerColorId
			&&
			MarkerText == obj.MarkerText
			&&
			WorkType == obj.WorkType
		);
	}

	public override bool Equals(object? obj)
		=> Equals(obj as TimetableRowData);

	public override int GetHashCode()
	{
		HashCode hashCode = new();
		hashCode.Add(Id);
		hashCode.Add(TrainId);
		hashCode.Add(StationId);
		hashCode.Add(DriveTime_MM);
		hashCode.Add(DriveTime_SS);
		hashCode.Add(IsOperationOnlyStop);
		hashCode.Add(IsPass);
		hashCode.Add(HasBracket);
		hashCode.Add(IsLastStop);
		hashCode.Add(Arrive_HH);
		hashCode.Add(Arrive_MM);
		hashCode.Add(Arrive_SS);
		hashCode.Add(Arrive_Str);
		hashCode.Add(Departure_HH);
		hashCode.Add(Departure_MM);
		hashCode.Add(Departure_SS);
		hashCode.Add(Departure_Str);
		hashCode.Add(StationTrackId);
		hashCode.Add(RunInLimit);
		hashCode.Add(RunOutLimit);
		hashCode.Add(Remarks);
		hashCode.Add(MarkerColorId);
		hashCode.Add(MarkerText);
		hashCode.Add(WorkType);
		return hashCode.ToHashCode();
	}

	public override string ToString()
		=> $"TimetableRowData[{TrainId} / {Id}](StationId={StationId}, DriveTime={DriveTime_MM}:{DriveTime_SS}, IsOperationOnlyStop={IsOperationOnlyStop}, IsPass={IsPass}, HasBracket={HasBracket}, IsLastStop={IsLastStop}, Arrive={Arrive_HH}:{Arrive_MM}:{Arrive_SS}({Arrive_Str}), Departure={Departure_HH}:{Departure_MM}:{Departure_SS}({Departure_Str}), StationTrackId={StationTrackId}, RunInLimit={RunInLimit}, RunOutLimit={RunOutLimit}, Remarks='{Remarks}', MarkerColorId={MarkerColorId}, MarkerText='{MarkerText}', WorkType={WorkType})";
}

[Table("language")]
public class Language : IEquatable<Language>
{
	[PrimaryKey, Column("id"), NotNull, Unique]
	public string Id { get; set; } = string.Empty;

	[Column("language_code"), NotNull, Unique]
	public string LanguageCode { get; set; } = string.Empty;

	public bool Equals(Language? obj)
	{
		if (obj is null)
			return false;

		return (
			Id == obj.Id
			&&
			LanguageCode == obj.LanguageCode
		);
	}

	public override bool Equals(object? obj)
		=> Equals(obj as Language);

	public override int GetHashCode()
		=> HashCode.Combine(Id, LanguageCode);

	public override string ToString()
		=> $"Language(Id={Id}, LanguageCode='{LanguageCode}')";
}

[Table("work_group_name_other_lang")]
public class WorkGroupNameOtherLang : IEquatable<WorkGroupNameOtherLang>
{
	[Column("work_group_id"), NotNull]
	[Indexed(Name = "sqlite_autoindex_workgroupnameotherlang_1", Order = 1, Unique = true)]
	public string WorkGroupId { get; set; } = string.Empty;

	[Column("language_id"), NotNull]
	[Indexed(Name = "sqlite_autoindex_workgroupnameotherlang_1", Order = 2, Unique = true)]
	public string LanguageId { get; set; } = string.Empty;

	[Column("name"), NotNull]
	public string Name { get; set; } = string.Empty;

	public bool Equals(WorkGroupNameOtherLang? obj)
	{
		if (obj is null)
			return false;

		return (
			WorkGroupId == obj.WorkGroupId
			&&
			LanguageId == obj.LanguageId
			&&
			Name == obj.Name
		);
	}

	public override bool Equals(object? obj)
		=> Equals(obj as WorkGroupNameOtherLang);

	public override int GetHashCode()
		=> HashCode.Combine(WorkGroupId, LanguageId, Name);

	public override string ToString()
		=> $"WorkGroupNameOtherLang[{WorkGroupId} / {LanguageId}](Name='{Name}')";
}

[Table("work_name_other_lang")]
public class WorkNameOtherLang : IEquatable<WorkNameOtherLang>
{
	[Column("work_id"), NotNull]
	[Indexed(Name = "sqlite_autoindex_worknameotherlang_1", Order = 1, Unique = true)]
	public string WorkId { get; set; } = string.Empty;

	[Column("language_id"), NotNull]
	[Indexed(Name = "sqlite_autoindex_worknameotherlang_1", Order = 2, Unique = true)]
	public string LanguageId { get; set; } = string.Empty;

	[Column("name"), NotNull]
	public string Name { get; set; } = string.Empty;

	public bool Equals(WorkNameOtherLang? obj)
	{
		if (obj is null)
			return false;

		return (
			WorkId == obj.WorkId
			&&
			LanguageId == obj.LanguageId
			&&
			Name == obj.Name
		);
	}

	public override bool Equals(object? obj)
		=> Equals(obj as WorkNameOtherLang);

	public override int GetHashCode()
		=> HashCode.Combine(WorkId, LanguageId, Name);

	public override string ToString()
		=> $"WorkNameOtherLang[{WorkId} / {LanguageId}](Name='{Name}')";
}

[Table("station_name_other_lang")]
public class StationNameOtherLang : IEquatable<StationNameOtherLang>
{
	[Column("station_id"), NotNull]
	[Indexed(Name = "sqlite_autoindex_stationnameotherlang_1", Order = 1, Unique = true)]
	public string StationId { get; set; } = string.Empty;

	[Column("language_id"), NotNull]
	[Indexed(Name = "sqlite_autoindex_stationnameotherlang_1", Order = 2, Unique = true)]
	public string LanguageId { get; set; } = string.Empty;

	[Column("name"), NotNull]
	public string Name { get; set; } = string.Empty;

	[Column("full_name")]
	public string? FullName { get; set; }

	public bool Equals(StationNameOtherLang? obj)
	{
		if (obj is null)
			return false;

		return (
			StationId == obj.StationId
			&&
			LanguageId == obj.LanguageId
			&&
			Name == obj.Name
			&&
			FullName == obj.FullName
		);
	}

	public override bool Equals(object? obj)
		=> Equals(obj as StationNameOtherLang);

	public override int GetHashCode()
		=> HashCode.Combine(StationId, LanguageId, Name, FullName);

	public override string ToString()
		=> $"StationNameOtherLang[{StationId} / {LanguageId}](Name='{Name}', FullName='{FullName}')";
}

[Table("station_track_name_other_lang")]
public class StationTrackNameOtherLang : IEquatable<StationTrackNameOtherLang>
{
	[Column("station_track_id"), NotNull]
	[Indexed(Name = "sqlite_autoindex_stationtracknameotherlang_1", Order = 1, Unique = true)]
	public string StationTrackId { get; set; } = string.Empty;

	[Column("language_id"), NotNull]
	[Indexed(Name = "sqlite_autoindex_stationtracknameotherlang_1", Order = 2, Unique = true)]
	public string LanguageId { get; set; } = string.Empty;

	[Column("name"), NotNull]
	public string Name { get; set; } = string.Empty;

	public bool Equals(StationTrackNameOtherLang? obj)
	{
		if (obj is null)
			return false;

		return (
			StationTrackId == obj.StationTrackId
			&&
			LanguageId == obj.LanguageId
			&&
			Name == obj.Name
		);
	}

	public override bool Equals(object? obj)
		=> Equals(obj as StationTrackNameOtherLang);

	public override int GetHashCode()
		=> HashCode.Combine(StationTrackId, LanguageId, Name);

	public override string ToString()
		=> $"StationTrackNameOtherLang[{StationTrackId} / {LanguageId}](Name='{Name}')";
}

[Table("color")]
public class Color : IEquatable<Color>
{
	[PrimaryKey, Column("id"), NotNull, Unique]
	public string Id { get; set; } = string.Empty;

	[Column("name"), NotNull]
	public string Name { get; set; } = string.Empty;

	[Column("rgb"), NotNull]
	public int RGB { get; set; }

	public bool Equals(Color? obj)
	{
		if (obj is null)
			return false;

		return (
			Id == obj.Id
			&&
			Name == obj.Name
			&&
			RGB == obj.RGB
		);
	}

	public override bool Equals(object? obj)
		=> Equals(obj as Color);

	public override int GetHashCode()
		=> HashCode.Combine(Id, Name, RGB);

	public override string ToString()
		=> $"Color[{Id}](Name='{Name}', RGB={RGB:X6})";
}
