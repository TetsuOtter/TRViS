using TRViS.IO.Models;

namespace TRViS.CustomRoute.Tests;

/// <summary>
/// CustomRoute時刻表表示のビジネスロジック層（テスト用簡易版）
/// UIとの完全な分離を目指し、単体テストで動作を保証する
/// </summary>
public class CustomRouteServiceSimplified
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

		// 同じWorkIdの列車を返す (WorkIdを路線と見なす)
		return _trains.Where(t => t.WorkId == train.WorkId).ToList();
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
		return (train.TrainNumber ?? string.Empty, train.TrainNumber, train.WorkId);
	}
}
