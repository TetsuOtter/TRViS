using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

using NLog;

using TRViS.Services;
using TRViS.Web;
using TRViS.Web.Adapters;
using TRViS.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

ConfigureNLog();

builder.Services.AddSingleton<RealTimeProvider>();
builder.Services.AddScoped<LocationService>(sp =>
{
	var http = sp.GetRequiredService<HttpClient>();
	var time = sp.GetRequiredService<RealTimeProvider>();
	return new LocationService(
		LogManager.GetLogger("LocationService"),
		LogManager.GetLogger("LocationService.Inner"),
		LogManager.GetLogger("LonLatLocationService"),
		http,
		time);
});
builder.Services.AddSingleton<BrowserWakeLock>();
builder.Services.AddScoped<DtacAppState>();
builder.Services.AddScoped<GeolocationBridge>();

await builder.Build().RunAsync();

static void ConfigureNLog()
{
	var config = new NLog.Config.LoggingConfiguration();
	var consoleTarget = new NLog.Targets.ConsoleTarget("console")
	{
		Layout = "${time} ${level:uppercase=true} ${logger} ${message} ${exception:format=tostring}"
	};
	config.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, consoleTarget);
	LogManager.Configuration = config;
}
