namespace TRViS.DTAC.Logic.Abstractions;

/// <summary>
/// Displays user-facing alert dialogs.
/// </summary>
public interface IUserAlertService
{
    /// <summary>
    /// Shows a non-blocking alert dialog with a single dismiss button.
    /// </summary>
    void DisplayAlert(string title, string message, string cancel);
}
