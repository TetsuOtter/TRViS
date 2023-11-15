namespace TRViS.Services.LocationService;

internal static partial class Utils
{
	public static bool IsNearBy(StaLocationInfo target, ILocationLonLat_deg currentLocation)
		=> IsNearBy(target, CalculateDistance_m(target, currentLocation));
	public static bool IsNearBy(StaLocationInfo target, double currentDistance_m)
		=> IsNearBy(target.NearbyRadius_m, currentDistance_m);

	public static bool IsNearBy(double NearbyRadius_m, double currentDistance_m)
		=> currentDistance_m <= NearbyRadius_m;
	
	public static bool IsLeaved(StaLocationInfo target, ILocationLonLat_deg currentLocation)
		=> IsLeaved(target, CalculateDistance_m(target, currentLocation));
	public static bool IsLeaved(StaLocationInfo target, double currentDistance_m)
		=> IsLeaved(target.NearbyRadius_m, currentDistance_m);
	public static bool IsLeaved(double NearbyRadius_m, double currentDistance_m)
		=> !IsNearBy(NearbyRadius_m * 1.1, currentDistance_m);
}
