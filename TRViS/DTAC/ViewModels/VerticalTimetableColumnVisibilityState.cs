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

	/// <summary>
	/// 運転時分・着線/発線が (狭幅表示ではないが) 768pt 未満の詰まった列幅
	/// (54px) で表示されているか。この帯ではフォントを少し縮める (issue #320)。
	/// 着線/発線は狭幅表示中 (<see cref="IsTrackNameNarrow"/>) は対象外
	/// (狭幅フォントが優先される)。
	/// </summary>
	[ObservableProperty]
	public partial bool IsRunTimeMid { get; set; } = false;
	[ObservableProperty]
	public partial bool IsTrackNameMid { get; set; } = false;

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

		// iPad mini 6/A17 Pro の実測ポートレート幅は 744pt ちょうど (issue #320 の
		// screenshot 回帰テストで実機シミュレータの SizeChanged 値として確認済み)。
		// 旧 "744 + 12 = 756" だと実測値がこの分岐に一切乗らず、iPad mini 6 のフル画面
		// ポートレートで列車情報ヘッダ/運転時分が常に非表示になっていた。
		IPAD_MINI_6_V = 744,
		IPAD_MINI_2_3_4_5_V = 768,
	}

	public static ViewWidthMode ClassifyWidth(double width) => width switch
	{
		>= 768 => ViewWidthMode.IPAD_MINI_2_3_4_5_V,
		>= 744 => ViewWidthMode.IPAD_MINI_6_V,
		>= 736 => ViewWidthMode.IPHONE_6_7_8_PLUS_H,
		>= 667 => ViewWidthMode.IPHONE_6_7_8_H,
		>= 568 => ViewWidthMode.IPHONE_SE_H,
		>= 414 => ViewWidthMode.IPHONE_6_7_8_PLUS_V,
		>= 375 => ViewWidthMode.IPHONE_6_7_8_V,
		>= 320 => ViewWidthMode.IPHONE_SE_V,
		_ => ViewWidthMode.NARROW,
	};

	// --- 単一の真実源となる static 述語 (幅ロジックと表示ロジックの両方が参照する) ---
	// 全ての列を実ビュー幅 (ClassifyWidth の戻り値) のみで判定する。端末本来の
	// フル幅へのフォールバック等は行わない — Split View 等で実際に縮んだ幅は、
	// その幅なりに正直に評価する (issue #320)。
	//
	// 制限速度 (IsRunInOutLimitVisible) の閾値だけは他の全列より高く保つ。これにより
	// iPad の 3:7 分割 (2/3 側 ≈ 0.7 * 1024 ≒ 717pt 以下) のように「他列は表示できる
	// 余裕はあるが制限速度だけ収まらない」幅で、制限速度だけが単独で非表示になる帯
	// ([IPHONE_SE_H, IPHONE_6_7_8_PLUS_H) = [568, 736)pt) を確保しつつ、iPad mini 6
	// 実機フルスクリーン (744pt, E2E 実測済み) では全列を表示できる。

	/// <summary>列車情報ヘッダ (列車番号/最高速度/速度種別/けん引定数) を表示するか。</summary>
	public static bool IsTrainInfoHeaderVisible(ViewWidthMode m) => m >= ViewWidthMode.IPHONE_SE_H;
	/// <summary>運転時分列を表示するか (幅が狭い帯 (4px) では文字も表示しない)。</summary>
	public static bool IsRunTimeVisible(ViewWidthMode m) => m >= ViewWidthMode.IPHONE_6_7_8_H;
	/// <summary>停車場名を狭幅表示にするか。</summary>
	public static bool IsStationNameNarrowMode(ViewWidthMode m) => m <= ViewWidthMode.IPHONE_6_7_8_PLUS_V;
	/// <summary>着線/発線を狭幅表示にするか。</summary>
	public static bool IsTrackNameNarrowMode(ViewWidthMode m) => m <= ViewWidthMode.IPHONE_6_7_8_PLUS_V;
	/// <summary>制限速度列を表示するか。</summary>
	public static bool IsRunInOutLimitVisible(ViewWidthMode m) => m >= ViewWidthMode.IPHONE_6_7_8_PLUS_H;
	/// <summary>記事列を表示するか。</summary>
	public static bool IsRemarksVisible(ViewWidthMode m) => m >= ViewWidthMode.IPHONE_SE_H;
	/// <summary>マーカー列を表示するか。</summary>
	public static bool IsMarkerVisible(ViewWidthMode m) => m >= ViewWidthMode.IPHONE_SE_H;

	public void UpdateState(int width)
	{
		ViewWidthMode mode = ClassifyWidth(width);
		CurrentMode = mode;

		bool trainInfoHeaderVisible = IsTrainInfoHeaderVisible(mode);
		TrainNumber = trainInfoHeaderVisible;
		MaxSpeed = trainInfoHeaderVisible;
		SpeedType = trainInfoHeaderVisible;
		NominalTractiveCapacity = trainInfoHeaderVisible;

		RunTime = IsRunTimeVisible(mode);
		// 停車場名・着線/発線・着発時刻は常に表示し、狭い画面では幅/フォントを詰める
		StationName = true;
		ArrivalTime = true;
		DepartureTime = true;
		TrackName = true;
		IsStationNameNarrow = IsStationNameNarrowMode(mode);
		IsTrackNameNarrow = IsTrackNameNarrowMode(mode);

		IsRunTimeMid = RunTime && mode < ViewWidthMode.IPAD_MINI_2_3_4_5_V;
		IsTrackNameMid = !IsTrackNameNarrow && mode < ViewWidthMode.IPAD_MINI_2_3_4_5_V;

		RunInOutLimit = IsRunInOutLimitVisible(mode);
		Remarks = IsRemarksVisible(mode);
		Marker = IsMarkerVisible(mode);
	}
}
