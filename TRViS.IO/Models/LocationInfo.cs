namespace TRViS.IO.Models;

public record LocationInfo(
	double Location_m,
	double? Longitude_deg,
	double? Latitude_deg,
	double? OnStationDetectRadius_m
)
{
}
