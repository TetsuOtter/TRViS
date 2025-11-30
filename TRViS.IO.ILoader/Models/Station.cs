namespace TRViS.IO.Models;

public record Station(
	string Id,
	string WorkGroupId,
	string Name,
	double Location,
	double? Location_Lon_deg = null,
	double? Location_Lat_deg = null,
	double? OnStationDetectRadius_m = null,
	string? FullName = null,
	StationRecordType? RecordType = null
) : IEquatable<Station>
{ }
