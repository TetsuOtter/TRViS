namespace TRViS.DTAC.Logic.Abstractions;

/// <summary>
/// Logic 層からユーザーへの通知が必要なときに投げる例外。
/// View 層がこの例外を catch してアラートダイアログとして表示する想定。
/// (DisplayAlert を Logic に持ち込むと View に寄りすぎるため、例外でメッセージを伝える)
/// </summary>
public sealed class UserAlertException : Exception
{
	public string Title { get; }
	public string CancelLabel { get; }

	public UserAlertException(string title, string message, string cancel)
			: base(message)
	{
		Title = title;
		CancelLabel = cancel;
	}

	public UserAlertException(string title, string message, string cancel, Exception innerException)
			: base(message, innerException)
	{
		Title = title;
		CancelLabel = cancel;
	}
}
