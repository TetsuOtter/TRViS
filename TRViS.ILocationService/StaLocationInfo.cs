using System;

namespace TRViS.Services;

public class StaLocationInfo : ILocationLonLat_deg, IEquatable<StaLocationInfo>
{
	public const double DefaultNearbyRadius_m = 200;

	public bool HasLonLatLocation { get; }
	public double Location_m { get; }
	public double Location_lon_deg { get; }
	public double Location_lat_deg { get; }
	public double NearbyRadius_m { get; }

	public StaLocationInfo(
		double location_m,
		double? location_lon_deg,
		double? location_lat_deg,
		double? nearbyRadius_m
	)
	{
		Location_m = location_m;
		NearbyRadius_m = nearbyRadius_m ?? DefaultNearbyRadius_m;
		if (location_lon_deg is double lon && location_lat_deg is double lat)
		{
			Location_lon_deg = lon;
			Location_lat_deg = lat;
			HasLonLatLocation = true;
		}
		else
		{
			Location_lon_deg = 0;
			Location_lat_deg = 0;
			HasLonLatLocation = false;
		}
	}

	public StaLocationInfo(
		double location_m,
		double? nearbyRadius_m
	) : this(
		location_m,
		null,
		null,
		nearbyRadius_m
	)
	{
	}

	public bool Equals(StaLocationInfo? other)
	{
		if (other is null)
			return false;

		if (ReferenceEquals(this, other))
			return true;

		return
			((IEquatable<ILocationLonLat_deg>)this).Equals(other)
			&&
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
		=> HasLonLatLocation
			? ToStringWithLonLat()
			: ToStringWithoutLonLat();
	private string ToStringWithLonLat()
		=> $"{nameof(StaLocationInfo)} {{ {nameof(Location_m)}: {Location_m}, lon: {Location_lon_deg}, lat: {Location_lat_deg}, {nameof(NearbyRadius_m)}: {NearbyRadius_m} }}";
	private string ToStringWithoutLonLat()
		=> $"{nameof(StaLocationInfo)} {{ {nameof(Location_m)}: {Location_m}, {nameof(NearbyRadius_m)}: {NearbyRadius_m} }}";
}
