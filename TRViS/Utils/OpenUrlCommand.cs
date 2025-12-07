namespace TRViS.Utils;

public static partial class Util
{
	static public readonly Command<string> OpenUrlCommand = new(
		async (url) =>
		{
			logger!.Trace("url: {0}", url);
			await Browser.OpenAsync(url, BrowserLaunchMode.SystemPreferred);
		}
	);
}
