using TRViS.DemoServer.Components;
using TRViS.DemoServer.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add TRViS services
builder.Services.AddSingleton<TimetableService>();
builder.Services.AddSingleton<TimeSimulationService>();
builder.Services.AddSingleton<ConnectionManagerService>();
builder.Services.AddScoped<WebSocketHandler>();
builder.Services.AddHostedService<TimeSimulationBackgroundService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Enable WebSockets
app.UseWebSockets();

// WebSocket endpoint for TRViS
app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var handler = context.RequestServices.GetRequiredService<WebSocketHandler>();
        await handler.HandleConnectionAsync(webSocket, ipAddress);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
