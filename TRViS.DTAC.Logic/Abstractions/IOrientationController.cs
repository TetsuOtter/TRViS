namespace TRViS.DTAC.Logic.Abstractions;

/// <summary>
/// Controls the screen orientation of the device.
/// </summary>
public interface IOrientationController
{
    /// <summary>
    /// Sets the desired screen orientation.
    /// </summary>
    void SetOrientation(DesiredOrientation orientation);
}
