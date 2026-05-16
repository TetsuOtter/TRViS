using TRViS.DTAC.Logic;

namespace TRViS.DTAC.Logic.Tests;

public class LocationMarkerRowCoordinatorTests
{
	private sealed class StubRow : ILocationMarkerHighlightTarget
	{
		public bool IsLocationMarkerOnThisRow { get; set; }
	}

	private static StubRow[] MakeRows(int count)
	{
		var rows = new StubRow[count];
		for (int i = 0; i < count; i++)
			rows[i] = new StubRow();
		return rows;
	}

	[Fact]
	public void SetRows_AfterMarkerConfigured_HighlightsMatchingRow()
	{
		var c = new LocationMarkerRowCoordinator
		{
			MarkerRowIndex = 2,
			IsMarkerVisible = true,
		};
		var rows = MakeRows(4);

		c.SetRows(rows);

		Assert.False(rows[0].IsLocationMarkerOnThisRow);
		Assert.False(rows[1].IsLocationMarkerOnThisRow);
		Assert.True(rows[2].IsLocationMarkerOnThisRow);
		Assert.False(rows[3].IsLocationMarkerOnThisRow);
	}

	[Fact]
	public void SetRows_MarkerInvisible_NoRowHighlighted()
	{
		var c = new LocationMarkerRowCoordinator
		{
			MarkerRowIndex = 2,
			IsMarkerVisible = false,
		};
		var rows = MakeRows(4);

		c.SetRows(rows);

		Assert.All(rows, r => Assert.False(r.IsLocationMarkerOnThisRow));
	}

	[Fact]
	public void MarkerRowIndexChange_RedistributesToExistingRows()
	{
		var c = new LocationMarkerRowCoordinator { IsMarkerVisible = true };
		var rows = MakeRows(4);
		c.SetRows(rows);
		c.MarkerRowIndex = 1;
		Assert.True(rows[1].IsLocationMarkerOnThisRow);

		c.MarkerRowIndex = 3;

		Assert.False(rows[1].IsLocationMarkerOnThisRow);
		Assert.True(rows[3].IsLocationMarkerOnThisRow);
	}

	[Fact]
	public void IsMarkerVisibleChange_RedistributesToExistingRows()
	{
		var c = new LocationMarkerRowCoordinator { MarkerRowIndex = 2 };
		var rows = MakeRows(4);
		c.SetRows(rows);
		Assert.False(rows[2].IsLocationMarkerOnThisRow);

		c.IsMarkerVisible = true;
		Assert.True(rows[2].IsLocationMarkerOnThisRow);

		c.IsMarkerVisible = false;
		Assert.False(rows[2].IsLocationMarkerOnThisRow);
	}

	[Fact]
	public void SetRows_ReplacingRowsWhileMarkerStaysAtSameIndex_HighlightsNewRow()
	{
		// 不具合再現: 横型時刻表ページから戻ったとき等、行モデルが差し替わっても
		// マーカー行 index が同じだと、新しい行に対して IsLocationMarkerOnThisRow=true を
		// 設定する経路が抜けて DriveTime ラベルが黒文字のまま残るケース。
		// 旧 View 実装は StateChanged を受けて for-loop で RowViewList に適用していたが、
		// SetRowViewsAsync が async で後から rows を入れ替えるため race していた。
		// Coordinator 経由なら SetRows のタイミングで必ず再適用される。
		var c = new LocationMarkerRowCoordinator
		{
			MarkerRowIndex = 1,
			IsMarkerVisible = true,
		};
		var oldRows = MakeRows(3);
		c.SetRows(oldRows);
		Assert.True(oldRows[1].IsLocationMarkerOnThisRow);

		// 「行モデル差し替え」のシミュレーション: 別インスタンスの行配列を渡す。
		// このとき新しい行は初期値 false で来る (現実の SetTrainData も同様)。
		var newRows = MakeRows(3);
		Assert.False(newRows[1].IsLocationMarkerOnThisRow); // 前提: 新行は黒文字状態

		c.SetRows(newRows);

		Assert.False(newRows[0].IsLocationMarkerOnThisRow);
		Assert.True(newRows[1].IsLocationMarkerOnThisRow);  // ← 修正後はここが白文字に
		Assert.False(newRows[2].IsLocationMarkerOnThisRow);
	}

	[Fact]
	public void SetRows_NullClearsRows_NoCrashOnSubsequentMarkerChange()
	{
		var c = new LocationMarkerRowCoordinator { IsMarkerVisible = true };
		c.SetRows(MakeRows(3));
		c.SetRows(null);

		// 列無しの状態でマーカーが動いても何も起こらない (NRE しない)
		c.MarkerRowIndex = 1;
		c.IsMarkerVisible = false;
	}

	[Fact]
	public void SetRows_MarkerIndexOutOfRange_NoCrashAndNoRowHighlighted()
	{
		var c = new LocationMarkerRowCoordinator
		{
			MarkerRowIndex = 99, // out of range
			IsMarkerVisible = true,
		};
		var rows = MakeRows(3);

		c.SetRows(rows);

		Assert.All(rows, r => Assert.False(r.IsLocationMarkerOnThisRow));
	}

	/// <summary>
	/// 旧 View 実装相当 (= 「StateChanged を受信したタイミングで一度だけ for-loop で適用、
	/// 行差し替えは後から非同期で行われる」) を再現すると、新しい行に対してマーカー highlight が
	/// 抜け落ちることを示すデモテスト。Coordinator のテストと対比させ、なぜ Coordinator が必要かを
	/// コードで明示する。
	/// </summary>
	[Fact]
	public void DemonstrateBug_LegacyApplyOnceThenReplaceRows_NewRowsNotHighlighted()
	{
		// 旧 View の挙動: state が変わるたびに一度だけ rows.ForEach(...) で flag を立てるが、
		// その後で行が差し替わる場合、新行に対しては適用されない (再適用パスがない)。
		static void LegacyApplyOnce(IReadOnlyList<ILocationMarkerHighlightTarget> rows, int markerRowIndex, bool isMarkerVisible)
		{
			int effective = isMarkerVisible ? markerRowIndex : -1;
			for (int i = 0; i < rows.Count; i++)
				rows[i].IsLocationMarkerOnThisRow = (i == effective);
		}

		// 旧 view 実装シミュレーション
		var oldRows = MakeRows(3);
		LegacyApplyOnce(oldRows, markerRowIndex: 1, isMarkerVisible: true);
		Assert.True(oldRows[1].IsLocationMarkerOnThisRow);

		// SetRowViewsAsync が後から rows を差し替えるシミュレーション。
		// 旧実装ではこの後、もう一度 LegacyApplyOnce が呼ばれる保証がない (state が変わっていないため)。
		var newRows = MakeRows(3);
		// ↓ ここが不具合: マーカー index は同じだが、新行は黒文字のまま。
		Assert.False(newRows[1].IsLocationMarkerOnThisRow);
	}
}
