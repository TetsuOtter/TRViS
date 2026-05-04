namespace TRViS.Services;

public class UserAlertRequestedEventArgs(string title, string message, string cancel) : EventArgs
{
	public string Title { get; } = title;
	public string Message { get; } = message;
	public string Cancel { get; } = cancel;
}
