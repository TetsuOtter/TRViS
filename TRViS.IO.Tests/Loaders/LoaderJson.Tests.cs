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

		Assert.That(actual, Is.EquivalentTo(new WorkGroup[]
		{
			new WorkGroup()
			{
				Name = "WorkGroup01",
				DBVersion = 1,
				Id = 0,
			},
			new WorkGroup()
			{
				Name = "WorkGroup02",
				DBVersion = null,
				Id = 1,
			},
		}));
	}

	[Test]
	public void GetWorkListTest0()
	{
		IReadOnlyList<Work> actual = loader!.GetWorkList(0);

		Assert.That(actual, Is.EquivalentTo(new Work[]
		{
			new Work()
			{
				WorkGroupId = 0,
				Id = 0,
				Name = "WG01-Work01",
				AffectDate = "20230318",

				AffixContent = null,
				AffixContentType = -1,
				Remarks = "仕業に対する注意事項を記載する",
				HasETrainTimetable = false,
				ETrainTimetableContent = null,
				ETrainTimetableContentType = -1,
			},
			new Work()
			{
				WorkGroupId = 0,
				Id = 1,
				Name = "WG01-Work02",
			},
		}));
	}

	[Test]
	public void GetWorkListTest1()
	{
		IReadOnlyList<Work> actual = loader!.GetWorkList(1);

		Assert.That(actual, Is.EquivalentTo(Array.Empty<Work>()));
	}

	[Test]
	public void GetTrainListTest0()
	{
		IReadOnlyList<TrainData> actual = loader!.GetTrainDataList(0);

		Assert.That(actual, Is.EquivalentTo(new TrainData[]
		{
			new TrainData()
			{
				AfterArrive = "着後作業",
				AfterArrive_OnStationTrackCol = "「着後」のうち、着発番線と同じ行に記載されている内容",
				AfterRemarks = "`(乗継)`など 最後の駅の下に記載されている内容",
				BeforeDeparture = "発前作業",
				BeforeDeparture_OnStationTrackCol = "「発前」のうち、着発番線と同じ行に記載されている内容",
				BeginRemarks = "`(乗継)`など 最初の駅の上に記載されている内容",
				CarCount = 10,
				ColorId = -1, // Not Implemented
				DayCount = 0,
				Destination = "終着駅",
				Direction = 1,
				Id = 0,
				IsRideOnMoving = false,
				MaxSpeed = "最高速度",
				NominalTractiveCapacity = "けん引定数",
				Remarks = "列車に対する注意事項を記載する",
				SpeedType = "速度種別",
				TrainInfo = "列車情報 (列車名など)",
				TrainNumber = "WG01-W01-Train01",
				WorkId = 0,
				WorkType = 0,
			},

			new TrainData()
			{
				Direction = -1,
				Id = 1,
				TrainNumber = "WG01-W01-Train02",
				WorkId = 0,

				ColorId = -1, // Not Implemented
			},
		}));
	}

	[Test]
	public void GetTrainListTest1()
	{
		IReadOnlyList<TrainData> actual = loader!.GetTrainDataList(1);

		Assert.That(actual, Is.EquivalentTo(new TrainData[]
		{
			new TrainData()
			{
				Direction = 1,
				Id = 2,
				TrainNumber = "WG01-W02-Train01",
				WorkId = 1,

				ColorId = -1, // Not Implemented
			},

			new TrainData()
			{
				Direction = -1,
				Id = 3,
				TrainNumber = "WG01-W02-Train02",
				WorkId = 1,

				ColorId = -1, // Not Implemented
			},
		}));
	}
}
