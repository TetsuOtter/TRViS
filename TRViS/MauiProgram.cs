using TRViS.IO;
using TRViS.ViewModels;

namespace TRViS;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIconsRegular");
			});

		builder.Services
			.AddSingleton(typeof(AppShell))
			.AddSingleton(typeof(SelectTrainPage))
			.AddSingleton(typeof(DTAC.VerticalStylePage))
			.AddSingleton(typeof(AppViewModel));

		return builder.Build();
	}
}

