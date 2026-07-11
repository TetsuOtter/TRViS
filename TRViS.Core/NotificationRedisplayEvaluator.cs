namespace TRViS.Core;

/// <summary>
/// 現在の列車の駅順リスト上の 1 駅を表す。インデックスは呼び出し側が渡す
/// <c>stations</c> リストの並びに対応しており、絞り込み前の全駅リストとは
/// インデックス空間が異なりうる (呼び出し側が info-row 等を除外した部分集合を
/// 渡すことを想定している)。
/// </summary>
public sealed record StationRef(string? Id, string? Name);

/// <summary>
/// 通告の再表示対象を表す。<see cref="SectionStart"/> / <see cref="SectionEnd"/> は
/// 駅名または駅 ID の文字列 (どちらでも一致判定できる)。<see cref="SectionEnd"/> が
/// null/空の場合は単一駅指定として扱う。<see cref="Key"/> は呼び出し側が自身の
/// 通告と対応付けるための不透明な識別子であり、このクラスは内容を解釈しない。
/// </summary>
public sealed record RedisplayTarget(string Key, string? SectionStart, string? SectionEnd, int StationsBefore);

/// <summary>
/// 現在の列車の駅順リストと現在駅の位置から、いま小さいバナーとして再表示すべき
/// 通告を判定するステートレスな評価器。
/// </summary>
public static class NotificationRedisplayEvaluator
{
	/// <summary>
	/// 各 <paramref name="targets"/> について、現在駅 (<paramref name="currentStationIndex"/>)
	/// が「区間の (開始側から <see cref="RedisplayTarget.StationsBefore"/> 駅前) 〜 (終了側)」の
	/// 範囲内にあるかを判定し、該当する <see cref="RedisplayTarget.Key"/> の集合を返す。
	///
	/// <para>
	/// SectionStart / SectionEnd はそれぞれ独立に <paramref name="stations"/> 内の駅と
	/// 順序無視で (Id または Name の一致で) 解決する。どちらも解決できなければ
	/// その対象は現在の列車の経路上に無いとみなし、非表示とする。
	/// </para>
	///
	/// <para>
	/// 非表示になるタイミングは区間終了駅 (単駅指定の場合は区間開始駅) の発車後であり、
	/// その次の駅への到着時ではない。そのため終了側の駅では <paramref name="isRunningToNextStation"/>
	/// (現在駅を発車し次駅へ向かって走行中かどうか) も参照し、発車済みなら非表示とする。
	/// </para>
	///
	/// <para>
	/// 同一 Key が複数の <paramref name="targets"/> に現れた場合は、最後に評価された
	/// 結果を採用する (last-wins)。
	/// </para>
	/// </summary>
	public static IReadOnlySet<string> EvaluateVisibleKeys(
		IReadOnlyList<StationRef> stations,
		int currentStationIndex,
		bool isRunningToNextStation,
		IEnumerable<RedisplayTarget> targets)
	{
		Dictionary<string, bool> visibilityByKey = [];

		foreach (var target in targets)
		{
			List<int> resolvedIndices = [];

			int? startIndex = FindStationIndex(stations, target.SectionStart);
			if (startIndex is not null)
				resolvedIndices.Add(startIndex.Value);

			if (!string.IsNullOrWhiteSpace(target.SectionEnd))
			{
				int? endIndex = FindStationIndex(stations, target.SectionEnd);
				if (endIndex is not null)
					resolvedIndices.Add(endIndex.Value);
			}

			if (resolvedIndices.Count == 0)
			{
				// 経路上のどちらの駅も解決できない = この列車には無関係な通告
				visibilityByKey[target.Key] = false;
				continue;
			}

			int stationsBefore = Math.Max(0, target.StationsBefore);
			int low = resolvedIndices.Min() - stationsBefore;
			int high = resolvedIndices.Max();

			// 終了駅を発車済み (currentStationIndex == high かつ発車後、または既に通過済み) なら非表示。
			bool departedEndStation = currentStationIndex > high
				|| (currentStationIndex == high && isRunningToNextStation);

			visibilityByKey[target.Key] = currentStationIndex >= low && !departedEndStation;
		}

		HashSet<string> visibleKeys = [];
		foreach (var (key, isVisible) in visibilityByKey)
		{
			if (isVisible)
				visibleKeys.Add(key);
		}
		return visibleKeys;
	}

	/// <summary>
	/// <paramref name="token"/> (駅名または駅 ID) に一致する最初の駅のインデックスを返す。
	/// 一致判定は序数比較 (<see cref="StringComparison.Ordinal"/>) で、Id と Name の
	/// どちらか一方でも一致すればマッチとする。
	/// </summary>
	private static int? FindStationIndex(IReadOnlyList<StationRef> stations, string? token)
	{
		if (string.IsNullOrWhiteSpace(token))
			return null;

		for (int i = 0; i < stations.Count; i++)
		{
			var station = stations[i];
			if (station.Id is not null && string.Equals(station.Id, token, StringComparison.Ordinal))
				return i;
			if (station.Name is not null && string.Equals(station.Name, token, StringComparison.Ordinal))
				return i;
		}

		return null;
	}
}
