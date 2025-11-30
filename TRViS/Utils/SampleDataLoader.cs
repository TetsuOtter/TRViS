using TRViS.IO.Models;

namespace TRViS;

public class SampleDataLoader : TRViS.IO.ILoader
{
	const string WORK_GROUP_1 = "1";
	const string WORK_1_1 = "1-1";
	const string WORK_1_2 = "1-2";
	const string TRAIN_1_1_1 = "1-1-1";
	const string TRAIN_1_1_2 = "1-1-2";
	const string TRAIN_1_1_3 = "1-1-3";
	const string TRAIN_1_2_1 = "1-2-1";

	// Sample horizontal timetable image (a simple PNG with text "横型時刻表サンプル")
	// This is a minimal valid PNG image
	static readonly byte[] SampleHorizontalTimetableImageData = CreateSamplePngData();

	static byte[] CreateSamplePngData()
	{
		// Create a simple sample PNG data
		// This is a minimal 1x1 pixel white PNG for demonstration purposes
		// In production, you would use actual timetable images
		return new byte[]
		{
			0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
			0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR chunk
			0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
			0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
			0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41,
			0x54, 0x08, 0xD7, 0x63, 0xF8, 0xFF, 0xFF, 0x3F,
			0x00, 0x05, 0xFE, 0x02, 0xFE, 0xDC, 0xCC, 0x59,
			0xE7, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E,
			0x44, 0xAE, 0x42, 0x60, 0x82
		};
	}

	static readonly List<WorkGroup> WorkGroupList = new()
	{
		new(Id: WORK_GROUP_1, Name: "WorkGroup1"),
	};

	static readonly List<Work> WorkList = new()
	{
		new(Id: WORK_1_1, WorkGroupId: WORK_GROUP_1, Name: "Work1-1", Remarks: "Sample [b][i]Work[/i][/b] [color=#FF0000 dark=#00FF00]Remark[size=32]s[/size][/color]\nLine 2\nLine 3"),
		new(
			Id: WORK_1_2,
			WorkGroupId: WORK_GROUP_1,
			Name: "Work1-2 (横型時刻表あり)",
			Remarks: "横型時刻表サンプルデータ付き",
			HasETrainTimetable: true,
			ETrainTimetableContentType: (int)ContentType.PNG,
			ETrainTimetableContent: SampleHorizontalTimetableImageData
		),
	};

	static readonly List<TrainData> TrainDataList = new()
	{
		new(Id: TRAIN_1_1_1, Direction: Direction.Inbound, TrainNumber: "Train01"),
		new(Id: TRAIN_1_1_2, Direction: Direction.Inbound, TrainNumber: "Train02"),
		new(Id: TRAIN_1_1_3, Direction: Direction.Inbound, TrainNumber: "Train03"),
	};

	static readonly List<TrainData> TrainDataList_Work1_2 = new()
	{
		new(Id: TRAIN_1_2_1, Direction: Direction.Inbound, TrainNumber: "Train01 (横型)"),
	};

	static readonly TrainData SampleTrainData_Work1_2 = new(
		Id: TRAIN_1_2_1,
		WorkName: "Work1-2 (横型時刻表あり)",
		AffectDate: new(2022, 9, 16),
		TrainNumber: "試単9094",
		MaxSpeed: "100",
		SpeedType: "特定",
		NominalTractiveCapacity: null,
		CarCount: 5,
		Destination: "終点",
		BeginRemarks: "(入換)",
		AfterRemarks: null,
		Remarks: "横型時刻表サンプルデータのある列車です",
		BeforeDeparture: null,
		TrainInfo: null,
		Rows: new[]
		{
			new TimetableRow(
				Id: "1",
				Location: new(1),
				DriveTimeMM: 0,
				DriveTimeSS: 0,
				StationName: "始発駅",
				IsOperationOnlyStop: false,
				IsPass: false,
				HasBracket: true,
				IsLastStop: false,
				ArriveTime: null,
				DepartureTime: new(10, 0, 0, null),
				TrackName: "1",
				RunInLimit: null,
				RunOutLimit: null,
				Remarks: null
			),
			new TimetableRow(
				Id: "2",
				Location: new(2),
				DriveTimeMM: 5,
				DriveTimeSS: 0,
				StationName: "中間駅",
				IsOperationOnlyStop: false,
				IsPass: false,
				HasBracket: false,
				IsLastStop: false,
				ArriveTime: new(10, 5, 0, null),
				DepartureTime: new(10, 6, 0, null),
				TrackName: "2",
				RunInLimit: null,
				RunOutLimit: null,
				Remarks: null
			),
			new TimetableRow(
				Id: "3",
				Location: new(3),
				DriveTimeMM: 10,
				DriveTimeSS: 0,
				StationName: "終点駅",
				IsOperationOnlyStop: false,
				IsPass: false,
				HasBracket: false,
				IsLastStop: true,
				ArriveTime: new(10, 16, 0, null),
				DepartureTime: null,
				TrackName: "3",
				RunInLimit: null,
				RunOutLimit: null,
				Remarks: null
			),
		},
		Direction: Direction.Outbound
	);

	static readonly TrainData SampleTrainData = new(
		Id: TRAIN_1_1_1,
		WorkName: "Work1-1",
		AffectDate: new(2022, 9, 16),
		TrainNumber: "試単9091",
		MaxSpeed: "130\nオオ~  15",
		SpeedType: "特定\nトウ~　　　＊＊\nオオ~　　　特定",
		NominalTractiveCapacity: "ＨＰ９９９形\n現車　　　１両\n換算　２０.０",
		CarCount: 1,
		Destination: "終点",
		BeginRemarks: "(入換)",
		AfterRemarks: "(入区)",
		Remarks: "サンプルデータです。\n車掌省略",
		BeforeDeparture: "転線   10分",
		TrainInfo: "<span style=\"color:red\">車掌省略</span>",

		// BeforeDepartureOnStationTrackCol: "転線",

		AfterArrive: "入換   20分",
		// AfterArriveOnStationTrackCol: "入換",

		NextTrainId: TRAIN_1_1_2,

		Rows: new[]
		{
			new TimetableRow(
				Id: "1",
				Location: new(1),
				DriveTimeMM: 0,
				DriveTimeSS: 0,
				StationName: "駅１",
				IsOperationOnlyStop: false,
				IsPass: false,
				HasBracket: true,
				IsLastStop: false,
				ArriveTime: new(1,23,45,null),
				DepartureTime: new(1,25,null, null),
				TrackName: "1",
				RunInLimit: null,
				RunOutLimit: 30,
				Remarks: "記事"
			),
			new TimetableRow(
				Id: "2",
				Location: new(2),
				DriveTimeMM: 10,
				DriveTimeSS: 50,
				StationName: "駅２",
				IsOperationOnlyStop: false,
				IsPass: false,
				HasBracket: false,
				IsLastStop: false,
				ArriveTime: new(null,null,null,null),
				DepartureTime: new(null,26,10, null),
				TrackName: "10",
				RunInLimit: 30,
				RunOutLimit: 30,
				Remarks: "<b>記事</b>"
			),
			new TimetableRow(
				Id: "3",
				Location: new(3),
				DriveTimeMM: 100,
				DriveTimeSS: 50,
				StationName: "駅３",
				IsOperationOnlyStop: true,
				IsPass: false,
				HasBracket: false,
				IsLastStop: false,
				ArriveTime: new(null,23,null,null),
				DepartureTime: new(12,25,null, null),
				TrackName: "<span>着２<br/>発３</span>",
				RunInLimit: 5,
				RunOutLimit: 5,
				Remarks: "転線"
			),
			new TimetableRow(
				Id: "4",
				Location: new(4),
				DriveTimeMM: 1,
				DriveTimeSS: null,
				StationName: "東京",
				IsOperationOnlyStop: false,
				IsPass: false,
				HasBracket: false,
				IsLastStop: false,
				ArriveTime: new(1,23,45,null),
				DepartureTime: new(1,25,null, null),
				TrackName: """<span style="color: aqua">1</span>""",
				RunInLimit: null,
				RunOutLimit: null,
				Remarks: "記事"
			),
			new TimetableRow(
				Id: "5",
				Location: new(5),
				DriveTimeMM: null,
				DriveTimeSS: 5,
				StationName: "津",
				IsOperationOnlyStop: false,
				IsPass: false,
				HasBracket: false,
				IsLastStop: false,
				ArriveTime: new(1,23,45,null),
				DepartureTime: new(1,25,null, null),
				TrackName: "外",
				RunInLimit: null,
				RunOutLimit: 30,
				Remarks: "記事\n任意の内容"
			),
			new TimetableRow(
				Id: "6",
				Location: new(6),
				DriveTimeMM: 4,
				DriveTimeSS: 30,
				StationName: "大宮",
				IsOperationOnlyStop: false,
				IsPass: false,
				HasBracket: false,
				IsLastStop: false,
				ArriveTime: new(1,23,45,null),
				DepartureTime: new(1,25,null, null),
				TrackName: "1",
				RunInLimit: null,
				RunOutLimit: 30,
				Remarks: "記事"
			),
			new TimetableRow(
				Id: "7",
				Location: new(6),
				DriveTimeMM: null,
				DriveTimeSS: null,
				StationName: "<span style=\"color:royalblue\">交  直  切  換</span>",
				IsOperationOnlyStop: false,
				IsPass: false,
				HasBracket: false,
				IsLastStop: false,
				ArriveTime: null,
				DepartureTime: null,
				TrackName: null,
				RunInLimit: null,
				RunOutLimit: null,
				Remarks: null,
				IsInfoRow: true
			),
			new TimetableRow(
				Id: "8",
				Location: new(7),
				DriveTimeMM: null,
				DriveTimeSS: null,
				StationName: "南浦和",
				IsOperationOnlyStop: false,
				IsPass: false,
				HasBracket: false,
				IsLastStop: false,
				ArriveTime: new(1,23,45,null),
				DepartureTime: new(1,25,null, null),
				TrackName: "3文字",
				RunInLimit: null,
				RunOutLimit: 30,
				Remarks: "記事"
			),
			new TimetableRow(
				Id: "9",
				Location: new(8),
				DriveTimeMM: null,
				DriveTimeSS: 5,
				StationName: "さ新都心",
				IsOperationOnlyStop: false,
				IsPass: false,
				HasBracket: false,
				IsLastStop: false,
				ArriveTime: new(1,23,45,null),
				DepartureTime: new(1,25,null, null),
				TrackName: "四文字版",
				RunInLimit: null,
				RunOutLimit: 30,
				Remarks: "記事"
			),
			new TimetableRow(
				Id: "10",
				Location: new(6),
				DriveTimeMM: null,
				DriveTimeSS: null,
				StationName: "<span style=\"color:red\">交  直  切  換  2</span>",
				IsOperationOnlyStop: false,
				IsPass: false,
				HasBracket: false,
				IsLastStop: false,
				ArriveTime: null,
				DepartureTime: null,
				TrackName: null,
				RunInLimit: null,
				RunOutLimit: null,
				Remarks: null,
				IsInfoRow: true
			),
			new TimetableRow(
				Id: "11",
				Location: new(6),
				DriveTimeMM: null,
				DriveTimeSS: null,
				StationName: "<span style=\"color:royalblue\">交  直  切  換  3</span>",
				IsOperationOnlyStop: false,
				IsPass: false,
				HasBracket: false,
				IsLastStop: false,
				ArriveTime: null,
				DepartureTime: null,
				TrackName: null,
				RunInLimit: null,
				RunOutLimit: null,
				Remarks: null,
				IsInfoRow: true
			),
			new TimetableRow(
				Id: "12",
				Location: new(9),
				DriveTimeMM: 1,
				DriveTimeSS: null,
				StationName: "赤羽",
				IsOperationOnlyStop: false,
				IsPass: false,
				HasBracket: false,
				IsLastStop: false,
				ArriveTime: new(1,23,45,null),
				DepartureTime: new(1,25,null, null),
				TrackName: "1\n三文字",
				RunInLimit: null,
				RunOutLimit: 30,
				Remarks: "記事"
			),
			new TimetableRow(
				Id: "13",
				Location: new(10),
				DriveTimeMM: 10,
				DriveTimeSS: 0,
				StationName: "駅１０",
				IsOperationOnlyStop: false,
				IsPass: false,
				HasBracket: false,
				IsLastStop: false,
				ArriveTime: new(1,23,45,null),
				DepartureTime: new(1,25,null, null),
				TrackName: "1\n四文字版",
				RunInLimit: null,
				RunOutLimit: 30,
				Remarks: "記事"
			),
			new TimetableRow(
				Id: "14",
				Location: new(11),
				DriveTimeMM: 1,
				DriveTimeSS: 5,
				StationName: "駅１１",
				IsOperationOnlyStop: true,
				IsPass: false,
				HasBracket: false,
				IsLastStop: false,
				ArriveTime: new(1,23,45,null),
				DepartureTime: new(1,25,null, null),
				TrackName: "11\n三文字",
				RunInLimit: null,
				RunOutLimit: 30,
				Remarks: "記事"
			),
			new TimetableRow(
				Id: "15",
				Location: new(12),
				DriveTimeMM: 1,
				DriveTimeSS: 5,
				StationName: "駅１２",
				IsOperationOnlyStop: false,
				IsPass: false,
				HasBracket: false,
				IsLastStop: false,
				ArriveTime: new(null,null,45,null),
				DepartureTime: new(1,null,5, null),
				TrackName: null,
				RunInLimit: null,
				RunOutLimit: 130,
				Remarks: "記事"
			),
			new TimetableRow(
				Id: "16",
				Location: new(13),
				DriveTimeMM: 1,
				DriveTimeSS: 5,
				StationName: "駅１３",
				IsOperationOnlyStop: false,
				IsPass: true,
				HasBracket: false,
				IsLastStop: false,
				ArriveTime: new(null,null,null,"↓"),
				DepartureTime: new(null,null,null, "通過"),
				TrackName: "123\n四文字版",
				RunInLimit: 130,
				RunOutLimit: 30,
				Remarks: "記事"
			),
			new TimetableRow(
				Id: "17",
				Location: new(14),
				DriveTimeMM: 1,
				DriveTimeSS: 5,
				StationName: "駅１４",
				IsOperationOnlyStop: false,
				IsPass: false,
				HasBracket: false,
				IsLastStop: false,
				ArriveTime: null,
				DepartureTime: null,
				TrackName: "1",
				RunInLimit: null,
				RunOutLimit: 30,
				Remarks: "記事"
			),
			new TimetableRow(
				Id: "18",
				Location: new(15),
				DriveTimeMM: 1,
				DriveTimeSS: 5,
				StationName: "駅１５",
				IsOperationOnlyStop: false,
				IsPass: false,
				HasBracket: false,
				IsLastStop: true,
				ArriveTime: new(1,23,45,null),
				DepartureTime: null,
				TrackName: "1",
				RunInLimit: null,
				RunOutLimit: 30,
				Remarks: "記事"
			),
		},
		Direction: Direction.Outbound
	);

	static readonly TrainData SampleTrainData2 = new(
		Id: TRAIN_1_1_2,
		WorkName: "Work1-1",
		AffectDate: null,
		TrainNumber: "試単9092",
		MaxSpeed: null,
		SpeedType: null,
		NominalTractiveCapacity: null,
		CarCount: null,
		Destination: "長い駅名",
		BeginRemarks: "(入換)\n(入換)",
		AfterRemarks: "(入換)\n(入換)",
		Remarks: null,
		BeforeDeparture: null,
		TrainInfo: null,
		NextTrainId: TRAIN_1_1_3,
		Rows: new[]
		{
			new TimetableRow(
				Id: "1",
				Location: new(1),
				DriveTimeMM: 1,
				DriveTimeSS: 5,
				StationName: "駅１",
				IsOperationOnlyStop: false,
				IsPass: false,
				HasBracket: true,
				IsLastStop: false,
				ArriveTime: new(1,23,45,null),
				DepartureTime: new(1,25,null, null),
				TrackName: "1",
				RunInLimit: null,
				RunOutLimit: 30,
				Remarks: "記事"
			),
			new TimetableRow(
				Id: "2",
				Location: new(2),
				DriveTimeMM: 1,
				DriveTimeSS: 5,
				StationName: "駅２",
				IsOperationOnlyStop: false,
				IsPass: false,
				HasBracket: false,
				IsLastStop: false,
				ArriveTime: new(null,null,null,null),
				DepartureTime: new(null,26,10, null),
				TrackName: "10",
				RunInLimit: 30,
				RunOutLimit: 30,
				Remarks: "<b>記事</b>"
			),
			new TimetableRow(
				Id: "3",
				Location: new(3),
				DriveTimeMM: 1,
				DriveTimeSS: 5,
				StationName: "駅３",
				IsOperationOnlyStop: true,
				IsPass: false,
				HasBracket: false,
				IsLastStop: false,
				ArriveTime: new(null,23,null,null),
				DepartureTime: new(12,25,null, null),
				TrackName: "<span>着２<br/>発３</span>",
				RunInLimit: 5,
				RunOutLimit: 5,
				Remarks: "転線"
			),
			new TimetableRow(
				Id: "4",
				Location: new(4),
				DriveTimeMM: 1,
				DriveTimeSS: 5,
				StationName: "東京",
				IsOperationOnlyStop: false,
				IsPass: false,
				HasBracket: false,
				IsLastStop: false,
				ArriveTime: new(1,23,45,null),
				DepartureTime: new(1,25,null, null),
				TrackName: "1",
				RunInLimit: null,
				RunOutLimit: null,
				Remarks: "記事"
			),
			new TimetableRow(
				Id: "5",
				Location: new(5),
				DriveTimeMM: 1,
				DriveTimeSS: 5,
				StationName: "津",
				IsOperationOnlyStop: false,
				IsPass: false,
				HasBracket: false,
				IsLastStop: false,
				ArriveTime: new(1,23,45,null),
				DepartureTime: new(1,25,null, null),
				TrackName: "外",
				RunInLimit: null,
				RunOutLimit: 30,
				Remarks: "記事\n任意の内容"
			),
		},
		Direction: Direction.Outbound
	);

	static readonly TrainData SampleTrainData3 = new(
		Id: TRAIN_1_1_3,
		WorkName: "Work1-1",
		AffectDate: null,
		TrainNumber: "試単9093",
		MaxSpeed: null,
		SpeedType: null,
		NominalTractiveCapacity: null,
		CarCount: null,
		Destination: null,
		BeginRemarks: null,
		AfterRemarks: null,
		Remarks: null,
		BeforeDeparture: null,
		TrainInfo: null,

		DayCount: 1,

		AfterArrive: "入換   20分",
		// AfterArriveOnStationTrackCol: "入換",

		Rows: new[]
		{
			new TimetableRow(
				Id: "1",
				Location: new(1),
				DriveTimeMM: 1,
				DriveTimeSS: 5,
				StationName: "駅１",
				IsOperationOnlyStop: false,
				IsPass: false,
				HasBracket: true,
				IsLastStop: false,
				ArriveTime: new(1,23,45,null),
				DepartureTime: new(1,25,null, null),
				TrackName: "1",
				RunInLimit: null,
				RunOutLimit: 30,
				Remarks: "記事"
			),
			new TimetableRow(
				Id: "2",
				Location: new(2),
				DriveTimeMM: 1,
				DriveTimeSS: 5,
				StationName: "駅２",
				IsOperationOnlyStop: false,
				IsPass: false,
				HasBracket: false,
				IsLastStop: false,
				ArriveTime: new(null,null,null,null),
				DepartureTime: new(null,26,10, null),
				TrackName: "10",
				RunInLimit: 30,
				RunOutLimit: 30,
				Remarks: "<b>記事</b>"
			),
		},
		Direction: Direction.Outbound
	);

	public void Dispose() { }

	public TrainData? GetTrainData(string trainId)
		=> trainId switch
		{
			TRAIN_1_1_1 => SampleTrainData,
			TRAIN_1_1_2 => SampleTrainData2,
			TRAIN_1_1_3 => SampleTrainData3,
			TRAIN_1_2_1 => SampleTrainData_Work1_2,
			_ => null
		};

	public IReadOnlyList<TrainData> GetTrainDataList(string workId)
		=> workId switch
		{
			WORK_1_1 => TrainDataList,
			WORK_1_2 => TrainDataList_Work1_2,
			_ => TrainDataList
		};

	public IReadOnlyList<WorkGroup> GetWorkGroupList()
		=> WorkGroupList;

	public IReadOnlyList<Work> GetWorkList(string workGroupId)
		=> WorkList;
}
