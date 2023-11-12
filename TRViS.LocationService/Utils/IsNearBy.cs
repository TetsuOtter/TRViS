namespace TRViS.Services.LocationService;

internal static partial class Utils
{
	public static bool IsNearBy(StaLocationInfo target, ILocationLonLat_deg currentLocation)
		=> IsNearBy(target, CalculateDistance_m(target, currentLocation));
	public static bool IsNearBy(StaLocationInfo target, double currentDistance_m)
		=> currentDistance_m <= target.NearbyRadius_m;
}
