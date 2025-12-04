using TRViS.IO.Models;

namespace TRViS;

/// <summary>
/// Test data loader for Hako Tab ordering functionality
/// Tests various NextTrainId chain patterns to ensure correct display order
/// </summary>
public class HakoTabOrderTestDataLoader : TRViS.IO.ILoader
{
	internal const string WORK_GROUP_ID = "hako-order-test";
	internal const string WORK_LINEAR = "work-linear";
	internal const string WORK_REVERSE = "work-reverse";
	internal const string WORK_MULTIPLE = "work-multiple";
	internal const string WORK_ISOLATED = "work-isolated";
	internal const string WORK_PARTIAL = "work-partial";
	internal const string WORK_CIRCULAR = "work-circular";
	internal const string WORK_DAYCOUNT = "work-daycount";

	// Pattern 1: Linear chain (Train1 -> Train2 -> Train3)
	const string TRAIN_LINEAR_1 = "linear-train-1";
	const string TRAIN_LINEAR_2 = "linear-train-2";
	const string TRAIN_LINEAR_3 = "linear-train-3";

	// Pattern 2: Reverse chain (Train3 -> Train2 -> Train1)
	const string TRAIN_REVERSE_1 = "reverse-train-1";
	const string TRAIN_REVERSE_2 = "reverse-train-2";
	const string TRAIN_REVERSE_3 = "reverse-train-3";

	// Pattern 3: Multiple chains (Chain A: Train1 -> Train2, Chain B: Train3 -> Train4)
	const string TRAIN_CHAIN_A_1 = "chain-a-train-1";
	const string TRAIN_CHAIN_A_2 = "chain-a-train-2";
	const string TRAIN_CHAIN_B_1 = "chain-b-train-1";
	const string TRAIN_CHAIN_B_2 = "chain-b-train-2";

	// Pattern 4: Isolated trains with no chains
	const string TRAIN_ISOLATED_1 = "isolated-train-1";
	const string TRAIN_ISOLATED_2 = "isolated-train-2";

	// Pattern 5: Partial chain (breaks in the middle due to invalid reference)
	const string TRAIN_PARTIAL_1 = "partial-train-1";
	const string TRAIN_PARTIAL_2 = "partial-train-2";
	const string TRAIN_PARTIAL_INVALID = "partial-train-3";  // References non-existent train

	// Pattern 6: Circular reference (Train1 -> Train2 -> Train1)
	const string TRAIN_CIRCULAR_1 = "circular-train-1";
	const string TRAIN_CIRCULAR_2 = "circular-train-2";

	// Pattern 7: Two chains with different DayCount values
	const string TRAIN_DAYCOUNT_A_1 = "daycount-a-train-1";
	const string TRAIN_DAYCOUNT_A_2 = "daycount-a-train-2";
	const string TRAIN_DAYCOUNT_B_1 = "daycount-b-train-1";
	const string TRAIN_DAYCOUNT_B_2 = "daycount-b-train-2";

	static readonly List<WorkGroup> WorkGroupList =
	[
		new(Id: WORK_GROUP_ID, Name: "ハコタブ順序確認"),
	];

	static readonly List<Work> WorkList =
	[
		new(Id: WORK_LINEAR, WorkGroupId: WORK_GROUP_ID, Name: "線形連結リスト"),
		new(Id: WORK_REVERSE, WorkGroupId: WORK_GROUP_ID, Name: "逆順連結リスト"),
		new(Id: WORK_MULTIPLE, WorkGroupId: WORK_GROUP_ID, Name: "複数連結リスト"),
		new(Id: WORK_ISOLATED, WorkGroupId: WORK_GROUP_ID, Name: "孤立列車"),
		new(Id: WORK_PARTIAL, WorkGroupId: WORK_GROUP_ID, Name: "部分連結リスト"),
		new(Id: WORK_CIRCULAR, WorkGroupId: WORK_GROUP_ID, Name: "循環参照"),
		new(Id: WORK_DAYCOUNT, WorkGroupId: WORK_GROUP_ID, Name: "日付跨ぎ連結リスト"),
	];

	static readonly List<TrainData> TrainDataList =
	[
		// Pattern 1: Linear chain
		new(Id: TRAIN_LINEAR_1, Direction: Direction.Outbound, TrainNumber: "Linear01"),
		new(Id: TRAIN_LINEAR_2, Direction: Direction.Outbound, TrainNumber: "Linear02"),
		new(Id: TRAIN_LINEAR_3, Direction: Direction.Outbound, TrainNumber: "Linear03"),

		// Pattern 2: Reverse chain
		new(Id: TRAIN_REVERSE_1, Direction: Direction.Outbound, TrainNumber: "Reverse01"),
		new(Id: TRAIN_REVERSE_2, Direction: Direction.Outbound, TrainNumber: "Reverse02"),
		new(Id: TRAIN_REVERSE_3, Direction: Direction.Outbound, TrainNumber: "Reverse03"),

		// Pattern 3: Multiple chains
		new(Id: TRAIN_CHAIN_A_1, Direction: Direction.Outbound, TrainNumber: "ChainA01"),
		new(Id: TRAIN_CHAIN_A_2, Direction: Direction.Outbound, TrainNumber: "ChainA02"),
		new(Id: TRAIN_CHAIN_B_1, Direction: Direction.Outbound, TrainNumber: "ChainB01"),
		new(Id: TRAIN_CHAIN_B_2, Direction: Direction.Outbound, TrainNumber: "ChainB02"),

		// Pattern 4: Isolated trains
		new(Id: TRAIN_ISOLATED_1, Direction: Direction.Outbound, TrainNumber: "Isolated01"),
		new(Id: TRAIN_ISOLATED_2, Direction: Direction.Outbound, TrainNumber: "Isolated02"),

		// Pattern 5: Partial chain
		new(Id: TRAIN_PARTIAL_1, Direction: Direction.Outbound, TrainNumber: "Partial01"),
		new(Id: TRAIN_PARTIAL_2, Direction: Direction.Outbound, TrainNumber: "Partial02"),
		new(Id: TRAIN_PARTIAL_INVALID, Direction: Direction.Outbound, TrainNumber: "PartialInv"),

		// Pattern 6: Circular reference
		new(Id: TRAIN_CIRCULAR_1, Direction: Direction.Outbound, TrainNumber: "Circular01"),
		new(Id: TRAIN_CIRCULAR_2, Direction: Direction.Outbound, TrainNumber: "Circular02"),

		// Pattern 7: Different DayCount
		new(Id: TRAIN_DAYCOUNT_A_1, Direction: Direction.Outbound, TrainNumber: "DayCountA01"),
		new(Id: TRAIN_DAYCOUNT_A_2, Direction: Direction.Outbound, TrainNumber: "DayCountA02"),
		new(Id: TRAIN_DAYCOUNT_B_1, Direction: Direction.Outbound, TrainNumber: "DayCountB01"),
		new(Id: TRAIN_DAYCOUNT_B_2, Direction: Direction.Outbound, TrainNumber: "DayCountB02"),
	];

	static readonly List<TrainData> LinearTrainDataList =
	[
		new(Id: TRAIN_LINEAR_1, Direction: Direction.Outbound, TrainNumber: "Linear01"),
		new(Id: TRAIN_LINEAR_2, Direction: Direction.Outbound, TrainNumber: "Linear02"),
		new(Id: TRAIN_LINEAR_3, Direction: Direction.Outbound, TrainNumber: "Linear03"),
	];

	static readonly List<TrainData> ReverseTrainDataList =
	[
		new(Id: TRAIN_REVERSE_1, Direction: Direction.Outbound, TrainNumber: "Reverse01"),
		new(Id: TRAIN_REVERSE_2, Direction: Direction.Outbound, TrainNumber: "Reverse02"),
		new(Id: TRAIN_REVERSE_3, Direction: Direction.Outbound, TrainNumber: "Reverse03"),
	];

	static readonly List<TrainData> MultipleTrainDataList =
	[
		new(Id: TRAIN_CHAIN_A_1, Direction: Direction.Outbound, TrainNumber: "ChainA01"),
		new(Id: TRAIN_CHAIN_B_1, Direction: Direction.Outbound, TrainNumber: "ChainB01"),
		new(Id: TRAIN_CHAIN_A_2, Direction: Direction.Outbound, TrainNumber: "ChainA02"),
		new(Id: TRAIN_CHAIN_B_2, Direction: Direction.Outbound, TrainNumber: "ChainB02"),
	];

	static readonly List<TrainData> IsolatedTrainDataList =
	[
		new(Id: TRAIN_ISOLATED_1, Direction: Direction.Outbound, TrainNumber: "Isolated01"),
		new(Id: TRAIN_ISOLATED_2, Direction: Direction.Outbound, TrainNumber: "Isolated02"),
	];

	static readonly List<TrainData> PartialTrainDataList =
	[
		new(Id: TRAIN_PARTIAL_1, Direction: Direction.Outbound, TrainNumber: "Partial01"),
		new(Id: TRAIN_PARTIAL_2, Direction: Direction.Outbound, TrainNumber: "Partial02"),
		new(Id: TRAIN_PARTIAL_INVALID, Direction: Direction.Outbound, TrainNumber: "PartialInv"),
	];

	static readonly List<TrainData> CircularTrainDataList =
	[
		new(Id: TRAIN_CIRCULAR_1, Direction: Direction.Outbound, TrainNumber: "Circular01"),
		new(Id: TRAIN_CIRCULAR_2, Direction: Direction.Outbound, TrainNumber: "Circular02"),
	];

	static readonly List<TrainData> DayCountTrainDataList =
	[
		new(Id: TRAIN_DAYCOUNT_A_1, Direction: Direction.Outbound, TrainNumber: "DayCountA01"),
		new(Id: TRAIN_DAYCOUNT_B_1, Direction: Direction.Outbound, TrainNumber: "DayCountB01"),
		new(Id: TRAIN_DAYCOUNT_A_2, Direction: Direction.Outbound, TrainNumber: "DayCountA02"),
		new(Id: TRAIN_DAYCOUNT_B_2, Direction: Direction.Outbound, TrainNumber: "DayCountB02"),
	];

	// Pattern 1: Linear chain (Linear1 -> Linear2 -> Linear3)
	static readonly TrainData LinearTrain1 = new(
		Id: TRAIN_LINEAR_1,
		WorkName: "順序確認用作業",
		TrainNumber: "Linear01",
		Direction: Direction.Outbound,
		NextTrainId: TRAIN_LINEAR_2,
		Rows: new[] { CreateSampleRow(1, "駅A", "駅B") }
	);

	static readonly TrainData LinearTrain2 = new(
		Id: TRAIN_LINEAR_2,
		WorkName: "順序確認用作業",
		TrainNumber: "Linear02",
		Direction: Direction.Outbound,
		NextTrainId: TRAIN_LINEAR_3,
		Rows: new[] { CreateSampleRow(2, "駅B", "駅C") }
	);

	static readonly TrainData LinearTrain3 = new(
		Id: TRAIN_LINEAR_3,
		WorkName: "順序確認用作業",
		TrainNumber: "Linear03",
		Direction: Direction.Outbound,
		NextTrainId: null,
		Rows: new[] { CreateSampleRow(3, "駅C", "駅D") }
	);

	// Pattern 2: Reverse chain (Reverse3 -> Reverse2 -> Reverse1)
	static readonly TrainData ReverseTrain1 = new(
		Id: TRAIN_REVERSE_1,
		WorkName: "順序確認用作業",
		TrainNumber: "Reverse01",
		Direction: Direction.Outbound,
		NextTrainId: null,
		Rows: new[] { CreateSampleRow(1, "駅A", "駅B") }
	);

	static readonly TrainData ReverseTrain2 = new(
		Id: TRAIN_REVERSE_2,
		WorkName: "順序確認用作業",
		TrainNumber: "Reverse02",
		Direction: Direction.Outbound,
		NextTrainId: TRAIN_REVERSE_1,
		Rows: new[] { CreateSampleRow(2, "駅B", "駅C") }
	);

	static readonly TrainData ReverseTrain3 = new(
		Id: TRAIN_REVERSE_3,
		WorkName: "順序確認用作業",
		TrainNumber: "Reverse03",
		Direction: Direction.Outbound,
		NextTrainId: TRAIN_REVERSE_2,
		Rows: new[] { CreateSampleRow(3, "駅C", "駅D") }
	);

	// Pattern 3: Multiple chains
	static readonly TrainData ChainATrain1 = new(
		Id: TRAIN_CHAIN_A_1,
		WorkName: "順序確認用作業",
		TrainNumber: "ChainA01",
		Direction: Direction.Outbound,
		NextTrainId: TRAIN_CHAIN_A_2,
		Rows: new[] { CreateSampleRow(1, "駅A", "駅B") }
	);

	static readonly TrainData ChainATrain2 = new(
		Id: TRAIN_CHAIN_A_2,
		WorkName: "順序確認用作業",
		TrainNumber: "ChainA02",
		Direction: Direction.Outbound,
		NextTrainId: null,
		Rows: new[] { CreateSampleRow(2, "駅B", "駅C") }
	);

	static readonly TrainData ChainBTrain1 = new(
		Id: TRAIN_CHAIN_B_1,
		WorkName: "順序確認用作業",
		TrainNumber: "ChainB01",
		Direction: Direction.Outbound,
		NextTrainId: TRAIN_CHAIN_B_2,
		Rows: new[] { CreateSampleRow(1, "駅A", "駅B") }
	);

	static readonly TrainData ChainBTrain2 = new(
		Id: TRAIN_CHAIN_B_2,
		WorkName: "順序確認用作業",
		TrainNumber: "ChainB02",
		Direction: Direction.Outbound,
		NextTrainId: null,
		Rows: new[] { CreateSampleRow(2, "駅B", "駅C") }
	);

	// Pattern 4: Isolated trains
	static readonly TrainData IsolatedTrain1 = new(
		Id: TRAIN_ISOLATED_1,
		WorkName: "順序確認用作業",
		TrainNumber: "Isolated01",
		Direction: Direction.Outbound,
		NextTrainId: null,
		Rows: new[] { CreateSampleRow(1, "駅A", "駅B") }
	);

	static readonly TrainData IsolatedTrain2 = new(
		Id: TRAIN_ISOLATED_2,
		WorkName: "順序確認用作業",
		TrainNumber: "Isolated02",
		Direction: Direction.Outbound,
		NextTrainId: null,
		Rows: new[] { CreateSampleRow(1, "駅A", "駅B") }
	);

	// Pattern 5: Partial chain (breaks because TRAIN_NONEXISTENT doesn't exist)
	static readonly TrainData PartialTrain1 = new(
		Id: TRAIN_PARTIAL_1,
		WorkName: "順序確認用作業",
		TrainNumber: "Partial01",
		Direction: Direction.Outbound,
		NextTrainId: TRAIN_PARTIAL_2,
		Rows: new[] { CreateSampleRow(1, "駅A", "駅B") }
	);

	static readonly TrainData PartialTrain2 = new(
		Id: TRAIN_PARTIAL_2,
		WorkName: "順序確認用作業",
		TrainNumber: "Partial02",
		Direction: Direction.Outbound,
		NextTrainId: "nonexistent-train-id",  // Invalid reference - chain breaks here
		Rows: new[] { CreateSampleRow(2, "駅B", "駅C") }
	);

	static readonly TrainData PartialTrainInvalid = new(
		Id: TRAIN_PARTIAL_INVALID,
		WorkName: "順序確認用作業",
		TrainNumber: "PartialInv",
		Direction: Direction.Outbound,
		NextTrainId: null,
		Rows: new[] { CreateSampleRow(1, "駅A", "駅B") }
	);

	// Pattern 6: Circular reference (Circular1 -> Circular2 -> Circular1)
	static readonly TrainData CircularTrain1 = new(
		Id: TRAIN_CIRCULAR_1,
		WorkName: "順序確認用作業",
		TrainNumber: "Circular01",
		Direction: Direction.Outbound,
		NextTrainId: TRAIN_CIRCULAR_2,
		Rows: new[] { CreateSampleRow(1, "駅A", "駅B") }
	);

	static readonly TrainData CircularTrain2 = new(
		Id: TRAIN_CIRCULAR_2,
		WorkName: "順序確認用作業",
		TrainNumber: "Circular02",
		Direction: Direction.Outbound,
		NextTrainId: TRAIN_CIRCULAR_1,  // Circular reference
		Rows: new[] { CreateSampleRow(2, "駅B", "駅C") }
	);

	// Pattern 7: Two chains with different DayCount
	static readonly TrainData DayCountATrain1 = new(
		Id: TRAIN_DAYCOUNT_A_1,
		WorkName: "順序確認用作業",
		TrainNumber: "DayCountA01",
		Direction: Direction.Outbound,
		DayCount: 1,  // Next day
		NextTrainId: TRAIN_DAYCOUNT_A_2,
		Rows: new[] { CreateSampleRow(1, "駅A", "駅B") }
	);

	static readonly TrainData DayCountATrain2 = new(
		Id: TRAIN_DAYCOUNT_A_2,
		WorkName: "順序確認用作業",
		TrainNumber: "DayCountA02",
		Direction: Direction.Outbound,
		DayCount: 1,  // Next day
		NextTrainId: null,
		Rows: new[] { CreateSampleRow(2, "駅B", "駅C") }
	);

	static readonly TrainData DayCountBTrain1 = new(
		Id: TRAIN_DAYCOUNT_B_1,
		WorkName: "順序確認用作業",
		TrainNumber: "DayCountB01",
		Direction: Direction.Outbound,
		DayCount: 0,  // Same day
		NextTrainId: TRAIN_DAYCOUNT_B_2,
		Rows: new[] { CreateSampleRow(1, "駅A", "駅B") }
	);

	static readonly TrainData DayCountBTrain2 = new(
		Id: TRAIN_DAYCOUNT_B_2,
		WorkName: "順序確認用作業",
		TrainNumber: "DayCountB02",
		Direction: Direction.Outbound,
		DayCount: 0,  // Same day
		NextTrainId: null,
		Rows: new[] { CreateSampleRow(2, "駅B", "駅C") }
	);

	private static TimetableRow CreateSampleRow(int index, string fromStation, string toStation)
	{
		return new TimetableRow(
			Id: index.ToString(),
			Location: new(index),
			DriveTimeMM: 10,
			DriveTimeSS: 0,
			StationName: fromStation,
			IsOperationOnlyStop: false,
			IsPass: false,
			HasBracket: false,
			IsLastStop: false,
			ArriveTime: new(10, 0, 0, null),
			DepartureTime: new(10, 5, null, null),
			TrackName: "1",
			RunInLimit: null,
			RunOutLimit: null,
			Remarks: null
		);
	}

	public void Dispose() { }

	public TrainData? GetTrainData(string trainId)
		=> trainId switch
		{
			// Pattern 1
			TRAIN_LINEAR_1 => LinearTrain1,
			TRAIN_LINEAR_2 => LinearTrain2,
			TRAIN_LINEAR_3 => LinearTrain3,

			// Pattern 2
			TRAIN_REVERSE_1 => ReverseTrain1,
			TRAIN_REVERSE_2 => ReverseTrain2,
			TRAIN_REVERSE_3 => ReverseTrain3,

			// Pattern 3
			TRAIN_CHAIN_A_1 => ChainATrain1,
			TRAIN_CHAIN_A_2 => ChainATrain2,
			TRAIN_CHAIN_B_1 => ChainBTrain1,
			TRAIN_CHAIN_B_2 => ChainBTrain2,

			// Pattern 4
			TRAIN_ISOLATED_1 => IsolatedTrain1,
			TRAIN_ISOLATED_2 => IsolatedTrain2,

			// Pattern 5
			TRAIN_PARTIAL_1 => PartialTrain1,
			TRAIN_PARTIAL_2 => PartialTrain2,
			TRAIN_PARTIAL_INVALID => PartialTrainInvalid,

			// Pattern 6
			TRAIN_CIRCULAR_1 => CircularTrain1,
			TRAIN_CIRCULAR_2 => CircularTrain2,

			// Pattern 7
			TRAIN_DAYCOUNT_A_1 => DayCountATrain1,
			TRAIN_DAYCOUNT_A_2 => DayCountATrain2,
			TRAIN_DAYCOUNT_B_1 => DayCountBTrain1,
			TRAIN_DAYCOUNT_B_2 => DayCountBTrain2,

			_ => null
		};

	public IReadOnlyList<TrainData> GetTrainDataList(string workId)
		=> workId switch
		{
			WORK_LINEAR => LinearTrainDataList,
			WORK_REVERSE => ReverseTrainDataList,
			WORK_MULTIPLE => MultipleTrainDataList,
			WORK_ISOLATED => IsolatedTrainDataList,
			WORK_PARTIAL => PartialTrainDataList,
			WORK_CIRCULAR => CircularTrainDataList,
			WORK_DAYCOUNT => DayCountTrainDataList,
			_ => TrainDataList, // fallback for backward compatibility
		};

	public IReadOnlyList<WorkGroup> GetWorkGroupList()
		=> WorkGroupList;

	public IReadOnlyList<Work> GetWorkList(string workGroupId)
		=> WorkList;
}

