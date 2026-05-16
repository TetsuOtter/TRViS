using TRViS.IO;
using TRViS.IO.Models;

namespace TRViS.Core.Tests;

/// <summary>
/// <see cref="TimetableSelectionManager"/> の選択保持/フォールバック挙動 (#214) を検証する。
/// テスト用の <see cref="FakeLoader"/> を介して各階層のリストを差し替え、
/// Refresh() で選択 Id が維持されること、消えた階層から先頭にフォールバックすることを確認する。
///
/// #224 で OnLoaderChanged 時の auto-pick が廃止された (Home picker の tentative 設計に合わせた変更)。
/// そのため Loader 設定後は SelectedWorkGroup は null。Refresh の挙動を検証するテストでは
/// <see cref="CommitFirstSelection"/> で明示的に「先頭 WG/Work/Train」を選択してから動かす。
/// </summary>
public class TimetableSelectionManagerTests
{
	/// <summary>
	/// 旧 auto-pick と同等の状態 (先頭 WG → 先頭 Work → 先頭 Train) を作る。
	/// </summary>
	private static void CommitFirstSelection(TimetableSelectionManager manager)
	{
		manager.SelectedWorkGroup = manager.WorkGroupList?.FirstOrDefault();
	}

	[Fact]
	public void Refresh_PreservesSelection_WhenAllIdsStillPresent()
	{
		var loader = new FakeLoader();
		loader.Setup(BuildSampleData());

		var manager = new TimetableSelectionManager { Loader = loader };
		CommitFirstSelection(manager);
		manager.SelectedWork = loader.WorkLists["wg-1"][1]; // Work1 へ移動
		manager.SelectedTrainData = loader.TrainListsByWorkId["w-1-1"][1]; // 2 つ目の Train

		Assert.Equal("w-1-1", manager.SelectedWork?.Id);
		Assert.Equal("t-1-1-1", manager.SelectedTrainData?.Id);

		// Loader 側で Train の中身を更新 (TrainNumber を変える)
		var data = BuildSampleData();
		var updated = data.TrainListsByWorkId["w-1-1"][1] with { TrainNumber = "UPDATED" };
		data.TrainListsByWorkId["w-1-1"] = [data.TrainListsByWorkId["w-1-1"][0], updated];
		data.TrainDataById["t-1-1-1"] = updated;
		loader.Setup(data);

		manager.Refresh();

		Assert.Equal("wg-1", manager.SelectedWorkGroup?.Id);
		Assert.Equal("w-1-1", manager.SelectedWork?.Id);
		Assert.Equal("t-1-1-1", manager.SelectedTrainData?.Id);
		Assert.Equal("UPDATED", manager.SelectedTrainData?.TrainNumber);
	}

	[Fact]
	public void Refresh_FallsBackToFirstTrain_WhenSelectedTrainRemoved()
	{
		var loader = new FakeLoader();
		loader.Setup(BuildSampleData());

		var manager = new TimetableSelectionManager { Loader = loader };
		CommitFirstSelection(manager);
		manager.SelectedTrainData = loader.TrainListsByWorkId["w-1-0"][1];
		Assert.Equal("t-1-0-1", manager.SelectedTrainData?.Id);

		// 2 つ目の Train を消す
		var data = BuildSampleData();
		data.TrainListsByWorkId["w-1-0"] = [data.TrainListsByWorkId["w-1-0"][0]];
		data.TrainDataById.Remove("t-1-0-1");
		loader.Setup(data);

		manager.Refresh();

		Assert.Equal("t-1-0-0", manager.SelectedTrainData?.Id);
	}

	[Fact]
	public void Refresh_FallsBackToFirstWork_WhenSelectedWorkRemoved()
	{
		var loader = new FakeLoader();
		loader.Setup(BuildSampleData());

		var manager = new TimetableSelectionManager { Loader = loader };
		CommitFirstSelection(manager);
		manager.SelectedWork = loader.WorkLists["wg-1"][1];
		Assert.Equal("w-1-1", manager.SelectedWork?.Id);

		// Work[1] を消す
		var data = BuildSampleData();
		data.WorkLists["wg-1"] = [data.WorkLists["wg-1"][0]];
		data.TrainListsByWorkId.Remove("w-1-1");
		loader.Setup(data);

		manager.Refresh();

		Assert.Equal("w-1-0", manager.SelectedWork?.Id);
		Assert.Equal("t-1-0-0", manager.SelectedTrainData?.Id);
	}

	[Fact]
	public void Refresh_FallsBackToFirstWorkGroup_WhenSelectedWorkGroupRemoved()
	{
		var loader = new FakeLoader();
		var data = BuildSampleData();
		data.WorkGroups.Add(new WorkGroup("wg-2", "WG2"));
		data.WorkLists["wg-2"] = [new Work("w-2-0", "wg-2", "Work2-0")];
		data.TrainListsByWorkId["w-2-0"] = [SimpleTrain("t-2-0-0")];
		data.TrainDataById["t-2-0-0"] = data.TrainListsByWorkId["w-2-0"][0];
		loader.Setup(data);

		var manager = new TimetableSelectionManager { Loader = loader };
		manager.SelectedWorkGroup = loader.WorkGroups[1]; // wg-2 を選択

		// wg-2 を消す
		data = BuildSampleData();
		loader.Setup(data);

		manager.Refresh();

		Assert.Equal("wg-1", manager.SelectedWorkGroup?.Id);
		Assert.Equal("w-1-0", manager.SelectedWork?.Id);
	}

	[Fact]
	public void OnWorkChanged_NormalSelection_FallsBackToFirstTrain()
	{
		// 通常の Work 選択切り替え (Refresh ではない経路) では先頭 Train が選択される
		var loader = new FakeLoader();
		loader.Setup(BuildSampleData());

		var manager = new TimetableSelectionManager { Loader = loader };
		manager.SelectedWork = loader.WorkLists["wg-1"][1];

		Assert.Equal("t-1-1-0", manager.SelectedTrainData?.Id);
	}

	[Fact]
	public void EmptyWorkGroupList_ClearsAllChildren()
	{
		// #214 コメント: WorkGroup リストが空のとき、Work / Train も空でなければならない。
		var loader = new FakeLoader();
		loader.Setup(new SampleData()); // 全部空

		var manager = new TimetableSelectionManager { Loader = loader };

		Assert.Null(manager.SelectedWorkGroup);
		Assert.Null(manager.SelectedWork);
		Assert.Null(manager.SelectedTrainData);
		Assert.Null(manager.WorkList);
		Assert.Null(manager.OrderedTrainDataList);
	}

	[Fact]
	public void Refresh_WhenNewWorkGroupListBecomesEmpty_ClearsChildren()
	{
		var loader = new FakeLoader();
		loader.Setup(BuildSampleData());

		var manager = new TimetableSelectionManager { Loader = loader };
		CommitFirstSelection(manager);
		Assert.NotNull(manager.SelectedWorkGroup);
		Assert.NotNull(manager.SelectedWork);
		Assert.NotNull(manager.SelectedTrainData);

		// Loader が空のリストを返すように切り替え、Refresh で再評価する
		loader.Setup(new SampleData());
		manager.Refresh();

		Assert.Null(manager.SelectedWorkGroup);
		Assert.Null(manager.SelectedWork);
		Assert.Null(manager.SelectedTrainData);
		Assert.Null(manager.WorkList);
		Assert.Null(manager.OrderedTrainDataList);
	}

	[Fact]
	public void OnLoaderChanged_DoesNotAutoSelectFirstWorkGroup_WhenMultipleWorkGroups()
	{
		// #224 の契約は「実際に選択肢がある (WorkGroup が 2 個以上)」場合に限り維持。
		// 複数 WorkGroup ではホーム画面の tentative-selection picker を見せたいので
		// auto-pick しない。単一 WorkGroup の場合は下のテストの通り自動選択する。
		var loader = new FakeLoader();
		loader.Setup(BuildMultiWgSampleData());

		var manager = new TimetableSelectionManager { Loader = loader };

		Assert.True(manager.WorkGroupList!.Count >= 2);
		Assert.Null(manager.SelectedWorkGroup);
		Assert.Null(manager.SelectedWork);
		Assert.Null(manager.SelectedTrainData);
	}

	[Fact]
	public void OnLoaderChanged_AutoSelectsWhenSingleWorkGroup()
	{
		// 単一 WorkGroup のときは #224 の auto-pick 抑止をスコープ付きで巻き戻す:
		// 1 項目だけの picker は摩擦でしかないため即コミットし、cascade で
		// 先頭 Work / 先頭 Train まで自動選択して時刻表を即表示できる状態にする。
		var loader = new FakeLoader();
		loader.Setup(BuildSampleData()); // BuildSampleData は WorkGroup 1 個構成

		var manager = new TimetableSelectionManager { Loader = loader };

		Assert.Single(manager.WorkGroupList!);
		Assert.Equal("wg-1", manager.SelectedWorkGroup?.Id);
		Assert.Equal("w-1-0", manager.SelectedWork?.Id);     // cascade: 先頭 Work
		Assert.Equal("t-1-0-0", manager.SelectedTrainData?.Id); // cascade: 先頭 Train
	}

	[Fact]
	public void OnLoaderChanged_SingleWorkGroup_CascadeThrows_LoadStillSucceeds()
	{
		// 不完全なソース (WorkGroup 行はあるが Work テーブルが無い SQLite 等):
		// 単一 WG 自動選択の cascade で GetWorkList が例外を投げても、Loader 設定
		// 自体は失敗してはならない (= auto-pick 導入前と同じく「開けて WG ピッカー
		// 表示」まで成功する)。選択は巻き戻り null、WorkGroupList は維持される。
		var loader = new FakeLoader { ThrowOnGetWorkList = true };
		loader.Setup(BuildSampleData()); // WorkGroup 1 個

		// コンストラクタ内の Loader 設定 (= OnLoaderChanged) が例外を伝播しない
		var manager = new TimetableSelectionManager { Loader = loader };

		Assert.Single(manager.WorkGroupList!);          // 一覧は読めている
		Assert.Null(manager.SelectedWorkGroup);         // auto-pick はロールバック
		Assert.Null(manager.SelectedWork);
		Assert.Null(manager.SelectedTrainData);
		Assert.Null(manager.WorkList);
	}

	[Fact]
	public void Refresh_DoesNotAutoCommit_WhenNoPriorSelection()
	{
		// #224 — Refresh は「すでにコミット済み」のときだけフォールバックする。
		// 未コミット状態 (Home picker 上で何も選んでいない) で WebSocket Refresh が
		// 飛んできても勝手に最初の項目を選んではならない。
		// 単一 WorkGroup は OnLoaderChanged 時点で自動コミットされてしまうため、
		// 「未コミット」状態を作るには複数 WorkGroup の構成が必要。
		var loader = new FakeLoader();
		loader.Setup(BuildMultiWgSampleData());

		var manager = new TimetableSelectionManager { Loader = loader };
		Assert.Null(manager.SelectedWorkGroup);

		manager.Refresh();

		Assert.Null(manager.SelectedWorkGroup);
		Assert.Null(manager.SelectedWork);
		Assert.Null(manager.SelectedTrainData);
	}

	[Fact]
	public void Refresh_AutoSelectsSingleWorkGroup_WhenListArrivesAsync()
	{
		// WebSocket: Loader is set before the WorkGroup list arrives, so
		// OnLoaderChanged saw an empty list and could not apply the
		// single-WorkGroup auto-pick. When the server push lands and Refresh
		// runs, the single-WorkGroup convenience must finally kick in
		// (otherwise a single-WG WebSocket connection is never selected).
		var loader = new FakeLoader();
		loader.Setup(new SampleData()); // empty at connect time
		var manager = new TimetableSelectionManager { Loader = loader };
		Assert.Null(manager.SelectedWorkGroup);

		loader.Setup(BuildSampleData()); // single-WG data arrives via push
		manager.Refresh();

		Assert.Equal("wg-1", manager.SelectedWorkGroup?.Id);
		Assert.Equal("w-1-0", manager.SelectedWork?.Id);      // cascade
		Assert.Equal("t-1-0-0", manager.SelectedTrainData?.Id); // cascade
	}

	[Fact]
	public void Refresh_DoesNotAutoSelect_WhenMultipleWorkGroupsArriveAsync()
	{
		// Multi-WorkGroup remains a genuine choice owned by the Home picker:
		// even when the list arrives late, Refresh must NOT force a default.
		var loader = new FakeLoader();
		loader.Setup(new SampleData());
		var manager = new TimetableSelectionManager { Loader = loader };

		loader.Setup(BuildMultiWgSampleData());
		manager.Refresh();

		Assert.True(manager.WorkGroupList!.Count >= 2);
		Assert.Null(manager.SelectedWorkGroup);
	}

	[Fact]
	public void Refresh_WhenNewWorkListBecomesEmpty_ClearsTrainSelection()
	{
		var loader = new FakeLoader();
		loader.Setup(BuildSampleData());

		var manager = new TimetableSelectionManager { Loader = loader };
		CommitFirstSelection(manager);
		Assert.NotNull(manager.SelectedWork);
		Assert.NotNull(manager.SelectedTrainData);

		// WorkGroup は維持しつつ、Work リストを空にする
		var data = BuildSampleData();
		data.WorkLists["wg-1"] = [];
		data.TrainListsByWorkId.Clear();
		data.TrainDataById.Clear();
		loader.Setup(data);

		manager.Refresh();

		Assert.Equal("wg-1", manager.SelectedWorkGroup?.Id);
		Assert.Null(manager.SelectedWork);
		Assert.Null(manager.SelectedTrainData);
		Assert.Null(manager.OrderedTrainDataList);
	}

	// ----- ヘルパー -----

	private static TrainData SimpleTrain(string id) =>
		new(id, Direction.Outbound, TrainNumber: id);

	private sealed class SampleData
	{
		public List<WorkGroup> WorkGroups { get; } = [];
		public Dictionary<string, List<Work>> WorkLists { get; } = [];
		public Dictionary<string, List<TrainData>> TrainListsByWorkId { get; } = [];
		public Dictionary<string, TrainData> TrainDataById { get; } = [];
	}

	private static SampleData BuildSampleData()
	{
		var d = new SampleData();
		d.WorkGroups.Add(new WorkGroup("wg-1", "WG1"));
		d.WorkLists["wg-1"] =
		[
			new Work("w-1-0", "wg-1", "Work1-0"),
			new Work("w-1-1", "wg-1", "Work1-1"),
		];
		d.TrainListsByWorkId["w-1-0"] =
		[
			SimpleTrain("t-1-0-0"),
			SimpleTrain("t-1-0-1"),
		];
		d.TrainListsByWorkId["w-1-1"] =
		[
			SimpleTrain("t-1-1-0"),
			SimpleTrain("t-1-1-1"),
		];
		foreach (var workTrains in d.TrainListsByWorkId.Values)
			foreach (var t in workTrains)
				d.TrainDataById[t.Id] = t;
		return d;
	}

	/// <summary>
	/// 2 個以上の WorkGroup を持つ構成。OnLoaderChanged の単一 WorkGroup
	/// auto-select が発火しないため、「未コミット (= ユーザーが picker で
	/// 未選択)」の契約を検証するテストで使う。
	/// </summary>
	private static SampleData BuildMultiWgSampleData()
	{
		var d = BuildSampleData();
		d.WorkGroups.Add(new WorkGroup("wg-2", "WG2"));
		d.WorkLists["wg-2"] =
		[
			new Work("w-2-0", "wg-2", "Work2-0"),
		];
		d.TrainListsByWorkId["w-2-0"] = [SimpleTrain("t-2-0-0")];
		d.TrainDataById["t-2-0-0"] = d.TrainListsByWorkId["w-2-0"][0];
		return d;
	}

	private sealed class FakeLoader : ILoader
	{
		public List<WorkGroup> WorkGroups { get; private set; } = [];
		public Dictionary<string, List<Work>> WorkLists { get; private set; } = [];
		public Dictionary<string, List<TrainData>> TrainListsByWorkId { get; private set; } = [];
		public Dictionary<string, TrainData> TrainDataById { get; private set; } = [];

		public void Setup(SampleData data)
		{
			WorkGroups = data.WorkGroups;
			WorkLists = data.WorkLists;
			TrainListsByWorkId = data.TrainListsByWorkId;
			TrainDataById = data.TrainDataById;
		}

		public TrainData? GetTrainData(string trainId) =>
			TrainDataById.TryGetValue(trainId, out var t) ? t : null;

		/// <summary>
		/// Simulates a malformed/partial source (e.g. SQLite with a WorkGroup
		/// row but no Work table): GetWorkGroupList works, GetWorkList throws.
		/// </summary>
		public bool ThrowOnGetWorkList { get; set; }

		public IReadOnlyList<WorkGroup> GetWorkGroupList() => WorkGroups;

		public IReadOnlyList<Work> GetWorkList(string workGroupId) =>
			ThrowOnGetWorkList
				? throw new InvalidOperationException("no such table: Work")
				: WorkLists.TryGetValue(workGroupId, out var list) ? list : [];

		public IReadOnlyList<TrainData> GetTrainDataList(string workId) =>
			TrainListsByWorkId.TryGetValue(workId, out var list) ? list : [];

		public void Dispose() { }
	}
}
