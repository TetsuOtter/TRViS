namespace TRViS.Services.LocationService;

internal static partial class Utils
{
	const double EARTH_RADIUS_m = 6378137;

	public static double DegToRad(in double deg)
		=> deg * Math.PI / 180.0;

	// Haversine Formula
	public static double CalculateDistance_m(in double lon1_deg, in double lat1_deg, in double lon2_deg, in double lat2_deg)
	{
		double lon1_rad = DegToRad(lon1_deg);
		double lat1_rad = DegToRad(lat1_deg);
		double lon2_rad = DegToRad(lon2_deg);
		double lat2_rad = DegToRad(lat2_deg);

		return EARTH_RADIUS_m * Math.Acos(
			Math.Sin(lat1_rad) * Math.Sin(lat2_rad) +
			Math.Cos(lat1_rad) * Math.Cos(lat2_rad) * Math.Cos(lon2_rad - lon1_rad)
		);
	}

	public static double CalculateDistance_m(in ILocationLonLat_deg value1, in ILocationLonLat_deg value2)
		=> CalculateDistance_m(value1.Location_lon_deg, value1.Location_lat_deg, value2.Location_lon_deg, value2.Location_lat_deg);
}
