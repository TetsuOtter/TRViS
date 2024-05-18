using System.Reflection;
using SQLite;
using TRViS.IO.Models;

namespace TRViS.IO.Tests;

public class LoaderSQLTests
{
	const string DB_FILE_NAME = $"{nameof(LoaderSQLTests)}.sqlite";
	static readonly string DB_FILE_PATH = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", DB_FILE_NAME);

	[OneTimeSetUp]
	public void SetUp()
	{
		if (File.Exists(DB_FILE_PATH))
			File.Delete(DB_FILE_NAME);

		SQLiteConnection cnx = new(DB_FILE_NAME);

		try
		{
			string sql = File.ReadAllText(Path.Combine("Resources", "CreateTables.sql"));

			foreach (var query in sql.Split(';'))
			{
				if (string.IsNullOrWhiteSpace(query))
					continue;
				cnx.Execute(query);
			}

			string sql_add_data = File.ReadAllText(Path.Combine("Resources", "AddSampleData_1.sql"));

			foreach (var query in sql_add_data.Split(';'))
			{
				if (string.IsNullOrWhiteSpace(query))
					continue;
				cnx.Execute(query);
			}
		}
		finally
		{
			cnx.Close();
		}
	}

	[Test]
	public void GetTrainDataGroupListTest()
	{
		using LoaderSQL loader = new(DB_FILE_PATH);
		TrainDataFileInfo[] emptyArr = Array.Empty<TrainDataFileInfo>();

		IReadOnlyList<TrainDataGroup> actual = loader.GetTrainDataGroupList();

		Assert.That(
			actual.Select(v => v with { FileInfoArray = emptyArr }),
			Has.Member(new TrainDataGroup("1", "Group01", emptyArr))
		);

		var actual_array = actual.FirstOrDefault(v => v.ID == "1" && v.GroupName == "Group01")?.FileInfoArray;

		Assert.That(actual_array, Is.Not.Null);
		Assert.That(
			actual_array,
			Has.Member(new TrainDataFileInfo("1", "1", "Work01", "T9910X"))
		);
	}

	[Test]
	public void GetTrainData()
	{
		using LoaderSQL loader = new(DB_FILE_PATH);
		TimetableRow[] emptyArr = Array.Empty<TimetableRow>();

		var all = loader.GetTrainData(1);

		TrainData? actual = loader.GetTrainData(1);
		Assert.That(actual, Is.Not.Null);
		Assert.That(actual.Rows, Is.Not.Null);

		Assert.Multiple(() =>
		{
			Assert.That(actual, Is.EqualTo(
				new TrainData(
					"Work01",
					new(2022, 9, 15),
					"T9910X",
					"95",
					"高速特定",
					"E237系\n1M",
					1,
					"行き先",
					"〜試験用データ~",
					"〜試験用データ終わり~",
					"試験用データ",
					"発前点検300分",
					"試験用ダミーデータ",
					actual.Rows,
					1,

					"着後作業 10分",
					"点検",
					"作業",
					1,
					false,
					null
				)
			));

			Assert.That(actual.Rows, Is.EquivalentTo(new TimetableRow[]
			{
				new(new(1, null, null, null), 12, 34, "Station1", false, false, false, false, null, new(12, 34, 56, null), "1-1", null, null, "abc", false, null, null),
				new(new(2, 135.5, 35.5, 200), 12, null, "Station2", false, false, false, true, new(null, null, null, "停車"), null, null, null, null, null, false, null, null)
			}));
		});
	}

	[Test]
	public void GetWorkGroupListTest()
	{
		using LoaderSQL loader = new(DB_FILE_PATH);

		var actual = loader.GetWorkGroupList();

		Assert.That(actual, Has.Member(new Models.DB.WorkGroup()
		{
			Id = 1,
			Name = "Group01",
			DBVersion = 1,
		}));
	}

	[Test]
	public void GetWorkListTest()
	{
		using LoaderSQL loader = new(DB_FILE_PATH);

		var actual = loader.GetWorkList(1);

		for (int i = 1; i <= 3; i++)
		{
			Assert.That(actual, Has.Member(new Models.DB.Work()
			{
				Id = i,
				WorkGroupId = 1,
				Name = $"Work0{i}",
				AffectDate = "2022-09-15",

				AffixContentType = null,
				AffixContent = null,
				Remarks = $"Work0{i} - Remarks",
				HasETrainTimetable = i == 1,
				ETrainTimetableContentType = null,
				ETrainTimetableContent = null,
			}));
		}
	}

	[Test]
	public void GetTrainDataListTest()
	{
		using LoaderSQL loader = new(DB_FILE_PATH);

		var actual = loader.GetTrainDataList(1);

		Assert.That(actual, Has.Member(new Models.DB.TrainData()
		{
			Id = 1,
			WorkId = 1,
			TrainNumber = "T9910X",
			MaxSpeed = "95",
			SpeedType = "高速特定",
			NominalTractiveCapacity = "E237系\n1M",
			CarCount = 1,
			Destination = "行き先",
			Remarks = "試験用データ",
			BeginRemarks = "〜試験用データ~",
			AfterRemarks = "〜試験用データ終わり~",
			BeforeDeparture = "発前点検300分",
			TrainInfo = "試験用ダミーデータ",
			Direction = 1,

			AfterArrive = "着後作業 10分",
			BeforeDeparture_OnStationTrackCol = "点検",
			AfterArrive_OnStationTrackCol = "作業",
			DayCount = 1,
			IsRideOnMoving = false,
			ColorId = null,
		}));
	}
}
