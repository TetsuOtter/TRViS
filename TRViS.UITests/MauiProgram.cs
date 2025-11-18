using Microsoft.Extensions.Logging;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace TRViS.UITests;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.ConfigureTests(new Startup())
			.UseVisualRunner();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
