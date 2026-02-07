using NUnit.Framework;
using TRViS.IO.Models;

namespace TRViS.CustomRoute.Tests;

/// <summary>
/// CustomRouteService の単体テスト（テストプロジェクト用簡易版）
/// ビジネスロジックが正しく動作することを保証
/// </summary>
[TestFixture]
public class CustomRouteServiceOnlyTests
{
	private CustomRouteServiceSimplified _service = null!;

	[SetUp]
	public void SetUp()
	{
		_service = new CustomRouteServiceSimplified();
	}

	#region SetTrains Tests

	[Test]
	public void SetTrains_WithValidList_LoadsTrainsSuccessfully()
	{
		// Arrange
		var trains = CreateTestTrains(3);

		// Act
		_service.SetTrains(trains);

		// Assert
		var availableTrains = _service.GetAvailableTrains();
		Assert.That(availableTrains, Has.Count.EqualTo(3));
	}

	[Test]
	public void SetTrains_WithEmptyList_ClearsTrains()
	{
		// Arrange
		var trains = CreateTestTrains(2);
		_service.SetTrains(trains);

		// Act
		_service.SetTrains([]);

		// Assert
		var availableTrains = _service.GetAvailableTrains();
		Assert.That(availableTrains, Has.Count.EqualTo(0));
	}

	[Test]
	public void SetTrains_WithNull_DoesNotThrow()
	{
		// Act & Assert
		Assert.DoesNotThrow(() => _service.SetTrains(null!));
	}

	#endregion

	#region SelectTrain Tests

	[Test]
	public void SelectTrainByIndex_WithValidIndex_SelectsCorrectTrain()
	{
		// Arrange
		var trains = CreateTestTrains(3);
		_service.SetTrains(trains);

		// Act
		var result = _service.SelectTrainByIndex(1);
		var selected = _service.GetSelectedTrain();

		// Assert
		Assert.That(result, Is.True);
		Assert.That(selected?.Id, Is.EqualTo($"train_1"));
	}

	[Test]
	public void SelectTrainByIndex_WithInvalidIndex_ReturnsFalse()
	{
		// Arrange
		var trains = CreateTestTrains(2);
		_service.SetTrains(trains);

		// Act
		var result = _service.SelectTrainByIndex(10);

		// Assert
		Assert.That(result, Is.False);
	}

	[Test]
	public void SelectTrainById_WithValidId_SelectsCorrectTrain()
	{
		// Arrange
		var trains = CreateTestTrains(3);
		_service.SetTrains(trains);

		// Act
		var result = _service.SelectTrainById("train_1");
		var selected = _service.GetSelectedTrain();

		// Assert
		Assert.That(result, Is.True);
		Assert.That(selected?.Id, Is.EqualTo("train_1"));
	}

	[Test]
	public void SelectTrainById_WithInvalidId_ReturnsFalse()
	{
		// Arrange
		var trains = CreateTestTrains(2);
		_service.SetTrains(trains);

		// Act
		var result = _service.SelectTrainById("invalid_id");

		// Assert
		Assert.That(result, Is.False);
	}

	#endregion

	#region GetSelectedTrain Tests

	[Test]
	public void GetSelectedTrain_AfterSetTrains_ReturnsFirstTrain()
	{
		// Arrange
		var trains = CreateTestTrains(2);
		_service.SetTrains(trains);

		// Act
		var selected = _service.GetSelectedTrain();

		// Assert
		Assert.That(selected?.Id, Is.EqualTo("train_0"));
	}

	[Test]
	public void GetSelectedTrainIndex_WithSelectedTrain_ReturnsCorrectIndex()
	{
		// Arrange
		var trains = CreateTestTrains(3);
		_service.SetTrains(trains);
		_service.SelectTrainByIndex(2);

		// Act
		var index = _service.GetSelectedTrainIndex();

		// Assert
		Assert.That(index, Is.EqualTo(2));
	}

	[Test]
	public void GetSelectedTrainIndex_WithoutSelection_ReturnsMinusOne()
	{
		// Act
		var index = _service.GetSelectedTrainIndex();

		// Assert
		Assert.That(index, Is.EqualTo(-1));
	}

	#endregion

	#region GetTrainsOnSameLine Tests

	[Test]
	public void GetTrainsOnSameLine_WithValidTrain_ReturnsTrainsOnSameLine()
	{
		// Arrange
		var trains = CreateTestTrainsWithDifferentLines();
		_service.SetTrains(trains);

		// Act
		var trainsOnLine1 = _service.GetTrainsOnSameLine(trains[0]);

		// Assert
		Assert.That(trainsOnLine1, Has.Count.GreaterThan(0));
	}

	#endregion

	#region GetSelectedTrainRows Tests

	[Test]
	public void GetSelectedTrainRows_AfterSelection_ReturnsRows()
	{
		// Arrange
		var trains = CreateTestTrains(1);
		_service.SetTrains(trains);

		// Act
		var rows = _service.GetSelectedTrainRows();

		// Assert
		Assert.That(rows, Is.Not.Null);
	}

	[Test]
	public void GetSelectedTrainRows_WithoutSelection_ReturnsEmptyList()
	{
		// Act
		var rows = _service.GetSelectedTrainRows();

		// Assert
		Assert.That(rows, Has.Count.EqualTo(0));
	}

	#endregion

	#region IsValidStationIndex Tests

	[Test]
	public void IsValidStationIndex_WithValidIndex_ReturnsTrue()
	{
		// Arrange
		var trains = CreateTestTrains(1);
		_service.SetTrains(trains);

		// Act
		var result = _service.IsValidStationIndex(0);

		// Assert
		Assert.That(result, Is.True);
	}

	[Test]
	public void IsValidStationIndex_WithInvalidIndex_ReturnsFalse()
	{
		// Arrange
		var trains = CreateTestTrains(1);
		_service.SetTrains(trains);

		// Act
		var result = _service.IsValidStationIndex(100);

		// Assert
		Assert.That(result, Is.False);
	}

	#endregion

	#region GetTrainBasicInfo Tests

	[Test]
	public void GetTrainBasicInfo_WithValidTrain_ReturnsCorrectInfo()
	{
		// Arrange
		var trains = CreateTestTrains(1);

		// Act
		var (trainName, trainNumber, lineId) = _service.GetTrainBasicInfo(trains[0]);

		// Assert
		Assert.That(trainName, Is.Not.Null.And.Not.Empty);
		Assert.That(trainNumber, Is.Not.Null);
	}

	[Test]
	public void GetTrainBasicInfo_WithNull_ReturnsEmpty()
	{
		// Act
		var (trainName, trainNumber, lineId) = _service.GetTrainBasicInfo(null!);

		// Assert
		Assert.That(trainName, Is.Empty);
		Assert.That(trainNumber, Is.Null);
		Assert.That(lineId, Is.Null);
	}

	#endregion

	#region Helper Methods

	private List<TrainData> CreateTestTrains(int count)
	{
		var trains = new List<TrainData>();
		for (int i = 0; i < count; i++)
		{
			// TrainDataの必須プロパティを設定
			var train = new TrainData(
				id: $"train_{i}",
				direction: 0,
				trainNumber: $"100{i}"
			);
			trains.Add(train);
		}
		return trains;
	}

	private List<TrainData> CreateTestTrainsWithDifferentLines()
	{
		var trains = new List<TrainData>
		{
			new TrainData(id: "train_0", direction: 0, trainNumber: "1000"),
			new TrainData(id: "train_1", direction: 0, trainNumber: "1001"),
			new TrainData(id: "train_2", direction: 0, trainNumber: "2000"),
		};
		return trains;
	}

	#endregion
}
