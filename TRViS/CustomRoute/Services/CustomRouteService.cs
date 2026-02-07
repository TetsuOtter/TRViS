using TRViS.IO.Models;
using NLog;

namespace TRViS.CustomRoute.Services;

/// <summary>
/// CustomRoute時刻表表示のビジネスロジック層
/// UIとの完全な分離を目指し、単体テストで動作を保証する
/// </summary>
public class CustomRouteService
{
	private static readonly Logger logger = LoggerService.GetGeneralLogger();
	private IReadOnlyList<TrainData>? _trains = null;
	private TrainData? _selectedTrain = null;
	private string? _selectedLineId = null;

	/// <summary>
	/// 列車データの設定
	/// </summary>
	public void SetTrains(IReadOnlyList<TrainData> trains)
	{
		if (trains == null)
		{
			logger.Error("SetTrains: trains is null");
			_trains = null;
			_selectedTrain = null;
			return;
		}

		_trains = trains;
		logger.Debug("SetTrains: {0} trains loaded", trains.Count);

		// 最初の列車を選択
		if (trains.Count > 0)
		{
			_selectedTrain = trains[0];
			_selectedLineId = trains[0].LineId;
			logger.Debug("SetTrains: Selected first train (Id={0})", _selectedTrain.Id);
		}
		else
		{
			_selectedTrain = null;
			_selectedLineId = null;
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
			logger.Warn("SelectTrainByIndex: Invalid index {0}", index);
			return false;
		}

		_selectedTrain = _trains[index];
		_selectedLineId = _selectedTrain?.LineId;
		logger.Info("SelectTrainByIndex: Selected train at index {0} (Id={1})", index, _selectedTrain?.Id);
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
			logger.Warn("SelectTrainById: Invalid trainId");
			return false;
		}

		var train = _trains.FirstOrDefault(t => t.Id == trainId);
		if (train == null)
		{
			logger.Warn("SelectTrainById: Train not found (Id={0})", trainId);
			return false;
		}

		_selectedTrain = train;
		_selectedLineId = train.LineId;
		logger.Info("SelectTrainById: Selected train (Id={0})", trainId);
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

		return _trains.IndexOf(_selectedTrain);
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

		return _trains.Where(t => t.LineId == train.LineId).ToList();
	}

	/// <summary>
	/// 選択された列車の駅一覧を取得
	/// </summary>
	public IReadOnlyList<TrainDataRow> GetSelectedTrainRows()
	{
		return _selectedTrain?.Rows ?? [];
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

		return (train.TrainName, train.TrainNumber, train.LineId);
	}

	/// <summary>
	/// 駅データから表示用の情報を抽出
	/// </summary>
	public (string stationName, string? arrivalTime, string? departureTime, bool isPass, bool isInfoRow)
		GetStationDisplayInfo(TrainDataRow row)
	{
		if (row == null)
		{
			return (string.Empty, null, null, false, false);
		}

		return (
			row.StationName ?? string.Empty,
			row.ArriveTime,
			row.DepartureTime,
			row.IsPass,
			row.IsInfoRow
		);
	}

	/// <summary>
	/// 駅間の走行時間を計算（秒単位）
	/// </summary>
	public int? CalculateTravelTimeSeconds(TrainDataRow fromRow, TrainDataRow toRow)
	{
		if (fromRow == null || toRow == null)
		{
			return null;
		}

		try
		{
			var departureSeconds = TimeStringToSeconds(fromRow.DepartureTime);
			var arrivalSeconds = TimeStringToSeconds(toRow.ArriveTime);

			if (departureSeconds == null || arrivalSeconds == null)
			{
				return null;
			}

			// 翌日への乗り越しを考慮
			if (arrivalSeconds < departureSeconds)
			{
				return (86400 - departureSeconds) + arrivalSeconds; // 24時間 = 86400秒
			}

			return arrivalSeconds - departureSeconds;
		}
		catch (Exception ex)
		{
			logger.Error(ex, "CalculateTravelTimeSeconds: Failed to calculate");
			return null;
		}
	}

	/// <summary>
	/// 時刻文字列を秒に変換 (HH:mm形式を想定)
	/// </summary>
	private int? TimeStringToSeconds(string? timeStr)
	{
		if (string.IsNullOrEmpty(timeStr))
		{
			return null;
		}

		try
		{
			var parts = timeStr.Split(':');
			if (parts.Length != 2 || !int.TryParse(parts[0], out var hours) || !int.TryParse(parts[1], out var minutes))
			{
				return null;
			}

			return hours * 3600 + minutes * 60;
		}
		catch
		{
			return null;
		}
	}
}
