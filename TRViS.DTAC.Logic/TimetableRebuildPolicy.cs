namespace TRViS.DTAC.Logic;

/// <summary>
/// 新しい TrainData が「同じ列車の編集」(= 同じ TrainId に対する更新) かどうかを判定する。
/// true なら呼び出し側 (ViewModel / Presenter) は ObservableCollection を作り直さず、
/// 既存の行列を mutate (= 同 index は field 上書き、超過分は Add / 不足分は Remove)
/// して、行 UI の dispose+再生成や IsRunning 等の運行状態リセットを避けることができる。
///
/// 不具合の背景: WebSocket 経由のリアルタイム編集 (TRViS_Realtime_Editor) では、
/// CacheTimetableData で同じ TrainId に対しても都度新しい <c>TrainData</c> インスタンスが
/// 作られる。旧実装はその度に <c>ObservableCollection&lt;...&gt;</c> ごと差し替えていたため、
/// 1 行の DriveTimeMM を直しただけでも全ての行 UI が破棄されて上から再描画されたり、
/// 運行中なのに「運行前」状態に戻されたりしていた。
/// </summary>
public static class TimetableRebuildPolicy
{
	/// <summary>
	/// 新 TrainData が現在表示中のものと「同じ列車の編集」と言えるか。
	/// 行数は問わない (= 駅が追加/削除された場合も同じ列車の編集としてカウントし、
	/// 行モデル列の mutate で対応する)。
	/// </summary>
	public static bool IsSameTrainEdit(string? currentTrainId, string? newTrainId)
	{
		if (currentTrainId is null || newTrainId is null)
			return false;
		return string.Equals(currentTrainId, newTrainId, StringComparison.Ordinal);
	}
}
