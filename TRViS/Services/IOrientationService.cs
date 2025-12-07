namespace TRViS.Services;

/// <summary>
/// Service interface for controlling screen orientation.
/// </summary>
public interface IOrientationService
{
	/// <summary>
	/// Sets the allowed display orientation for the application.
	/// </summary>
	/// <param name="orientation">The desired orientation constraint.</param>
	void SetOrientation(AppDisplayOrientation orientation);
}
