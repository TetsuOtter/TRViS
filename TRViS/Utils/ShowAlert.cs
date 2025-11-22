namespace TRViS;

public static partial class Utils
{
	public static Task DisplayAlertAsync(string title, string message, string cancel)
	{
		Page? page = 0 < Application.Current?.Windows.Count ? Application.Current?.Windows[0].Page : null;

		if (page is null)
		{
			logger.Warn("App.Current?.MainPage is null");
			return Task.CompletedTask;
		}
		return DisplayAlertAsync(page, title, message, cancel);
	}
	public static Task DisplayAlertAsync(Page page, string title, string message, string cancel)
	{
		if (!MainThread.IsMainThread)
			return MainThread.InvokeOnMainThreadAsync(() => page.DisplayAlertAsync(title, message, cancel));
		else
			return page.DisplayAlertAsync(title, message, cancel);
	}

	public static Task<bool> DisplayAlertAsync(string title, string message, string accept, string cancel)
	{
		Page? page = 0 < Application.Current?.Windows.Count ? Application.Current?.Windows[0].Page : null;

		if (page is null)
		{
			logger.Warn("App.Current?.MainPage is null");
			return Task.FromResult(false);
		}
		return DisplayAlertAsync(page, title, message, accept, cancel);
	}
	public static Task<bool> DisplayAlertAsync(Page page, string title, string message, string accept, string cancel)
	{
		if (!MainThread.IsMainThread)
			return MainThread.InvokeOnMainThreadAsync(() => page.DisplayAlertAsync(title, message, accept, cancel));
		else
			return page.DisplayAlertAsync(title, message, accept, cancel);
	}

	public static Task ExitWithAlert(Exception ex)
		=> DisplayAlertAsync("エラー", "不明なエラーが発生しました。アプリを終了します。\n" + ex.Message, "OK").ContinueWith(static _ => Environment.Exit(1));
}
