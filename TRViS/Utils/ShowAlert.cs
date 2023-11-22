namespace TRViS;

public static partial class Utils
{
	public static Task DisplayAlert(string title, string message, string cancel)
	{
		Page? page = Application.Current?.MainPage;

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
		Page? page = Application.Current?.MainPage;

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
}
