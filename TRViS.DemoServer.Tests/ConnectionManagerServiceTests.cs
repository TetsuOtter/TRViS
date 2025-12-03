using System.Net.WebSockets;
using NUnit.Framework;
using Moq;
using TRViS.DemoServer.Services;

namespace TRViS.DemoServer.Tests;

[TestFixture]
public class ConnectionManagerServiceTests
{
    [Test]
    public void AddConnection_ReturnsUniqueId()
    {
        // Arrange
        var service = new ConnectionManagerService();
        var mockWebSocket = new Mock<WebSocket>();

        // Act
        var id1 = service.AddConnection(mockWebSocket.Object, "192.168.1.1");
        var id2 = service.AddConnection(mockWebSocket.Object, "192.168.1.2");

        // Assert
        Assert.That(id1, Is.Not.EqualTo(id2));
        Assert.That(id1, Does.StartWith("client_"));
        Assert.That(id2, Does.StartWith("client_"));
    }

    [Test]
    public void AddConnection_FiresConnectionAddedEvent()
    {
        // Arrange
        var service = new ConnectionManagerService();
        var mockWebSocket = new Mock<WebSocket>();
        bool eventFired = false;
        ClientConnection? capturedConnection = null;

        service.ConnectionAdded += (sender, args) =>
        {
            eventFired = true;
            capturedConnection = args.Connection;
        };

        // Act
        var id = service.AddConnection(mockWebSocket.Object, "192.168.1.1");

        // Assert
        Assert.That(eventFired, Is.True);
        Assert.That(capturedConnection, Is.Not.Null);
        Assert.That(capturedConnection!.Id, Is.EqualTo(id));
        Assert.That(capturedConnection.IpAddress, Is.EqualTo("192.168.1.1"));
    }

    [Test]
    public void RemoveConnection_FiresConnectionRemovedEvent()
    {
        // Arrange
        var service = new ConnectionManagerService();
        var mockWebSocket = new Mock<WebSocket>();
        var id = service.AddConnection(mockWebSocket.Object, "192.168.1.1");
        
        bool eventFired = false;
        ClientConnection? capturedConnection = null;

        service.ConnectionRemoved += (sender, args) =>
        {
            eventFired = true;
            capturedConnection = args.Connection;
        };

        // Act
        service.RemoveConnection(id);

        // Assert
        Assert.That(eventFired, Is.True);
        Assert.That(capturedConnection, Is.Not.Null);
        Assert.That(capturedConnection!.DisconnectedAt, Is.Not.Null);
    }

    [Test]
    public void GetAllConnections_ReturnsAllActiveConnections()
    {
        // Arrange
        var service = new ConnectionManagerService();
        var mockWebSocket1 = new Mock<WebSocket>();
        var mockWebSocket2 = new Mock<WebSocket>();
        
        var id1 = service.AddConnection(mockWebSocket1.Object, "192.168.1.1");
        var id2 = service.AddConnection(mockWebSocket2.Object, "192.168.1.2");

        // Act
        var connections = service.GetAllConnections().ToList();

        // Assert
        Assert.That(connections.Count, Is.EqualTo(2));
        Assert.That(connections.Any(c => c.Id == id1), Is.True);
        Assert.That(connections.Any(c => c.Id == id2), Is.True);
    }

    [Test]
    public void GetConnection_ReturnsCorrectConnection()
    {
        // Arrange
        var service = new ConnectionManagerService();
        var mockWebSocket = new Mock<WebSocket>();
        var id = service.AddConnection(mockWebSocket.Object, "192.168.1.1");

        // Act
        var connection = service.GetConnection(id);

        // Assert
        Assert.That(connection, Is.Not.Null);
        Assert.That(connection!.Id, Is.EqualTo(id));
        Assert.That(connection.IpAddress, Is.EqualTo("192.168.1.1"));
    }

    [Test]
    public void GetConnection_ReturnsNullForNonExistentId()
    {
        // Arrange
        var service = new ConnectionManagerService();

        // Act
        var connection = service.GetConnection("nonexistent");

        // Assert
        Assert.That(connection, Is.Null);
    }

    [Test]
    public void ActiveConnectionCount_ReturnsCorrectCount()
    {
        // Arrange
        var service = new ConnectionManagerService();
        var mockWebSocket1 = new Mock<WebSocket>();
        var mockWebSocket2 = new Mock<WebSocket>();

        // Act & Assert - Initially 0
        Assert.That(service.ActiveConnectionCount, Is.EqualTo(0));

        // Add connections
        var id1 = service.AddConnection(mockWebSocket1.Object, "192.168.1.1");
        Assert.That(service.ActiveConnectionCount, Is.EqualTo(1));

        service.AddConnection(mockWebSocket2.Object, "192.168.1.2");
        Assert.That(service.ActiveConnectionCount, Is.EqualTo(2));

        // Remove connection
        service.RemoveConnection(id1);
        Assert.That(service.ActiveConnectionCount, Is.EqualTo(1));
    }

    [Test]
    public void ClientConnection_PropertiesCanBeSet()
    {
        // Arrange
        var service = new ConnectionManagerService();
        var mockWebSocket = new Mock<WebSocket>();
        var id = service.AddConnection(mockWebSocket.Object, "192.168.1.1");
        var connection = service.GetConnection(id);

        // Act
        connection!.SelectedTrainId = "train_123";
        connection.LocationMeters = 1234.5;
        connection.CanStart = true;

        // Assert
        var retrieved = service.GetConnection(id);
        Assert.That(retrieved!.SelectedTrainId, Is.EqualTo("train_123"));
        Assert.That(retrieved.LocationMeters, Is.EqualTo(1234.5));
        Assert.That(retrieved.CanStart, Is.True);
    }
}
