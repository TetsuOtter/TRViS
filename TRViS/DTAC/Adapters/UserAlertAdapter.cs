using TRViS.DTAC.Logic.Abstractions;
using TRViS.Utils;

namespace TRViS.DTAC.Adapters;

/// <summary>
/// Adapter that shows alert dialogs using Util.DisplayAlertAsync.
/// </summary>
internal class UserAlertAdapter : IUserAlertService
{
    public void DisplayAlert(string title, string message, string cancel)
    {
        Util.DisplayAlertAsync(title, message, cancel);
    }
}
