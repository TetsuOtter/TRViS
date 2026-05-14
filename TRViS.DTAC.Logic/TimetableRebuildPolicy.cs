namespace TRViS.DTAC.Logic;

/// <summary>
/// VerticalTimetableViewModel が新しい TrainData を受け取った時、行リストを
/// 全面再構築 (= ObservableCollection を作り直して row UI を全 dispose → 再生成) するか、
/// 既存の行モデルに対して in-place で field 単位の差分更新だけ行うかを判定するためのポリシー。
///
/// 不具合の背景: WebSocket 経由のリアルタイム編集では、CacheTimetableData で
/// 同じ TrainId に対しても都度新しい <c>TrainData</c> インスタンスが作られる。
/// 旧実装はその度に <c>ObservableCollection&lt;...&gt;</c> ごと差し替えていたため、
/// 1 行の DriveTimeMM を直しただけでも全ての行 UI が破棄されて上から再描画される
/// 見た目になっていた。同一列車 (= 同じ Id) で行数も変わらないなら、field 代入で
/// PropertyChanged 単位の更新に留めれば、変更のあった row だけが UI 更新される。
/// </summary>
public static class TimetableRebuildPolicy
{
	/// <summary>
	/// 現在表示中の TrainData (<paramref name="currentTrainId"/> / <paramref name="currentRowCount"/>) に対して
	/// 新しい TrainData (<paramref name="newTrainId"/> / <paramref name="newRowCount"/>) が「同じ列車の field 単位編集」と
	/// 言えるかを返す。true なら呼び出し側は既存の row 列に in-place 更新を適用してよい。
	///
	/// 同一とみなす条件:
	///   - 両方の TrainId が null でなく、文字列として等しい
	///   - 行数も等しい (構造変化なし)
	/// それ以外 (= 列車自体の切替、行が追加/削除されたケース) は全面再構築させる。
	/// </summary>
	public static bool CanUpdateInPlace(
		string? currentTrainId,
		int currentRowCount,
		string? newTrainId,
		int newRowCount
	)
	{
		if (currentTrainId is null || newTrainId is null)
			return false;
		if (!string.Equals(currentTrainId, newTrainId, StringComparison.Ordinal))
			return false;
		return currentRowCount == newRowCount;
	}
}
