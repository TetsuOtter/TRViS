using TRViS.DTAC.Logic;

namespace TRViS.DTAC.Logic.Tests;

public class TimetableRebuildPolicyTests
{
	[Fact]
	public void CanUpdateInPlace_SameIdAndSameRowCount_True()
	{
		// 通常の WS リアルタイム編集ケース: 同じ列車の field 編集が来た。
		Assert.True(TimetableRebuildPolicy.CanUpdateInPlace(
			currentTrainId: "t-1", currentRowCount: 18,
			newTrainId: "t-1", newRowCount: 18));
	}

	[Fact]
	public void CanUpdateInPlace_DifferentTrainId_False()
	{
		// 列車切替: 全面再構築すべき。
		Assert.False(TimetableRebuildPolicy.CanUpdateInPlace(
			currentTrainId: "t-1", currentRowCount: 18,
			newTrainId: "t-2", newRowCount: 18));
	}

	[Fact]
	public void CanUpdateInPlace_SameIdDifferentRowCount_False()
	{
		// 同じ列車だが行が増減した: 構造変化なので全面再構築。
		Assert.False(TimetableRebuildPolicy.CanUpdateInPlace(
			currentTrainId: "t-1", currentRowCount: 18,
			newTrainId: "t-1", newRowCount: 19));
		Assert.False(TimetableRebuildPolicy.CanUpdateInPlace(
			currentTrainId: "t-1", currentRowCount: 18,
			newTrainId: "t-1", newRowCount: 17));
	}

	[Fact]
	public void CanUpdateInPlace_CurrentIdNull_False()
	{
		// 初回ロード: 既存 row 列が無いので in-place 不可。
		Assert.False(TimetableRebuildPolicy.CanUpdateInPlace(
			currentTrainId: null, currentRowCount: 0,
			newTrainId: "t-1", newRowCount: 18));
	}

	[Fact]
	public void CanUpdateInPlace_NewIdNull_False()
	{
		// 列車選択解除 (TrainData=null) ケース: 全面クリア相当なので in-place しない。
		Assert.False(TimetableRebuildPolicy.CanUpdateInPlace(
			currentTrainId: "t-1", currentRowCount: 18,
			newTrainId: null, newRowCount: 0));
	}

	[Fact]
	public void CanUpdateInPlace_BothIdsNull_False()
	{
		Assert.False(TimetableRebuildPolicy.CanUpdateInPlace(
			currentTrainId: null, currentRowCount: 0,
			newTrainId: null, newRowCount: 0));
	}

	[Fact]
	public void CanUpdateInPlace_SameIdZeroRows_True()
	{
		// 同じ Id で両方 0 行というレアケースでも安定して in-place 扱い (= no-op).
		Assert.True(TimetableRebuildPolicy.CanUpdateInPlace(
			currentTrainId: "t-1", currentRowCount: 0,
			newTrainId: "t-1", newRowCount: 0));
	}
}
