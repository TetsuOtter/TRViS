using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace TRViS.UITests;

public class Startup : IStartup
{
	public void Configure(IAppHostBuilder appBuilder)
	{
		appBuilder
			.ConfigureTests(tests =>
			{
				tests.AddTest<AppLaunchTests>();
				tests.AddTest<NavigationTests>();
			})
			.UseVisualRunner();
	}
}
