namespace TRViS.IO.Models;

public record LocationInfo(
	double Location_m,
	double? Longitude_deg = null,
	double? Latitude_deg = null,
	double? OnStationDetectRadius_m = null
)
{
}
