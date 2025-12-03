using NUnit.Framework;
using TRViS.DemoServer.Services;

namespace TRViS.DemoServer.Tests;

[TestFixture]
public class TimetableServiceTests
{
    [Test]
    public void Constructor_InitializesWithSampleData()
    {
        // Arrange & Act
        var service = new TimetableService();

        // Assert
        var trains = service.GetAllTrains();
        Assert.That(trains.Count, Is.GreaterThan(0));
    }

    [Test]
    public void SearchTrains_WhenEnabled_ReturnsMatchingTrains()
    {
        // Arrange
        var service = new TimetableService();
        service.IsTrainSearchEnabled = true;

        // Act
        var response = service.SearchTrains("1234");

        // Assert
        Assert.That(response.Success, Is.True);
        Assert.That(response.Results, Is.Not.Null);
        Assert.That(response.Results!.Length, Is.GreaterThan(0));
        Assert.That(response.Results[0].TrainNumber, Is.EqualTo("1234"));
    }

    [Test]
    public void SearchTrains_WhenDisabled_ReturnsError()
    {
        // Arrange
        var service = new TimetableService();
        service.IsTrainSearchEnabled = false;

        // Act
        var response = service.SearchTrains("1234");

        // Assert
        Assert.That(response.Success, Is.False);
        Assert.That(response.ErrorMessage, Does.Contain("disabled"));
    }

    [Test]
    public void SearchTrains_WithNonExistentTrain_ReturnsEmptyResults()
    {
        // Arrange
        var service = new TimetableService();
        service.IsTrainSearchEnabled = true;

        // Act
        var response = service.SearchTrains("9999999");

        // Assert
        Assert.That(response.Success, Is.True);
        Assert.That(response.Results, Is.Not.Null);
        Assert.That(response.Results!.Length, Is.EqualTo(0));
    }

    [Test]
    public void GetTrainData_WithValidTrainId_ReturnsData()
    {
        // Arrange
        var service = new TimetableService();
        var trains = service.GetAllTrains();
        var trainId = trains.First().TrainId;

        // Act
        var response = service.GetTrainData(trainId);

        // Assert
        Assert.That(response.Success, Is.True);
        Assert.That(response.TrainId, Is.EqualTo(trainId));
        Assert.That(response.Data, Is.Not.Null);
        Assert.That(response.Data, Does.Contain("TrainNumber"));
    }

    [Test]
    public void GetTrainData_WithInvalidTrainId_ReturnsError()
    {
        // Arrange
        var service = new TimetableService();

        // Act
        var response = service.GetTrainData("invalid_train");

        // Assert
        Assert.That(response.Success, Is.False);
        Assert.That(response.ErrorMessage, Does.Contain("not found"));
    }

    [Test]
    public void GetAllTrains_ReturnsReadOnlyList()
    {
        // Arrange
        var service = new TimetableService();

        // Act
        var trains = service.GetAllTrains();

        // Assert
        Assert.That(trains, Is.Not.Null);
        Assert.That(trains, Is.InstanceOf<IReadOnlyList<SampleTrain>>());
    }

    [Test]
    public void IsTrainSearchEnabled_CanBeToggled()
    {
        // Arrange
        var service = new TimetableService();

        // Act & Assert
        service.IsTrainSearchEnabled = true;
        Assert.That(service.IsTrainSearchEnabled, Is.True);

        service.IsTrainSearchEnabled = false;
        Assert.That(service.IsTrainSearchEnabled, Is.False);
    }
}
