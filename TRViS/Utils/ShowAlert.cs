namespace TRViS;

public static partial class Utils
{
	public static Task DisplayAlert(string title, string message, string cancel)
	{
		Page? page = 0 < Application.Current?.Windows.Count ? Application.Current?.Windows[0].Page : null;

		if (page is null)
		{
			logger.Warn("App.Current?.MainPage is null");
			return Task.CompletedTask;
		}
		return DisplayAlert(page, title, message, cancel);
	}
	public static Task DisplayAlert(Page page, string title, string message, string cancel)
	{
		if (!MainThread.IsMainThread)
			return MainThread.InvokeOnMainThreadAsync(() => page.DisplayAlert(title, message, cancel));
		else
			return page.DisplayAlert(title, message, cancel);
	}

	public static Task<bool> DisplayAlert(string title, string message, string accept, string cancel)
	{
		Page? page = 0 < Application.Current?.Windows.Count ? Application.Current?.Windows[0].Page : null;

		if (page is null)
		{
			logger.Warn("App.Current?.MainPage is null");
			return Task.FromResult(false);
		}
		return DisplayAlert(page, title, message, accept, cancel);
	}
	public static Task<bool> DisplayAlert(Page page, string title, string message, string accept, string cancel)
	{
		if (!MainThread.IsMainThread)
			return MainThread.InvokeOnMainThreadAsync(() => page.DisplayAlert(title, message, accept, cancel));
		else
			return page.DisplayAlert(title, message, accept, cancel);
	}

	public static Task ExitWithAlert(Exception ex)
		=> DisplayAlert("エラー", "不明なエラーが発生しました。アプリを終了します。\n" + ex.Message, "OK").ContinueWith(_ => Environment.Exit(1));
}
