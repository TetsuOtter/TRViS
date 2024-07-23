using System;

namespace TRViS.Services;

public class LocationLonLat_deg : ILocationLonLat_deg, IEquatable<LocationLonLat_deg>
{
	public bool HasLonLatLocation { get; }
	public double Location_lon_deg { get; }
	public double Location_lat_deg { get; }

	public LocationLonLat_deg(
		double location_lon_deg,
		double location_lat_deg
	)
	{
		Location_lon_deg = location_lon_deg;
		Location_lat_deg = location_lat_deg;
		HasLonLatLocation = true;
	}

	public bool Equals(LocationLonLat_deg? other)
	{
		if (other is null)
			return false;

		if (ReferenceEquals(this, other))
			return true;

		return
			((IEquatable<ILocationLonLat_deg>)this).Equals(other)
			&&
			Location_lon_deg == other.Location_lon_deg
			&&
			Location_lat_deg == other.Location_lat_deg
		;
	}

	public override bool Equals(object obj)
		=> Equals(obj as LocationLonLat_deg);

	public override int GetHashCode()
		=> HashCode.Combine(Location_lon_deg, Location_lat_deg);

	public override string ToString()
	{
		return $"{nameof(LocationLonLat_deg)} {{ lon:{Location_lon_deg}, lat:{Location_lat_deg} }}";
	}
}
