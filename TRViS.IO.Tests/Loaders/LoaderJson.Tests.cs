using System.Reflection;

using TRViS.IO.Models.DB;

namespace TRViS.IO.Tests;

public class LoaderJsonTests
{
	static readonly string JSON_FILE_PATH = Path.Combine("Resources", "db.sample.json");

	LoaderJson? loader;

	[OneTimeSetUp]
	public async Task SetUp()
	{
		loader = await LoaderJson.InitFromFileAsync(JSON_FILE_PATH);
	}

	[Test]
	public void GetWorkGroupListTest()
	{
		IReadOnlyList<WorkGroup> actual = loader!.GetWorkGroupList();

		Assert.That(actual, Has.Count.EqualTo(2));
		Assert.Multiple(() =>
		{
			Assert.That(Guid.TryParse(actual[0].Id, out _), Is.True);
			Assert.That(actual[0].Name, Is.EqualTo("WorkGroup01"));
			Assert.That(actual[0].DBVersion, Is.EqualTo(1));

			Assert.That(Guid.TryParse(actual[1].Id, out _), Is.True);
			Assert.That(actual[1].Name, Is.EqualTo("WorkGroup02"));
			Assert.That(actual[1].DBVersion, Is.Null);
		});
	}

	[Test]
	public void GetWorkListTest0()
	{
		IReadOnlyList<WorkGroup> workGroupList = loader!.GetWorkGroupList();
		string workGroupId = workGroupList[0].Id;
		IReadOnlyList<Work> actual = loader!.GetWorkList(workGroupId);

		Assert.That(actual, Has.Count.EqualTo(2));
		Assert.Multiple(() =>
		{
			Assert.That(Guid.TryParse(actual[0].Id, out _), Is.True);
			Assert.That(actual[0].WorkGroupId, Is.EqualTo(workGroupId));
			Assert.That(actual[0].Name, Is.EqualTo("WG01-Work01"));
			Assert.That(actual[0].AffectDate, Is.EqualTo("20230318"));
			Assert.That(actual[0].Remarks, Is.EqualTo("仕業に対する注意事項を記載する"));
			Assert.That(actual[0].HasETrainTimetable, Is.False);
			Assert.That(actual[0].ETrainTimetableContent, Is.Null);
			Assert.That(actual[0].ETrainTimetableContentType, Is.EqualTo(-1));

			Assert.That(Guid.TryParse(actual[1].Id, out _), Is.True);
			Assert.That(actual[1].WorkGroupId, Is.EqualTo(workGroupId));
			Assert.That(actual[1].Name, Is.EqualTo("WG01-Work02"));
		});
	}

	[Test]
	public void GetWorkListTest1()
	{
		IReadOnlyList<WorkGroup> workGroupList = loader!.GetWorkGroupList();
		string workGroupId = workGroupList[1].Id;
		IReadOnlyList<Work> actual = loader!.GetWorkList(workGroupId);

		Assert.That(actual, Is.EquivalentTo(Array.Empty<Work>()));
	}

	[Test]
	public void GetTrainListTest0()
	{
		IReadOnlyList<WorkGroup> workGroupList = loader!.GetWorkGroupList();
		string workGroupId = workGroupList[0].Id;
		IReadOnlyList<Work> workList = loader!.GetWorkList(workGroupId);
		string workId = workList[0].Id;
		IReadOnlyList<TrainData> actual = loader!.GetTrainDataList(workId);

		Assert.That(actual, Has.Count.EqualTo(2));
		Assert.Multiple(() =>
		{
			Assert.That(Guid.TryParse(actual[0].Id, out _), Is.True);
			Assert.That(actual[0].WorkId, Is.EqualTo(workId));
			Assert.That(actual[0].TrainNumber, Is.EqualTo("WG01-W01-Train01"));
			Assert.That(actual[0].Direction, Is.EqualTo(1));
			Assert.That(actual[0].ColorId, Is.EqualTo(-1));
			Assert.That(actual[0].TrainInfo, Is.EqualTo("列車情報 (列車名など)"));
			Assert.That(actual[0].Destination, Is.EqualTo("終着駅"));
			Assert.That(actual[0].CarCount, Is.EqualTo(10));
			Assert.That(actual[0].SpeedType, Is.EqualTo("速度種別"));
			Assert.That(actual[0].MaxSpeed, Is.EqualTo("最高速度"));
			Assert.That(actual[0].NominalTractiveCapacity, Is.EqualTo("けん引定数"));
			Assert.That(actual[0].BeforeDeparture, Is.EqualTo("発前作業"));
			Assert.That(actual[0].BeforeDeparture_OnStationTrackCol, Is.EqualTo("「発前」のうち、着発番線と同じ行に記載されている内容"));
			Assert.That(actual[0].AfterArrive, Is.EqualTo("着後作業"));
			Assert.That(actual[0].AfterArrive_OnStationTrackCol, Is.EqualTo("「着後」のうち、着発番線と同じ行に記載されている内容"));
			Assert.That(actual[0].BeginRemarks, Is.EqualTo("`(乗継)`など 最初の駅の上に記載されている内容"));
			Assert.That(actual[0].AfterRemarks, Is.EqualTo("`(乗継)`など 最後の駅の下に記載されている内容"));
			Assert.That(actual[0].Remarks, Is.EqualTo("列車に対する注意事項を記載する"));
			Assert.That(actual[0].IsRideOnMoving, Is.False);

			Assert.That(Guid.TryParse(actual[1].Id, out _), Is.True);
			Assert.That(actual[1].WorkId, Is.EqualTo(workId));
			Assert.That(actual[1].TrainNumber, Is.EqualTo("WG01-W01-Train02"));
			Assert.That(actual[1].Direction, Is.EqualTo(-1));
			Assert.That(actual[1].ColorId, Is.EqualTo(-1));
		});
	}

	[Test]
	public void GetTrainListTest1()
	{
		IReadOnlyList<WorkGroup> workGroupList = loader!.GetWorkGroupList();
		string workGroupId = workGroupList[0].Id;
		IReadOnlyList<Work> workList = loader!.GetWorkList(workGroupId);
		string workId = workList[1].Id;
		IReadOnlyList<TrainData> actual = loader!.GetTrainDataList(workId);

		Assert.That(actual, Has.Count.EqualTo(2));
		Assert.Multiple(() =>
		{
			Assert.That(Guid.TryParse(actual[0].Id, out _), Is.True);
			Assert.That(actual[0].WorkId, Is.EqualTo(workId));
			Assert.That(actual[0].TrainNumber, Is.EqualTo("WG01-W02-Train01"));
			Assert.That(actual[0].Direction, Is.EqualTo(1));
			Assert.That(actual[0].ColorId, Is.EqualTo(-1));

			Assert.That(Guid.TryParse(actual[1].Id, out _), Is.True);
			Assert.That(actual[1].WorkId, Is.EqualTo(workId));
			Assert.That(actual[1].TrainNumber, Is.EqualTo("WG01-W02-Train02"));
			Assert.That(actual[1].Direction, Is.EqualTo(-1));
			Assert.That(actual[1].ColorId, Is.EqualTo(-1));
		});
	}
}
