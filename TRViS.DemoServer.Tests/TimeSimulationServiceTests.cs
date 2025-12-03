using NUnit.Framework;
using TRViS.DemoServer.Services;

namespace TRViS.DemoServer.Tests;

[TestFixture]
public class TimeSimulationServiceTests
{
    [Test]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Arrange & Act
        var service = new TimeSimulationService();

        // Assert
        Assert.That(service.CurrentSpeed, Is.EqualTo(TimeSimulationService.TimeSpeed.Normal));
        Assert.That(service.IsRunning, Is.False);
    }

    [Test]
    public void SetSpeed_UpdatesCurrentSpeed()
    {
        // Arrange
        var service = new TimeSimulationService();

        // Act
        service.CurrentSpeed = TimeSimulationService.TimeSpeed.Fast30;

        // Assert
        Assert.That(service.CurrentSpeed, Is.EqualTo(TimeSimulationService.TimeSpeed.Fast30));
    }

    [Test]
    public void Start_SetsIsRunningToTrue()
    {
        // Arrange
        var service = new TimeSimulationService();

        // Act
        service.Start();

        // Assert
        Assert.That(service.IsRunning, Is.True);
    }

    [Test]
    public void Stop_SetsIsRunningToFalse()
    {
        // Arrange
        var service = new TimeSimulationService();
        service.Start();

        // Act
        service.Stop();

        // Assert
        Assert.That(service.IsRunning, Is.False);
    }

    [Test]
    public void SetSimulatedTime_UpdatesCurrentSimulatedTime()
    {
        // Arrange
        var service = new TimeSimulationService();
        var targetTime = new DateTime(2025, 12, 3, 10, 30, 0);

        // Act
        service.SetSimulatedTime(targetTime);

        // Assert
        var currentTime = service.CurrentSimulatedTime;
        Assert.That(currentTime.Year, Is.EqualTo(2025));
        Assert.That(currentTime.Month, Is.EqualTo(12));
        Assert.That(currentTime.Day, Is.EqualTo(3));
        Assert.That(currentTime.Hour, Is.EqualTo(10));
        Assert.That(currentTime.Minute, Is.EqualTo(30));
    }

    [Test]
    public async Task Tick_WhenRunning_AdvancesTime()
    {
        // Arrange
        var service = new TimeSimulationService();
        service.CurrentSpeed = TimeSimulationService.TimeSpeed.Fast30;
        var initialTime = service.CurrentSimulatedTime;
        service.Start();

        // Act
        await Task.Delay(150); // Wait for some time to pass
        service.Tick();
        var newTime = service.CurrentSimulatedTime;

        // Assert
        Assert.That(newTime, Is.GreaterThan(initialTime));
    }

    [Test]
    public void Tick_WhenStopped_DoesNotAdvanceTime()
    {
        // Arrange
        var service = new TimeSimulationService();
        service.Stop();
        var initialTime = service.CurrentSimulatedTime;

        // Act
        service.Tick();
        var newTime = service.CurrentSimulatedTime;

        // Assert
        Assert.That(newTime, Is.EqualTo(initialTime));
    }

    [Test]
    public void TimeUpdated_EventFiredOnStart()
    {
        // Arrange
        var service = new TimeSimulationService();
        bool eventFired = false;
        service.TimeUpdated += (sender, args) => eventFired = true;

        // Act
        service.Start();

        // Assert
        Assert.That(eventFired, Is.True);
    }

    [Test]
    public void Reset_ResetsToCurrentSystemTime()
    {
        // Arrange
        var service = new TimeSimulationService();
        service.SetSimulatedTime(new DateTime(2020, 1, 1));
        
        // Act
        service.Reset();
        var currentTime = service.CurrentSimulatedTime;

        // Assert
        Assert.That(currentTime.Year, Is.EqualTo(DateTime.Now.Year));
    }

    [Test]
    public void CurrentTimeMilliseconds_ReturnsCorrectValue()
    {
        // Arrange
        var service = new TimeSimulationService();
        var targetTime = new DateTime(2025, 12, 3, 1, 30, 0); // 1:30 AM = 90 minutes = 5400 seconds = 5400000 ms
        service.SetSimulatedTime(targetTime);

        // Act
        var milliseconds = service.CurrentTimeMilliseconds;

        // Assert
        Assert.That(milliseconds, Is.EqualTo(5400000));
    }
}
