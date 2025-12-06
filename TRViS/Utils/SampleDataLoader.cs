using TRViS.IO.Models;

namespace TRViS;

public class SampleDataLoader : TRViS.IO.ILoader
{
	const string WORK_GROUP_1 = "1";
	const string WORK_1_1 = "1-1";
	const string TRAIN_1_1_1 = "1-1-1";
	const string TRAIN_1_1_2 = "1-1-2";
	const string TRAIN_1_1_3 = "1-1-3";

	static readonly HakoTabOrderTestDataLoader HakoTabOrderTestLoader = new();

	static readonly List<WorkGroup> WorkGroupList =
	[
		new(Id: WORK_GROUP_1, Name: "WorkGroup1"),
		new(Id: HakoTabOrderTestDataLoader.WORK_GROUP_ID, Name: "ハコタブ順序確認"),
	];

	static readonly List<Work> WorkList =
	[
		new(Id: WORK_1_1, WorkGroupId: WORK_GROUP_1, Name: "Work1-1", Remarks: "Sample [b][i]Work[/i][/b] [color=#FF0000 dark=#00FF00]Remark[size=32]s[/size][/color]\nLine 2\nLine 3"),
	];

	static readonly List<TrainData> TrainDataList =
	[
		new(Id: TRAIN_1_1_1, Direction: Direction.Inbound, TrainNumber: "Train01"),
		new(Id: TRAIN_1_1_2, Direction: Direction.Inbound, TrainNumber: "Train02"),
		new(Id: TRAIN_1_1_3, Direction: Direction.Inbound, TrainNumber: "Train03"),
	];

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
		BeginRemarks: "(入換1行目)\n(入換2行目)",
		AfterRemarks: "(入区)",
		Remarks: "サンプルデータです。\n車掌省略",
		BeforeDeparture: "転線   10分",
		TrainInfo: "<span style=\"color:red\">車掌省略</span>",

		// BeforeDepartureOnStationTrackCol: "転線",

		AfterArrive: "入換   20分",
		// AfterArriveOnStationTrackCol: "入換",

		NextTrainId: null,

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
				Remarks: "記事",
				DefaultMarkerColor_RGB: 0x3366CC,
				DefaultMarkerText: "確認"
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
				Remarks: "<b>記事</b>",
				DefaultMarkerColor_RGB: 0xF04020,
				DefaultMarkerText: "注意"
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
				IsOperationOnlyStop: true,
				IsPass: false,
				HasBracket: true,
				IsLastStop: false,
				ArriveTime: new(1,23,45,null),
				DepartureTime: new(12,34,56, null),
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
				StationName: "[b][color=#FF0000 dark=#FF0000]　　　　　交　直　切　換　2[/color][/b]",
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
		NextTrainId: TRAIN_1_1_1,
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
				Remarks: "記事",
				DefaultMarkerColor_RGB: 0x40F020,
				DefaultMarkerText: "発車"
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
		NextTrainId: TRAIN_1_1_2,

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
	{
		var hakoTrainData = HakoTabOrderTestLoader.GetTrainData(trainId);
		if (hakoTrainData is not null)
		{
			return hakoTrainData;
		}

		return trainId switch
		{
			TRAIN_1_1_1 => SampleTrainData,
			TRAIN_1_1_2 => SampleTrainData2,
			TRAIN_1_1_3 => SampleTrainData3,
			_ => null
		};
	}

	public IReadOnlyList<TrainData> GetTrainDataList(string workId)
		=> workId == HakoTabOrderTestDataLoader.WORK_LINEAR ||
			 workId == HakoTabOrderTestDataLoader.WORK_REVERSE ||
			 workId == HakoTabOrderTestDataLoader.WORK_MULTIPLE ||
			 workId == HakoTabOrderTestDataLoader.WORK_ISOLATED ||
			 workId == HakoTabOrderTestDataLoader.WORK_PARTIAL ||
			 workId == HakoTabOrderTestDataLoader.WORK_CIRCULAR ||
			 workId == HakoTabOrderTestDataLoader.WORK_DAYCOUNT
			? HakoTabOrderTestLoader.GetTrainDataList(workId)
			: TrainDataList;

	public IReadOnlyList<WorkGroup> GetWorkGroupList()
		=> WorkGroupList;

	public IReadOnlyList<Work> GetWorkList(string workGroupId)
		=> workGroupId == HakoTabOrderTestDataLoader.WORK_GROUP_ID
			? HakoTabOrderTestLoader.GetWorkList(workGroupId)
			: WorkList;
}
