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
	public void OnLoaderChanged_DoesNotAutoSelectFirstWorkGroup()
	{
		// #224 — OnLoaderChanged は auto-pick せず、選択を null にする。
		// ホーム画面の tentative-selection 設計に合わせた契約。
		var loader = new FakeLoader();
		loader.Setup(BuildSampleData());

		var manager = new TimetableSelectionManager { Loader = loader };

		Assert.NotEmpty(manager.WorkGroupList!);
		Assert.Null(manager.SelectedWorkGroup);
		Assert.Null(manager.SelectedWork);
		Assert.Null(manager.SelectedTrainData);
	}

	[Fact]
	public void Refresh_DoesNotAutoCommit_WhenNoPriorSelection()
	{
		// #224 — Refresh は「すでにコミット済み」のときだけフォールバックする。
		// 未コミット状態 (Home picker 上で何も選んでいない) で WebSocket Refresh が
		// 飛んできても勝手に最初の項目を選んではならない。
		var loader = new FakeLoader();
		loader.Setup(BuildSampleData());

		var manager = new TimetableSelectionManager { Loader = loader };
		Assert.Null(manager.SelectedWorkGroup);

		manager.Refresh();

		Assert.Null(manager.SelectedWorkGroup);
		Assert.Null(manager.SelectedWork);
		Assert.Null(manager.SelectedTrainData);
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

		public IReadOnlyList<WorkGroup> GetWorkGroupList() => WorkGroups;

		public IReadOnlyList<Work> GetWorkList(string workGroupId) =>
			WorkLists.TryGetValue(workGroupId, out var list) ? list : [];

		public IReadOnlyList<TrainData> GetTrainDataList(string workId) =>
			TrainListsByWorkId.TryGetValue(workId, out var list) ? list : [];

		public void Dispose() { }
	}
}
