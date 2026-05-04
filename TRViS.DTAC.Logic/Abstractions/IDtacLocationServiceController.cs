using TRViS.IO.Models;
using TRViS.Services;

namespace TRViS.DTAC.Logic.Abstractions;

/// <summary>
/// Represents GPS location update data
/// </summary>
public record GpsLocationUpdate(double Latitude, double Longitude, double? Accuracy);

/// <summary>
/// Extended location service controller for D-TAC with additional capabilities
/// </summary>
public interface IDtacLocationServiceController : ILocationService
{
	/// <summary>
	/// Whether the network sync service can start
	/// </summary>
	bool NetworkSyncServiceCanStart { get; }

	/// <summary>
	/// Fired when GPS location is updated
	/// </summary>
	event EventHandler<GpsLocationUpdate>? GpsLocationUpdated;

	/// <summary>
	/// Sets timetable rows for location service
	/// </summary>
	void SetTimetableRows(TimetableRow[]? rows);

	/// <summary>
	/// Fired when the location service encounters an exception.
	/// </summary>
	event EventHandler<Exception>? ExceptionThrown;
}
