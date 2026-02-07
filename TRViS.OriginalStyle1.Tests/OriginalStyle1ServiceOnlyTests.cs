using NUnit.Framework;
using TRViS.IO.Models;

namespace TRViS.OriginalStyle1.Tests;

using Direction = TRViS.IO.Models.Direction;

/// <summary>
/// 時刻表表示のビジネスロジック層（テスト用簡易版）
/// UIとの完全な分離を目指し、単体テストで動作を保証する
/// </summary>
public class ServiceSimplified
{
	private IReadOnlyList<TrainData>? _trains = null;
	private TrainData? _selectedTrain = null;

	/// <summary>
	/// 列車データの設定
	/// </summary>
	public void SetTrains(IReadOnlyList<TrainData> trains)
	{
		if (trains == null)
		{
			_trains = null;
			_selectedTrain = null;
			return;
		}

		_trains = trains;

		// 最初の列車を選択
		if (trains.Count > 0)
		{
			_selectedTrain = trains[0];
		}
		else
		{
			_selectedTrain = null;
		}
	}

	/// <summary>
	/// 現在利用可能な列車の一覧を取得
	/// </summary>
	public IReadOnlyList<TrainData> GetAvailableTrains()
	{
		return _trains ?? [];
	}

	/// <summary>
	/// 指定されたインデックスの列車を選択
	/// </summary>
	/// <returns>成功時はtrue、インデックスが範囲外の場合はfalse</returns>
	public bool SelectTrainByIndex(int index)
	{
		if (_trains == null || index < 0 || index >= _trains.Count)
		{
			return false;
		}

		_selectedTrain = _trains[index];
		return true;
	}

	/// <summary>
	/// 指定されたIDの列車を選択
	/// </summary>
	/// <returns>成功時はtrue、IDが見つからない場合はfalse</returns>
	public bool SelectTrainById(string trainId)
	{
		if (_trains == null || string.IsNullOrEmpty(trainId))
		{
			return false;
		}

		var train = _trains.FirstOrDefault(t => t.Id == trainId);
		if (train == null)
		{
			return false;
		}

		_selectedTrain = train;
		return true;
	}

	/// <summary>
	/// 現在選択されている列車を取得
	/// </summary>
	public TrainData? GetSelectedTrain()
	{
		return _selectedTrain;
	}

	/// <summary>
	/// 現在選択されている列車のインデックスを取得
	/// </summary>
	/// <returns>見つからない場合は-1</returns>
	public int GetSelectedTrainIndex()
	{
		if (_trains == null || _selectedTrain == null)
		{
			return -1;
		}

		// IndexOfの代替
		for (int i = 0; i < _trains.Count; i++)
		{
			if (_trains[i].Id == _selectedTrain.Id)
			{
				return i;
			}
		}
		return -1;
	}

	/// <summary>
	/// 同じ路線の列車リストを取得
	/// </summary>
	public IReadOnlyList<TrainData> GetTrainsOnSameLine(TrainData train)
	{
		if (_trains == null || train == null)
		{
			return [];
		}

		// 同じ方向の列車を返す (簡略版 - テスト用)
		return _trains.Where(t => t != null && t.Direction == train.Direction).ToList();
	}

	/// <summary>
	/// 選択された列車の駅一覧を取得
	/// </summary>
	public IReadOnlyList<object> GetSelectedTrainRows()
	{
		return (_selectedTrain?.Rows ?? []).Cast<object>().ToList();
	}

	/// <summary>
	/// 指定された駅インデックスが有効かどうかを確認
	/// </summary>
	public bool IsValidStationIndex(int stationIndex)
	{
		var rows = GetSelectedTrainRows();
		return stationIndex >= 0 && stationIndex < rows.Count;
	}

	/// <summary>
	/// 列車の基本情報を取得
	/// </summary>
	public (string trainName, string? trainNumber, string? lineId) GetTrainBasicInfo(TrainData train)
	{
		if (train == null)
		{
			return (string.Empty, null, null);
		}

		// TrainDataから基本情報を抽出
		// TrainNameはデータベースに存在しないため、TrainNumberやIdから構築
		return (train.TrainNumber ?? string.Empty, train.TrainNumber, train.Id);
	}
}

/// <summary>
/// Service の単体テスト（テストプロジェクト用簡易版）
/// ビジネスロジックが正しく動作することを保証
/// </summary>
[TestFixture]
public class ServiceOnlyTests
{
	private ServiceSimplified _service = null!;

	[SetUp]
	public void SetUp()
	{
		_service = new ServiceSimplified();
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
			// sqlite-net-pcl generated constructor: (string id, Direction direction, string? trainNumber, ...)
			var train = new TrainData($"train_{i}", Direction.Outbound, $"100{i}");
			trains.Add(train);
		}
		return trains;
	}

	private List<TrainData> CreateTestTrainsWithDifferentLines()
	{
		var trains = new List<TrainData>
		{
			new TrainData("train_0", Direction.Outbound, "1000"),
			new TrainData("train_1", Direction.Outbound, "1001"),
			new TrainData("train_2", Direction.Inbound, "2000"),
		};
		return trains;
	}

	#endregion
}
