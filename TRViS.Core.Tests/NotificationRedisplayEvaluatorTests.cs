namespace TRViS.Core.Tests;

public class NotificationRedisplayEvaluatorTests
{
	private static readonly IReadOnlyList<StationRef> Stations =
	[
		new StationRef("s0", "駅A"), // 0
		new StationRef("s1", "駅B"), // 1
		new StationRef("s2", "駅C"), // 2
		new StationRef("s3", "駅D"), // 3
		new StationRef("s4", "駅E"), // 4
		new StationRef("s5", "駅F"), // 5
	];

	[Fact]
	public void EvaluateVisibleKeys_SingleStation_VisibleFromStationsBeforeThroughStation()
	{
		// Arrange: 駅C (index 2) 指定、2 駅前から表示
		var target = new RedisplayTarget("k1", "駅C", null, StationsBefore: 2);

		// Act & Assert: index 0 (= 2 駅前) から index 2 (駅C自体) まで表示
		Assert.Contains("k1", NotificationRedisplayEvaluator.EvaluateVisibleKeys(Stations, 0, [target]));
		Assert.Contains("k1", NotificationRedisplayEvaluator.EvaluateVisibleKeys(Stations, 1, [target]));
		Assert.Contains("k1", NotificationRedisplayEvaluator.EvaluateVisibleKeys(Stations, 2, [target]));
	}

	[Fact]
	public void EvaluateVisibleKeys_SingleStation_HiddenBeforeWindow()
	{
		// Arrange: 駅C (index 2) 指定、2 駅前から表示 → index -1 相当の前は非表示
		var target = new RedisplayTarget("k1", "駅C", null, StationsBefore: 2);

		// Act: window の外側 (low より前) はまだ列にないので、代わりに別の駅リストで検証する。
		// ここでは currentStationIndex が low(=0) より小さくなるケースとして、
		// stationsBefore を使い切る前の状態を負のインデックスで表現する。
		var result = NotificationRedisplayEvaluator.EvaluateVisibleKeys(Stations, -1, [target]);

		// Assert
		Assert.DoesNotContain("k1", result);
	}

	[Fact]
	public void EvaluateVisibleKeys_SingleStation_HiddenAfterWindow()
	{
		// Arrange: 駅C (index 2) 指定、2 駅前から表示。駅通過後 (index 3) は非表示。
		var target = new RedisplayTarget("k1", "駅C", null, StationsBefore: 2);

		// Act
		var result = NotificationRedisplayEvaluator.EvaluateVisibleKeys(Stations, 3, [target]);

		// Assert
		Assert.DoesNotContain("k1", result);
	}

	[Fact]
	public void EvaluateVisibleKeys_Section_WindowIncludesStartBeforeThroughEnd()
	{
		// Arrange: 区間 駅B(1) 〜 駅D(3)、1 駅前から表示 → window = [0, 3]
		var target = new RedisplayTarget("k1", "駅B", "駅D", StationsBefore: 1);

		// Act & Assert
		Assert.Contains("k1", NotificationRedisplayEvaluator.EvaluateVisibleKeys(Stations, 0, [target]));
		Assert.Contains("k1", NotificationRedisplayEvaluator.EvaluateVisibleKeys(Stations, 1, [target]));
		Assert.Contains("k1", NotificationRedisplayEvaluator.EvaluateVisibleKeys(Stations, 3, [target]));
		Assert.DoesNotContain("k1", NotificationRedisplayEvaluator.EvaluateVisibleKeys(Stations, 4, [target]));
	}

	[Fact]
	public void EvaluateVisibleKeys_ReversedSectionOrder_YieldsSameWindow()
	{
		// Arrange: SectionStart/End が経路上の順序と逆 (駅D が先、駅B が後)
		var forward = new RedisplayTarget("k1", "駅B", "駅D", StationsBefore: 1);
		var reversed = new RedisplayTarget("k1", "駅D", "駅B", StationsBefore: 1);

		// Act
		var forwardVisible = new HashSet<int>();
		var reversedVisible = new HashSet<int>();
		for (int i = 0; i < Stations.Count; i++)
		{
			if (NotificationRedisplayEvaluator.EvaluateVisibleKeys(Stations, i, [forward]).Contains("k1"))
				forwardVisible.Add(i);
			if (NotificationRedisplayEvaluator.EvaluateVisibleKeys(Stations, i, [reversed]).Contains("k1"))
				reversedVisible.Add(i);
		}

		// Assert: 同じ window になる
		Assert.Equal(forwardVisible, reversedVisible);
	}

	[Fact]
	public void EvaluateVisibleKeys_StationsBeforeGreaterThanOne_ExtendsWindowStart()
	{
		// Arrange: 駅D (index 3) 指定、3 駅前から表示 → window = [0, 3]
		var target = new RedisplayTarget("k1", "駅D", null, StationsBefore: 3);

		// Act & Assert
		Assert.Contains("k1", NotificationRedisplayEvaluator.EvaluateVisibleKeys(Stations, 0, [target]));
		Assert.DoesNotContain("k1", NotificationRedisplayEvaluator.EvaluateVisibleKeys(Stations, -1, [target]));
	}

	[Fact]
	public void EvaluateVisibleKeys_StationsBeforeZeroOrNegative_ClampedToZero()
	{
		// Arrange: StationsBefore <= 0 は 0 にクランプされ、区間開始からのみ表示
		var zero = new RedisplayTarget("k1", "駅C", null, StationsBefore: 0);
		var negative = new RedisplayTarget("k2", "駅C", null, StationsBefore: -5);

		// Act
		var atStation = NotificationRedisplayEvaluator.EvaluateVisibleKeys(Stations, 2, [zero, negative]);
		var before = NotificationRedisplayEvaluator.EvaluateVisibleKeys(Stations, 1, [zero, negative]);

		// Assert
		Assert.Contains("k1", atStation);
		Assert.Contains("k2", atStation);
		Assert.DoesNotContain("k1", before);
		Assert.DoesNotContain("k2", before);
	}

	[Fact]
	public void EvaluateVisibleKeys_HiddenBeforeWindow_AtRealisticNonNegativeIndex()
	{
		// Arrange: 駅D (index 3) 指定、1 駅前から表示 → window = [2,3]。
		// low が 0 より大きい現実的なケースで「window より手前」の境界を検証する。
		var target = new RedisplayTarget("k1", "駅D", null, StationsBefore: 1);

		// Act
		var beforeWindow = NotificationRedisplayEvaluator.EvaluateVisibleKeys(Stations, 1, [target]);
		var atLow = NotificationRedisplayEvaluator.EvaluateVisibleKeys(Stations, 2, [target]);

		// Assert
		Assert.DoesNotContain("k1", beforeWindow);
		Assert.Contains("k1", atLow);
	}

	[Fact]
	public void EvaluateVisibleKeys_PartialSectionResolve_FallsBackToResolvedStationOnly()
	{
		// Arrange: SectionStart (駅C, index 2) は経路上にあるが、SectionEnd は経路外。
		// 解決できた駅だけを使った単一駅相当の window になるはず → [1,2]
		var target = new RedisplayTarget("k1", "駅C", "存在しない駅", StationsBefore: 1);

		// Act & Assert
		Assert.Contains("k1", NotificationRedisplayEvaluator.EvaluateVisibleKeys(Stations, 1, [target]));
		Assert.Contains("k1", NotificationRedisplayEvaluator.EvaluateVisibleKeys(Stations, 2, [target]));
		Assert.DoesNotContain("k1", NotificationRedisplayEvaluator.EvaluateVisibleKeys(Stations, 3, [target]));
	}

	[Fact]
	public void EvaluateVisibleKeys_CurrentIndexPastSection_NotVisible()
	{
		// Arrange: 区間を通過済み (currentStationIndex > high) は非表示
		var target = new RedisplayTarget("k1", "駅B", "駅D", StationsBefore: 1);

		// Act
		var result = NotificationRedisplayEvaluator.EvaluateVisibleKeys(Stations, 5, [target]);

		// Assert
		Assert.DoesNotContain("k1", result);
	}

	[Fact]
	public void EvaluateVisibleKeys_TokenNotOnRoute_NotVisible()
	{
		// Arrange: SectionStart がこの列車の経路上に存在しない駅名
		var target = new RedisplayTarget("k1", "存在しない駅", null, StationsBefore: 2);

		// Act
		var result = NotificationRedisplayEvaluator.EvaluateVisibleKeys(Stations, 2, [target]);

		// Assert
		Assert.DoesNotContain("k1", result);
	}

	[Fact]
	public void EvaluateVisibleKeys_BothTokensNotOnRoute_NotVisible()
	{
		// Arrange: 区間指定で、開始・終了ともに経路上に無い
		var target = new RedisplayTarget("k1", "存在しない駅1", "存在しない駅2", StationsBefore: 2);

		// Act
		var result = NotificationRedisplayEvaluator.EvaluateVisibleKeys(Stations, 2, [target]);

		// Assert
		Assert.DoesNotContain("k1", result);
	}

	[Fact]
	public void EvaluateVisibleKeys_MatchesById_WhenNameDiffers()
	{
		// Arrange: token は駅 ID。station の Name は別物だが Id が一致すればマッチする。
		var stations = new List<StationRef>
		{
			new("id-1", "名前A"),
			new("id-2", "名前B"),
		};
		var target = new RedisplayTarget("k1", "id-2", null, StationsBefore: 1);

		// Act
		var result = NotificationRedisplayEvaluator.EvaluateVisibleKeys(stations, 0, [target]);

		// Assert
		Assert.Contains("k1", result);
	}

	[Fact]
	public void EvaluateVisibleKeys_MatchesByName_WhenIdIsNull()
	{
		// Arrange: station の Id が null の場合でも Name 一致でマッチする。
		var stations = new List<StationRef>
		{
			new(null, "名前A"),
			new(null, "名前B"),
		};
		var target = new RedisplayTarget("k1", "名前B", null, StationsBefore: 1);

		// Act
		var result = NotificationRedisplayEvaluator.EvaluateVisibleKeys(stations, 0, [target]);

		// Assert
		Assert.Contains("k1", result);
	}

	[Fact]
	public void EvaluateVisibleKeys_MultipleTargets_ReturnsCorrectSubset()
	{
		// Arrange: 3 つの通告のうち、現在駅に応じて可視なものだけ返る
		var t1 = new RedisplayTarget("k1", "駅A", null, StationsBefore: 0); // index 0 のみ
		var t2 = new RedisplayTarget("k2", "駅C", "駅E", StationsBefore: 1); // window [1,4]
		var t3 = new RedisplayTarget("k3", "存在しない駅", null, StationsBefore: 2); // 常に非表示

		// Act
		var result = NotificationRedisplayEvaluator.EvaluateVisibleKeys(Stations, 1, [t1, t2, t3]);

		// Assert
		Assert.DoesNotContain("k1", result);
		Assert.Contains("k2", result);
		Assert.DoesNotContain("k3", result);
		Assert.Single(result); // k2 のみ可視
	}

	[Fact]
	public void EvaluateVisibleKeys_IndexSpaceIsRelativeToPassedStationList()
	{
		// Arrange: info-row などを除外した部分集合を渡した場合、インデックスは
		// その部分集合内での位置として解釈される (元の全駅リストのインデックスではない)。
		var filteredStations = new List<StationRef>
		{
			new("s0", "駅A"), // index 0 (元は index 0)
			new("s2", "駅C"), // index 1 (元は index 2 だったが、駅Bが除外され詰まっている)
			new("s4", "駅E"), // index 2 (元は index 4)
		};
		var target = new RedisplayTarget("k1", "駅C", null, StationsBefore: 1);

		// Act: フィルタ後のリストでの index 0 (= 駅C の 1 つ前) から表示されるはず
		var atIndex0 = NotificationRedisplayEvaluator.EvaluateVisibleKeys(filteredStations, 0, [target]);
		var atIndex1 = NotificationRedisplayEvaluator.EvaluateVisibleKeys(filteredStations, 1, [target]);
		var atIndex2 = NotificationRedisplayEvaluator.EvaluateVisibleKeys(filteredStations, 2, [target]);

		// Assert
		Assert.Contains("k1", atIndex0);
		Assert.Contains("k1", atIndex1);
		Assert.DoesNotContain("k1", atIndex2); // フィルタ後リストでは駅Cの次なので window 外
	}

	[Fact]
	public void EvaluateVisibleKeys_EmptyStations_NotVisible()
	{
		// Arrange
		var target = new RedisplayTarget("k1", "駅A", null, StationsBefore: 1);

		// Act
		var result = NotificationRedisplayEvaluator.EvaluateVisibleKeys([], 0, [target]);

		// Assert
		Assert.Empty(result);
	}

	[Fact]
	public void EvaluateVisibleKeys_EmptyTargets_ReturnsEmptySet()
	{
		// Act
		var result = NotificationRedisplayEvaluator.EvaluateVisibleKeys(Stations, 0, []);

		// Assert
		Assert.Empty(result);
	}

	[Fact]
	public void EvaluateVisibleKeys_WhitespaceOrEmptyTokens_NotVisible()
	{
		// Arrange: SectionStart が空/空白のみ
		var emptyStart = new RedisplayTarget("k1", "", null, StationsBefore: 1);
		var whitespaceStart = new RedisplayTarget("k2", "   ", null, StationsBefore: 1);
		var nullStart = new RedisplayTarget("k3", null, null, StationsBefore: 1);

		// Act
		var result = NotificationRedisplayEvaluator.EvaluateVisibleKeys(
			Stations, 0, [emptyStart, whitespaceStart, nullStart]);

		// Assert
		Assert.Empty(result);
	}

	[Fact]
	public void EvaluateVisibleKeys_DuplicateKeys_DoesNotThrow_LastWins()
	{
		// Arrange: 同一 Key を持つ 2 つの target。後方が可視、前方は非可視。
		var notVisible = new RedisplayTarget("k1", "存在しない駅", null, StationsBefore: 1);
		var visible = new RedisplayTarget("k1", "駅A", null, StationsBefore: 0);

		// Act
		var result = NotificationRedisplayEvaluator.EvaluateVisibleKeys(Stations, 0, [notVisible, visible]);

		// Assert: 最後に評価された (visible な) 方が採用される
		Assert.Contains("k1", result);
	}
}
