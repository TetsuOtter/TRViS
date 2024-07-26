using System;

namespace TRViS.Services;

public interface ILocationLonLat_deg : IEquatable<ILocationLonLat_deg>
{
	bool HasLonLatLocation { get; }
	double Location_lon_deg { get; }
	double Location_lat_deg { get; }

	bool IEquatable<ILocationLonLat_deg>.Equals(ILocationLonLat_deg? other)
	{
		if (other is null)
			return false;

		if (ReferenceEquals(this, other))
			return true;

		return
			HasLonLatLocation == other.HasLonLatLocation
			&&
			Location_lon_deg == other.Location_lon_deg
			&&
			Location_lat_deg == other.Location_lat_deg
		;
	}
}
