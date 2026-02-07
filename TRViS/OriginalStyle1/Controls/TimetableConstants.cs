using Microsoft.Maui.Controls;

namespace TRViS.OriginalStyle1.Controls;

/// <summary>
/// 時刻表の共通定数とレイアウト設定
/// </summary>
public static class TimetableConstants
{
	/// <summary>
	/// 時刻表行の高さ
	/// </summary>
	public const double ROW_HEIGHT = 60;

	/// <summary>
	/// ヘッダー行の高さ
	/// </summary>
	public const double HEADER_HEIGHT = 40;

	/// <summary>
	/// 列間のスペーシング
	/// </summary>
	public const double COLUMN_SPACING = 4;

	/// <summary>
	/// 時刻表の列定義
	/// </summary>
	public static ColumnDefinitionCollection CreateColumnDefinitions()
	{
		return
		[
			new ColumnDefinition { Width = GridLength.Star },                              // 駅名（可変幅）
			new ColumnDefinition { Width = new GridLength(120, GridUnitType.Absolute) },  // 着時刻
			new ColumnDefinition { Width = new GridLength(120, GridUnitType.Absolute) },  // 発時刻
			new ColumnDefinition { Width = new GridLength(60, GridUnitType.Absolute) },   // 番線
			new ColumnDefinition { Width = new GridLength(40, GridUnitType.Absolute) },   // 制限（上段：RunInLimit、下段：RunOutLimit）
			new ColumnDefinition { Width = new GridLength(100, GridUnitType.Absolute) },  // 記事
		];
	}

	/// <summary>
	/// 列ヘッダーのテキスト
	/// </summary>
	public static string[] ColumnHeaders => new[] { "駅名", "着時刻", "発時刻", "番線", "制限", "記事" };

	/// <summary>
	/// 列インデックスの定義
	/// </summary>
	public static class ColumnIndex
	{
		public const int StationName = 0;
		public const int ArrivalTime = 1;
		public const int DepartureTime = 2;
		public const int TrackName = 3;
		public const int Limit = 4;
		public const int Remarks = 5;
		public const int Count = 6;
	}
}
