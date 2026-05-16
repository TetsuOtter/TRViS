using TRViS.DTAC.Logic;

namespace TRViS.DTAC.Logic.Tests;

public class TimetableRebuildPolicyTests
{
	[Fact]
	public void IsSameTrainEdit_SameId_True()
	{
		// 通常の WS リアルタイム編集ケース: 同じ列車の field 編集が来た。
		Assert.True(TimetableRebuildPolicy.IsSameTrainEdit(
			currentTrainId: "t-1",
			newTrainId: "t-1"));
	}

	[Fact]
	public void IsSameTrainEdit_DifferentTrainId_False()
	{
		// 列車切替: 全面再構築すべき。
		Assert.False(TimetableRebuildPolicy.IsSameTrainEdit(
			currentTrainId: "t-1",
			newTrainId: "t-2"));
	}

	[Fact]
	public void IsSameTrainEdit_CurrentIdNull_False()
	{
		// 初回ロード: 既存 row 列が無いので mutate 不可、全面構築扱い。
		Assert.False(TimetableRebuildPolicy.IsSameTrainEdit(
			currentTrainId: null,
			newTrainId: "t-1"));
	}

	[Fact]
	public void IsSameTrainEdit_NewIdNull_False()
	{
		// 列車選択解除 (TrainData=null) ケース: 全面クリア相当。
		Assert.False(TimetableRebuildPolicy.IsSameTrainEdit(
			currentTrainId: "t-1",
			newTrainId: null));
	}

	[Fact]
	public void IsSameTrainEdit_BothIdsNull_False()
	{
		Assert.False(TimetableRebuildPolicy.IsSameTrainEdit(
			currentTrainId: null,
			newTrainId: null));
	}

	[Fact]
	public void IsSameTrainEdit_SameId_RowCountIsNotConsidered()
	{
		// 同じ列車に対する更新であれば、行数が違っていても (= 駅追加/削除でも)
		// soft path 対象とする。呼び出し側は overlap 部分を field 更新し、
		// 余りを Add / 不足を Remove することで mutate ベースで対応する。
		Assert.True(TimetableRebuildPolicy.IsSameTrainEdit("t-1", "t-1"));
	}
}
