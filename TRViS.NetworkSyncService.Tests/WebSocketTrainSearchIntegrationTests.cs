using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using TRViS.NetworkSyncService;

namespace TRViS.NetworkSyncService.Tests;

/// <summary>
/// Integration tests for WebSocket train search functionality
/// Tests timeout handling and error scenarios
/// </summary>
[TestFixture]
public class WebSocketTrainSearchIntegrationTests
{
	private const int TEST_TIMEOUT_MS = 30000; // 30 seconds for test timeout

	/// <summary>
	/// Mock WebSocket server for testing
	/// </summary>
	private class MockWebSocketServer : IDisposable
	{
		private readonly HttpListener _httpListener;
		private readonly CancellationTokenSource _cts = new();
		private Task? _listenerTask;
		private WebSocket? _serverWebSocket;
		private readonly Func<string, Task<string?>>? _messageHandler;

		public MockWebSocketServer(string prefix, Func<string, Task<string?>>? messageHandler = null)
		{
			_httpListener = new HttpListener();
			_httpListener.Prefixes.Add(prefix);
			_messageHandler = messageHandler;
		}

		public async Task StartAsync()
		{
			_httpListener.Start();
			_listenerTask = AcceptConnectionsAsync(_cts.Token);
			// Give server time to start
			await Task.Delay(100);
		}

		private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					var context = await _httpListener.GetContextAsync();
					if (context.Request.IsWebSocketRequest)
					{
						var wsContext = await context.AcceptWebSocketAsync(null);
						_serverWebSocket = wsContext.WebSocket;
						_ = HandleWebSocketAsync(_serverWebSocket, cancellationToken);
					}
				}
				catch (HttpListenerException)
				{
					// Listener was stopped
					break;
				}
				catch (Exception ex)
				{
					TestContext.WriteLine($"Error accepting connection: {ex.Message}");
				}
			}
		}

		private async Task HandleWebSocketAsync(WebSocket webSocket, CancellationToken cancellationToken)
		{
			var buffer = new byte[4096];
			var messageBuilder = new StringBuilder();

			try
			{
				while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
				{
					var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

					if (result.MessageType == WebSocketMessageType.Close)
					{
						await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
						break;
					}

					messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

					if (result.EndOfMessage)
					{
						string message = messageBuilder.ToString();
						messageBuilder.Clear();

						// Process message and send response
						if (_messageHandler != null)
						{
							string? response = await _messageHandler(message);
							if (response != null)
							{
								byte[] responseBytes = Encoding.UTF8.GetBytes(response);
								await webSocket.SendAsync(
									new ArraySegment<byte>(responseBytes),
									WebSocketMessageType.Text,
									true,
									cancellationToken
								);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				TestContext.WriteLine($"Error handling WebSocket: {ex.Message}");
			}
		}

		public void Dispose()
		{
			_cts.Cancel();
			_serverWebSocket?.Dispose();
			_httpListener.Stop();
			_httpListener.Close();
			_cts.Dispose();
			_listenerTask?.Wait(1000);
		}
	}

	[Test, Timeout(TEST_TIMEOUT_MS)]
	public async Task SearchTrainAsync_WithTimeout_ReturnsNull()
	{
		// Arrange - Server that never responds
		using var server = new MockWebSocketServer("http://localhost:8765/", async (message) =>
		{
			// Never send a response - simulate timeout
			await Task.Delay(20000); // Longer than search timeout
			return null;
		});

		await server.StartAsync();

		using var clientWebSocket = new ClientWebSocket();
		var uri = new Uri("ws://localhost:8765/");
		var service = new WebSocketNetworkSyncService(uri, clientWebSocket);

		await service.ConnectAsync(CancellationToken.None);

		// Act
		var result = await service.SearchTrainAsync("1234");

		// Assert
		Assert.That(result, Is.Null, "Should return null when timeout occurs");

		await service.DisconnectAsync();
	}

	[Test, Timeout(TEST_TIMEOUT_MS)]
	public async Task SearchTrainAsync_WithSuccessResponse_ReturnsResults()
	{
		// Arrange - Server that responds with success
		using var server = new MockWebSocketServer("http://localhost:8766/", async (message) =>
		{
			await Task.Delay(100); // Small delay to simulate processing

			using var jsonDoc = JsonDocument.Parse(message);
			var root = jsonDoc.RootElement;
			
			if (root.TryGetProperty("MessageType", out var msgType) &&
				msgType.GetString() == "SearchTrain")
			{
				string requestId = root.GetProperty("RequestId").GetString() ?? "";
				var response = new TrainSearchResponse
				{
					RequestId = requestId,
					Success = true,
					Results = new[]
					{
						new TrainSearchResult
						{
							TrainId = "train_123",
							TrainNumber = "1234",
							WorkId = "work_1",
							WorkName = "行路1",
							StartStation = "東京",
							EndStation = "大阪"
						}
					}
				};
				return JsonSerializer.Serialize(response);
			}

			return null;
		});

		await server.StartAsync();

		using var clientWebSocket = new ClientWebSocket();
		var uri = new Uri("ws://localhost:8766/");
		var service = new WebSocketNetworkSyncService(uri, clientWebSocket);

		await service.ConnectAsync(CancellationToken.None);

		// Act
		var result = await service.SearchTrainAsync("1234");

		// Assert
		Assert.That(result, Is.Not.Null);
		Assert.That(result!.Success, Is.True);
		Assert.That(result.Results, Is.Not.Null);
		Assert.That(result.Results!.Length, Is.EqualTo(1));
		Assert.That(result.Results[0].TrainNumber, Is.EqualTo("1234"));

		await service.DisconnectAsync();
	}

	[Test, Timeout(TEST_TIMEOUT_MS)]
	public async Task SearchTrainAsync_WithErrorResponse_ReturnsError()
	{
		// Arrange - Server that responds with error
		using var server = new MockWebSocketServer("http://localhost:8767/", async (message) =>
		{
			await Task.Delay(100);

			using var jsonDoc = JsonDocument.Parse(message);
			var root = jsonDoc.RootElement;
			
			if (root.TryGetProperty("MessageType", out var msgType) &&
				msgType.GetString() == "SearchTrain")
			{
				string requestId = root.GetProperty("RequestId").GetString() ?? "";
				var response = new TrainSearchResponse
				{
					RequestId = requestId,
					Success = false,
					ErrorMessage = "Train not found"
				};
				return JsonSerializer.Serialize(response);
			}

			return null;
		});

		await server.StartAsync();

		using var clientWebSocket = new ClientWebSocket();
		var uri = new Uri("ws://localhost:8767/");
		var service = new WebSocketNetworkSyncService(uri, clientWebSocket);

		await service.ConnectAsync(CancellationToken.None);

		// Act
		var result = await service.SearchTrainAsync("9999");

		// Assert
		Assert.That(result, Is.Not.Null);
		Assert.That(result!.Success, Is.False);
		Assert.That(result.ErrorMessage, Is.EqualTo("Train not found"));

		await service.DisconnectAsync();
	}

	[Test, Timeout(TEST_TIMEOUT_MS)]
	public async Task GetTrainDataAsync_WithTimeout_ReturnsNull()
	{
		// Arrange - Server that never responds
		using var server = new MockWebSocketServer("http://localhost:8768/", async (message) =>
		{
			// Never send a response - simulate timeout
			await Task.Delay(20000); // Longer than data timeout
			return null;
		});

		await server.StartAsync();

		using var clientWebSocket = new ClientWebSocket();
		var uri = new Uri("ws://localhost:8768/");
		var service = new WebSocketNetworkSyncService(uri, clientWebSocket);

		await service.ConnectAsync(CancellationToken.None);

		// Act
		var result = await service.GetTrainDataAsync("train_123");

		// Assert
		Assert.That(result, Is.Null, "Should return null when timeout occurs");

		await service.DisconnectAsync();
	}

	[Test, Timeout(TEST_TIMEOUT_MS)]
	public async Task GetFeaturesAsync_WithTimeout_ReturnsNull()
	{
		// Arrange - Server that never responds
		using var server = new MockWebSocketServer("http://localhost:8769/", async (message) =>
		{
			// Never send a response - simulate timeout
			await Task.Delay(10000); // Longer than features timeout
			return null;
		});

		await server.StartAsync();

		using var clientWebSocket = new ClientWebSocket();
		var uri = new Uri("ws://localhost:8769/");
		var service = new WebSocketNetworkSyncService(uri, clientWebSocket);

		await service.ConnectAsync(CancellationToken.None);

		// Act
		var result = await service.GetFeaturesAsync();

		// Assert
		Assert.That(result, Is.Null, "Should return null when timeout occurs");

		await service.DisconnectAsync();
	}

	[Test, Timeout(TEST_TIMEOUT_MS)]
	public async Task GetFeaturesAsync_WithResponse_ReturnsFeatures()
	{
		// Arrange - Server that responds with features
		using var server = new MockWebSocketServer("http://localhost:8770/", async (message) =>
		{
			await Task.Delay(100);

			using var jsonDoc = JsonDocument.Parse(message);
			var root = jsonDoc.RootElement;
			
			if (root.TryGetProperty("MessageType", out var msgType) &&
				msgType.GetString() == "GetFeatures")
			{
				var response = new FeaturesResponse
				{
					Features = new[] { "TrainSearch", "SyncedData", "Timetable" }
				};
				return JsonSerializer.Serialize(response);
			}

			return null;
		});

		await server.StartAsync();

		using var clientWebSocket = new ClientWebSocket();
		var uri = new Uri("ws://localhost:8770/");
		var service = new WebSocketNetworkSyncService(uri, clientWebSocket);

		await service.ConnectAsync(CancellationToken.None);

		// Act
		var result = await service.GetFeaturesAsync();

		// Assert
		Assert.That(result, Is.Not.Null);
		Assert.That(result!.Features, Is.Not.Null);
		Assert.That(result.Features, Does.Contain("TrainSearch"));

		await service.DisconnectAsync();
	}
}
