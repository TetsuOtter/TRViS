using CommunityToolkit.Mvvm.ComponentModel;

namespace TRViS.DTAC.ViewModels;

/// <summary>
/// 縦型時刻表の列を画面幅に応じて出し分け・幅調整するための単一の真実源 (issue #41)。
///
/// <para>
/// 旧 feature/support-smartphone ブランチの <c>DTACColumnDefinitionsProvider</c> が持っていた
/// 段階的ブレークポイント (<see cref="ViewWidthMode"/>) と各列の表示/狭幅判定ロジックを、
/// 現 main の MVVM 構成 (本 ObservableObject を <see cref="VerticalTimetableRow"/> が
/// PropertyChanged 購読する) に移植したもの。
/// </para>
/// <para>
/// 列の <b>表示/非表示</b> (本クラスの bool プロパティ) と、列の <b>幅</b>
/// (<see cref="DTACElementStyles.SetTimetableColumnWidthCollection"/>) が食い違わないよう、
/// 判定は必ず本クラスの static 述語 (<see cref="IsRunTimeVisible"/> 等) を経由する。
/// </para>
/// </summary>
public partial class VerticalTimetableColumnVisibilityState : ObservableObject
{
	[ObservableProperty]
	public partial bool TrainNumber { get; set; } = true;
	[ObservableProperty]
	public partial bool MaxSpeed { get; set; } = true;
	[ObservableProperty]
	public partial bool SpeedType { get; set; } = true;
	[ObservableProperty]
	public partial bool NominalTractiveCapacity { get; set; } = true;

	[ObservableProperty]
	public partial bool RunTime { get; set; } = true;
	[ObservableProperty]
	public partial bool StationName { get; set; } = true;
	[ObservableProperty]
	public partial bool ArrivalTime { get; set; } = true;
	[ObservableProperty]
	public partial bool DepartureTime { get; set; } = true;
	[ObservableProperty]
	public partial bool TrackName { get; set; } = true;
	[ObservableProperty]
	public partial bool RunInOutLimit { get; set; } = true;
	[ObservableProperty]
	public partial bool Remarks { get; set; } = true;
	[ObservableProperty]
	public partial bool Marker { get; set; } = true;

	/// <summary>
	/// 停車場名・着線/発線を狭幅表示にすべきか (文字数を詰める・フォントを縮める)。
	/// 列自体は常に表示するが、幅の狭い画面では詰めて表示する。
	/// </summary>
	[ObservableProperty]
	public partial bool IsStationNameNarrow { get; set; } = false;
	[ObservableProperty]
	public partial bool IsTrackNameNarrow { get; set; } = false;

	public ViewWidthMode CurrentMode { get; private set; } = ViewWidthMode.IPAD_MINI_2_3_4_5_V;

	public VerticalTimetableColumnVisibilityState(int width)
	{
		UpdateState(width);
	}

	/// <summary>
	/// 画面幅 (DIP) のブレークポイント。値はそのモードの下限幅 (px) であり、
	/// 列挙順 = 幅の昇順 になっているので <c>mode &lt;= ...</c> / <c>mode &gt;= ...</c> で
	/// 「これより狭い/広い」を素直に判定できる。
	/// </summary>
	public enum ViewWidthMode
	{
		NARROW = 0,

		IPHONE_SE_V = 320,
		IPHONE_6_7_8_V = 375,
		IPHONE_6_7_8_PLUS_V = 414,

		IPHONE_SE_H = 568,
		IPHONE_6_7_8_H = 667,
		IPHONE_6_7_8_PLUS_H = 736,

		// iPad mini 6 以降は左右の余白を含めると 12px 狭い (main commit b244d19)
		IPAD_MINI_6_V = 744 + 12,
		IPAD_MINI_2_3_4_5_V = 768,
	}

	public static ViewWidthMode ClassifyWidth(double width) => width switch
	{
		>= 768 => ViewWidthMode.IPAD_MINI_2_3_4_5_V,
		>= 756 => ViewWidthMode.IPAD_MINI_6_V,
		>= 736 => ViewWidthMode.IPHONE_6_7_8_PLUS_H,
		>= 667 => ViewWidthMode.IPHONE_6_7_8_H,
		>= 568 => ViewWidthMode.IPHONE_SE_H,
		>= 414 => ViewWidthMode.IPHONE_6_7_8_PLUS_V,
		>= 375 => ViewWidthMode.IPHONE_6_7_8_V,
		>= 320 => ViewWidthMode.IPHONE_SE_V,
		_ => ViewWidthMode.NARROW,
	};

	// --- 単一の真実源となる static 述語 (幅ロジックと表示ロジックの両方が参照する) ---

	/// <summary>列車情報ヘッダ (列車番号/最高速度/速度種別/けん引定数) を表示するか。</summary>
	public static bool IsTrainInfoHeaderVisible(ViewWidthMode m) => m >= ViewWidthMode.IPAD_MINI_6_V;
	/// <summary>運転時分列を表示するか。</summary>
	public static bool IsRunTimeVisible(ViewWidthMode m) => m >= ViewWidthMode.IPAD_MINI_6_V;
	/// <summary>停車場名を狭幅表示にするか。</summary>
	public static bool IsStationNameNarrowMode(ViewWidthMode m) => m <= ViewWidthMode.IPHONE_6_7_8_PLUS_V;
	/// <summary>着線/発線を狭幅表示にするか。</summary>
	public static bool IsTrackNameNarrowMode(ViewWidthMode m) => m <= ViewWidthMode.IPHONE_6_7_8_PLUS_V;
	/// <summary>制限速度列を表示するか。</summary>
	public static bool IsRunInOutLimitVisible(ViewWidthMode m) => m >= ViewWidthMode.IPHONE_6_7_8_PLUS_H;
	/// <summary>記事列を表示するか。</summary>
	public static bool IsRemarksVisible(ViewWidthMode m) => m >= ViewWidthMode.IPHONE_6_7_8_H;
	/// <summary>マーカー列を表示するか。</summary>
	public static bool IsMarkerVisible(ViewWidthMode m) => m >= ViewWidthMode.IPHONE_6_7_8_H;

	public void UpdateState(int width)
	{
		ViewWidthMode m = ClassifyWidth(width);
		CurrentMode = m;

		bool trainInfoHeaderVisible = IsTrainInfoHeaderVisible(m);
		TrainNumber = trainInfoHeaderVisible;
		MaxSpeed = trainInfoHeaderVisible;
		SpeedType = trainInfoHeaderVisible;
		NominalTractiveCapacity = trainInfoHeaderVisible;

		RunTime = IsRunTimeVisible(m);
		// 停車場名・着線/発線・着発時刻は常に表示し、狭い画面では幅/フォントを詰める
		StationName = true;
		ArrivalTime = true;
		DepartureTime = true;
		TrackName = true;
		IsStationNameNarrow = IsStationNameNarrowMode(m);
		IsTrackNameNarrow = IsTrackNameNarrowMode(m);

		RunInOutLimit = IsRunInOutLimitVisible(m);
		Remarks = IsRemarksVisible(m);
		Marker = IsMarkerVisible(m);
	}
}
