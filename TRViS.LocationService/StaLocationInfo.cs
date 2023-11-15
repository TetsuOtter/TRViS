namespace TRViS.Services;

public interface ILocationLonLat_deg
{
	double Location_lon_deg { get; }
	double Location_lat_deg { get; }
}

public class StaLocationInfo : ILocationLonLat_deg
{
	public double Location_m { get; }
	public double Location_lon_deg { get; }
	public double Location_lat_deg { get; }
	public double NearbyRadius_m { get; }

	public StaLocationInfo(double location_m, double location_lon_deg, double location_lat_deg, double nearbyRadius_m)
	{
		Location_m = location_m;
		Location_lon_deg = location_lon_deg;
		Location_lat_deg = location_lat_deg;
		NearbyRadius_m = nearbyRadius_m;
	}

	public bool Equals(StaLocationInfo? other)
	{
		if (other is null)
			return false;

		if (ReferenceEquals(this, other))
			return true;

		return
			Location_m == other.Location_m
			&&
			Location_lon_deg == other.Location_lon_deg
			&&
			Location_lat_deg == other.Location_lat_deg
			&&
			NearbyRadius_m == other.NearbyRadius_m
		;
	}

	public override bool Equals(object obj)
		=> Equals(obj as StaLocationInfo);

	public override int GetHashCode()
		=> HashCode.Combine(Location_m, Location_lon_deg, Location_lat_deg, NearbyRadius_m);

	public override string ToString()
	{
		return $"{nameof(StaLocationInfo)} {{ {nameof(Location_m)}: {Location_m}, {nameof(Location_lon_deg)}: {Location_lon_deg}, {nameof(Location_lat_deg)}: {Location_lat_deg}, {nameof(NearbyRadius_m)}: {NearbyRadius_m} }}";
	}
}
