using TRViS.IO.Models;
using TRViS.IO.Models.DB;

namespace TRViS;

public class SampleDataLoader : TRViS.IO.ILoader
{
	const int WORK_GROUP_1 = 1;
	const int WORK_1_1 = 1;

	static readonly List<WorkGroup> WorkGroupList = new()
	{
		new(){ Id = WORK_GROUP_1, Name = "WorkGroup1" },
	};

	static readonly List<Work> WorkList = new()
	{
		new(){ Id = WORK_1_1, Name = "Work1-1" },
	};

	static readonly List<IO.Models.DB.TrainData> TrainDataList = new()
	{
		new(){ Id = WORK_1_1, TrainNumber = "Train01" },
	};

	static readonly List<TrainDataGroup> TrainDataGroupList = new()
	{
		new("1", "Group01", new[]{ new TrainDataFileInfo("1", "1", "Work01", "Train01") }),
	};

	static readonly IO.Models.TrainData SampleTrainData = new(
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
		Rows: new[]
		{
			new TimetableRow(
				Location: 1,
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
				Location: 2,
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
				Location: 3,
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
				Location: 4,
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
				Location: 5,
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
			new TimetableRow(
				Location: 6,
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
				Location: 7,
				DriveTimeMM: null,
				DriveTimeSS: null,
				StationName: "南浦和",
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
				Location: 8,
				DriveTimeMM: null,
				DriveTimeSS: 5,
				StationName: "さ新都心",
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
				Location: 9,
				DriveTimeMM: 1,
				DriveTimeSS: null,
				StationName: "赤羽",
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
				Location: 10,
				DriveTimeMM: 10,
				DriveTimeSS: 0,
				StationName: "駅１０",
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
				Location: 11,
				DriveTimeMM: 1,
				DriveTimeSS: 5,
				StationName: "駅１１",
				IsOperationOnlyStop: true,
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
				Location: 12,
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
				Location: 13,
				DriveTimeMM: 1,
				DriveTimeSS: 5,
				StationName: "駅１３",
				IsOperationOnlyStop: false,
				IsPass: true,
				HasBracket: false,
				IsLastStop: false,
				ArriveTime: new(null,null,null,"↓"),
				DepartureTime: new(null,null,null, "通過"),
				TrackName: "1",
				RunInLimit: 130,
				RunOutLimit: 30,
				Remarks: "記事"
			),
			new TimetableRow(
				Location: 14,
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
				Location: 15,
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
		1
		);

	public void Dispose() { }

	public IO.Models.TrainData? GetTrainData(int trainId)
		=> SampleTrainData;

	public IReadOnlyList<TrainDataGroup> GetTrainDataGroupList()
		=> TrainDataGroupList;

	public IReadOnlyList<IO.Models.DB.TrainData> GetTrainDataList(int workId)
		=> TrainDataList;

	public IReadOnlyList<WorkGroup> GetWorkGroupList()
		=> WorkGroupList;

	public IReadOnlyList<Work> GetWorkList(int workGroupId)
		=> WorkList;
}
