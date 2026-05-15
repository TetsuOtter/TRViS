namespace TRViS.DTAC.Logic;

/// <summary>
/// 「現在位置マーカー (CurrentLocationMarker) と重なっている行は DriveTime ラベルを白文字にする」
/// の状態を保持する対象 (= タイムテーブル行モデル)。
/// </summary>
public interface ILocationMarkerHighlightTarget
{
	/// <summary>
	/// 現在位置マーカーがこの行に被っているかどうか。
	/// true のときに DriveTime ラベルを白文字 (反転色) で描く。
	/// </summary>
	bool IsLocationMarkerOnThisRow { get; set; }
}

/// <summary>
/// マーカー位置と「現在 View に並んでいる行モデル列」の対応関係を一元管理するヘルパー。
/// 行モデル列が差し替わっても、保持しているマーカー位置に従って新しい行に対しても
/// <see cref="ILocationMarkerHighlightTarget.IsLocationMarkerOnThisRow"/> を再適用する。
///
/// 旧実装は View 側で「StateChanged を受信したタイミングで RowViewList の各 Model に対して
/// for-loop で適用」していたが、ObservableCollection を差し替えた直後は RowViewList が
/// まだ古い行のままで、新しい行は async で後から追加されるため、新行に対する適用が
/// 漏れて DriveTime ラベルが黒文字のままになる不具合 (issue: HorizontalTimetable 往復後等) があった。
/// ここに集約することで「行を差し替えたら必ず再適用する」「マーカー位置が変わったら必ず再適用する」を
/// View 側の async タイミングに依存せず保証する。
/// </summary>
public sealed class LocationMarkerRowCoordinator
{
	private int _markerRowIndex = -1;
	private bool _isMarkerVisible = false;
	private IReadOnlyList<ILocationMarkerHighlightTarget>? _rows;

	/// <summary>
	/// 現在のマーカー対象行 (0-based)。<see cref="IsMarkerVisible"/> が false のときは無視される。
	/// 変更すると保持中の行に即時反映する。
	/// </summary>
	public int MarkerRowIndex
	{
		get => _markerRowIndex;
		set
		{
			if (_markerRowIndex == value)
				return;
			_markerRowIndex = value;
			ApplyToRows();
		}
	}

	/// <summary>
	/// マーカー (CurrentLocationMarker) を画面に出しているか。
	/// false のときはどの行も highlight しない。
	/// </summary>
	public bool IsMarkerVisible
	{
		get => _isMarkerVisible;
		set
		{
			if (_isMarkerVisible == value)
				return;
			_isMarkerVisible = value;
			ApplyToRows();
		}
	}

	/// <summary>
	/// 行モデル列を差し替える。直後に現在のマーカー状態を各行に適用する。
	/// 既存と同じ参照を渡されたときも、リスト内オブジェクトの状態が外部要因で
	/// リセットされている可能性があるので再適用する。
	/// </summary>
	public void SetRows(IReadOnlyList<ILocationMarkerHighlightTarget>? rows)
	{
		_rows = rows;
		ApplyToRows();
	}

	private void ApplyToRows()
	{
		if (_rows is null)
			return;

		int effectiveRow = _isMarkerVisible ? _markerRowIndex : -1;
		for (int i = 0; i < _rows.Count; i++)
		{
			bool shouldHighlight = i == effectiveRow;
			// 一致しても代入する: 新しく作られた行で flag が false のままになっているケースを
			// 拾うため (`==` チェックでスキップすると新行が highlight されない)。
			// 既存 ObservableObject 実装は同値代入を内部で no-op 化するので副作用はない。
			_rows[i].IsLocationMarkerOnThisRow = shouldHighlight;
		}
	}
}
