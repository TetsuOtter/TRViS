using TRViS.ReferenceServer;

// ポートは環境変数 PORT、またはコマンドライン引数 --port <n> で指定可能
ushort port = 8080;
for (int i = 0; i < args.Length - 1; i++)
{
	if (args[i] == "--port" && ushort.TryParse(args[i + 1], out ushort p))
	{
		port = p;
		break;
	}
}
if (Environment.GetEnvironmentVariable("PORT") is string envPort && ushort.TryParse(envPort, out ushort ep))
	port = ep;

using var server = new ReferenceNetworkSyncServer(port);
server.Start();
Console.WriteLine($"TRViS Reference Server listening on port {server.Port}");
Console.WriteLine("Press Ctrl+C to stop.");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
	e.Cancel = true;
	cts.Cancel();
};

try
{
	await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (TaskCanceledException) { }
finally
{
	server.Stop();
	Console.WriteLine("Server stopped.");
}
