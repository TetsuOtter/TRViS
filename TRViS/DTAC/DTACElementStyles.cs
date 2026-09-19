using System.Text.RegularExpressions;

using Microsoft.Maui.Controls.Shapes;

using TRViS.Controls;
using TRViS.DTAC.ViewModels;
using TRViS.Services;
using TRViS.Utils;

namespace TRViS.DTAC;

public static partial class DTACElementStyles
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	static Color genColor(byte value)
		=> new(value, value, value);
	static AppThemeColorBindingExtension genColor(byte defaultColorValue, byte darkColorValue)
		=> new(genColor(defaultColorValue), genColor(darkColorValue));

	const byte baseDarkColor = 0x25;

	public static readonly AppThemeColorBindingExtension DefaultTextColor = genColor(0x33, 0xFF);
	public static readonly AppThemeColorBindingExtension HeaderTextColor = genColor(0x55, 0xFF);
	public static readonly AppThemeColorBindingExtension TimetableTextColor = genColor(0x00, 0xDD);
	public static readonly AppThemeColorBindingExtension TimetableTextInvColor = genColor(0xFF, 0xFF);
	public static readonly AppThemeColorBindingExtension TrainNumNextDayTextColor = new(
		new(0x33, 0x33, 0xDD),
		new(0x44, 0x99, 0xFF)
	);
	public static readonly AppThemeColorBindingExtension HorizontalTimetableButtonTextColor = genColor(0x33, 0xDD);
	public static readonly AppThemeColorBindingExtension HeaderBackgroundColor = genColor(0xDD, baseDarkColor + 0x18);
	public static readonly AppThemeColorBindingExtension SeparatorLineColor = genColor(0xAA, baseDarkColor + 0x33);
	public static readonly AppThemeColorBindingExtension DefaultBGColor = genColor(0xFF, baseDarkColor);
	public static readonly AppThemeColorBindingExtension CarCountBGColor = genColor(0xFE, baseDarkColor + 0x11);
	public static readonly AppThemeColorBindingExtension TabAreaBGColor = genColor(0xEE, baseDarkColor - 0x20);
	public static readonly AppThemeColorBindingExtension TabButtonBGColor = genColor(0xCC, baseDarkColor - 0x11);

	public static readonly AppThemeColorBindingExtension OpenCloseButtonBGColor = genColor(0xFD, 0x4A);
	public static readonly AppThemeColorBindingExtension OpenCloseButtonTextColor = genColor(0xAA, 0x99);
	public static readonly AppThemeColorBindingExtension MarkerButtonIconColor = new(
		new(0x00, 0x44, 0x00),
		new(0x00, 0x99, 0x00)
	);
	public static readonly AppThemeColorBindingExtension MarkerMarkButtonBGColor = genColor(0xFA, 0x4A);
	public static readonly AppThemeGenericsBindingExtension<Brush> MarkerMarkButtonBGColorBrush
		= MarkerMarkButtonBGColor.ToBrushTheme();

	public static readonly AppThemeColorBindingExtension DefaultGreen = new(
		new(0x00, 0x80, 0x00),
		new(0x00, 0x80, 0x00)
	);
	public static readonly AppThemeColorBindingExtension SemiDarkGreen = new(
		new(0x00, 0x77, 0x00),
		new(0x00, 0x77, 0x00)
	);
	public static readonly AppThemeColorBindingExtension DarkGreen = new(
		new(0x31, 0x65, 0x23),
		new(0x00, 0x33, 0x00)
	);

	public static readonly AppThemeColorBindingExtension ForegroundBlackWhite = genColor(0x00, 0xFF);
	public static readonly AppThemeGenericsBindingExtension<Brush> ForegroundBlackWhiteBrush = ForegroundBlackWhite.ToBrushTheme();

	public static readonly AppThemeColorBindingExtension LocationServiceSelectedSideBorderColor = genColor(0xFF, 0xAA);
	public static readonly AppThemeColorBindingExtension LocationServiceSelectedSideDisabledBorderColor = genColor(0xDD, 0x99);
	public static readonly AppThemeColorBindingExtension LocationServiceSelectedSideTextColor = genColor(0xFF, 0xDD);
	public static readonly AppThemeColorBindingExtension LocationServiceNotSelectedSideTextColor = genColor(0x00, 0x00);
	public static readonly AppThemeColorBindingExtension LocationServiceNotSelectedSideBaseColor = genColor(0xFF, 0xDD);
	public static readonly AppThemeColorBindingExtension LocationServiceNotSelectedSideDisabledBaseColor = genColor(0xDD, 0x99);

	public static readonly AppThemeColorBindingExtension StartEndRunButtonTextColor = genColor(0xFF, 0xE0);

	public const double DefaultTextSize = 16;
	public const double DefaultTextSizePlus = 17;
	public const double LargeTextSize = 24;
	public const double AffectDateFontSize = 20;
	// Only the Hako tab's headers (乗務開始/乗務終了, diagram station names) use this — kept
	// separate from HeaderLabelStyle's own FontSize so the Timetable tab's header label (also
	// built via HeaderLabelStyle, in BeforeDeparture_AfterArrive) isn't affected.
	public const double HakoHeaderFontSize = 20;
	public const double BEFORE_REMARKS_FONT_SIZE = 17;
	// 縦の短い画面 (ViewHeightMode.Low) では BeforeRemarks の2行表示が行の枠に
	// 収まらないため、DestinationLabel (LabelStyleResource / DefaultTextSize) と
	// 同じフォントサイズまで縮める。
	public const double BEFORE_REMARKS_FONT_SIZE_LOW = DefaultTextSize;
	public const double BEFORE_REMARKS_LINE_HEIGHT_LOW = 1.0;
	public const double BEFORE_REMARKS_BOTTOM_MARGIN = 8;
	public const double BEFORE_REMARKS_BOTTOM_MARGIN_LOW = 0;
	public const double AFTER_REMARKS_FONT_SIZE = 20;
	public static readonly double TimetableFontSize = DeviceInfo.Current.Platform == DevicePlatform.iOS ? 32 : 30;
	// 狭い画面で停車場名・着線/発線を詰めて表示するときのフォントサイズ (issue #41)
	public static readonly double TimetableFontSizeNarrow = TimetableFontSize - 4;
	// iPad mini 6 等、着線/発線列が 54px に詰まる帯 (768pt 未満・非狭幅表示) 用の
	// 中間フォントサイズ (issue #320)。狭幅表示 (TimetableFontSizeNarrow) ほどは
	// 縮めない。
	public static readonly double TimetableFontSizeMid = TimetableFontSize - 2;
	public static readonly double TimetableRunLimitFontSize = DeviceInfo.Current.Platform == DevicePlatform.iOS ? 26 : 24;
	public static readonly double DriveTimeMMFontSize = DeviceInfo.Current.Platform == DevicePlatform.iOS ? 28 : 26;
	// 運転時分列が 54px に詰まる帯 (768pt 未満) 用の中間フォントサイズ (issue #320)。
	public static readonly double DriveTimeMMFontSizeMid = DriveTimeMMFontSize - 2;
	public static readonly double DriveTimeSSFontSize = DeviceInfo.Current.Platform == DevicePlatform.iOS ? 18 : 16;
	public static readonly double DriveTimeSSFontSizeMid = DriveTimeSSFontSize - 2;

	public const int TRAIN_INFO_HEIGHT = 50;
	public const int BEFORE_DEPARTURE_HEIGHT = 45;

	public const int TimetableRowMarkerBackgroundZIndex = -1;
	public const int TimetableRowLocationBoxZIndex = 2;
	public const int TimetableRowMarkerBoxZIndex = 3;
	public const int TimetableRowRunTimeTextZIndex = 10;

	public const double BEFORE_REMARKS_LEFT_MARGIN = 20;

	public const string DefaultFontFamily = "Hiragino Sans";
	public const string MaterialIconFontFamily = "MaterialIconsRegular";
	public const string TimetableNumFontFamily = "Helvetica";
	// iOS only exposes Hiragino Sans as W3 (Regular)/W6 (Bold) — already fully used by
	// FontAttributes.Bold elsewhere — so getting noticeably bolder than that (for
	// 行路施行日/行路名) means switching to a different, heavier bundled font instead of a
	// heavier Hiragino Sans weight.
	public const string HakoBoldFontFamily = "NotoSansJPBold";

	// Material Icons
	public const string BackArrowIcon = "\ue5c4";
	public const string MenuIcon = "\ue241";

	public const string AffectDateLabelTextPrefix = "行路施行日\n";

	public static readonly Shadow DefaultShadow = new()
	{
		Brush = Colors.Black,
		Offset = new(3, 3),
		Radius = 3,
		Opacity = 0.2f
	};

	public const double RUN_TIME_COLUMN_WIDTH = 60;
	private const double RUN_TIME_COLUMN_WIDTH_MID = 54;
	private const double RUN_TIME_COLUMN_WIDTH_NARROW = 4;
	private const double STA_NAME_COLUMN_WIDTH = 140;
	private const double STA_NAME_COLUMN_WIDTH_NARROW = 96;
	private const double ARR_DEP_COLUMN_WIDTH = 140;
	private const double ARR_DEP_COLUMN_WIDTH_NARROW = 110;
	private const double ARR_DEP_COLUMN_WIDTH_MINI6 = 134;
	private const double TRACK_NUMBER_COLUMN_WIDTH = 60;
	private const double TRACK_NUMBER_COLUMN_WIDTH_MID = 54;
	private const double TRACK_NUMBER_COLUMN_WIDTH_NARROW = 48;
	private const double SPEED_LIMIT_COLUMN_WIDTH = 60;
	private const double MARKER_COLUMN_WIDTH = 64;

	/// <summary>
	/// 縦型時刻表の 8 列 (運転時分/停車場名/着/発/着線発線/制限速度/記事/マーカー) の
	/// 列定義を <paramref name="grid"/> に設定し、画面幅に応じて段階的に幅を変える
	/// SizeChanged ハンドラを取り付ける (issue #41)。
	///
	/// <para>
	/// 旧 main は &lt;768 で着発時刻列を 134px にするだけの単一ブレークポイントだったため、
	/// 狭い画面で非表示相当の列が幅を占有し続け内容が見切れていた。
	/// 旧 feature/support-smartphone ブランチの <c>DTACColumnDefinitionsProvider</c> の
	/// 段階的ロジック (非表示列は 0 幅へ畳む / 停車場名・着線発線は狭幅へ /
	/// 最後に残る可変列を Star にして余白を埋める) を移植する。
	/// </para>
	/// <para>
	/// 幅判定は必ず <see cref="VerticalTimetableColumnVisibilityState"/> の
	/// 分類器・述語を経由するため、列の表示/非表示 (内容側) と列幅が食い違わない。
	/// この grid 自身の幅で判定するので、同じ列構成を持つ複数 grid
	/// (ヘッダ・出発前・記事前・行本体) は同一幅で一貫した結果になる。
	/// </para>
	/// </summary>
	public static void SetTimetableColumnWidthCollection(Grid grid)
	{
		ColumnDefinition runTimeColumn = new(new(RUN_TIME_COLUMN_WIDTH));
		ColumnDefinition stationNameColumn = new(new(STA_NAME_COLUMN_WIDTH));
		ColumnDefinition arrivalDepartureTimeColumn = new(new(ARR_DEP_COLUMN_WIDTH));
		ColumnDefinition trackNumberColumn = new(new(TRACK_NUMBER_COLUMN_WIDTH));
		ColumnDefinition speedLimitColumn = new(new(SPEED_LIMIT_COLUMN_WIDTH));
		ColumnDefinition remarksColumn = new(new(1, GridUnitType.Star));
		ColumnDefinition markerColumn = new(new(MARKER_COLUMN_WIDTH));
		grid.ColumnDefinitions = [
			runTimeColumn,
			stationNameColumn,
			arrivalDepartureTimeColumn,
			arrivalDepartureTimeColumn,
			trackNumberColumn,
			speedLimitColumn,
			remarksColumn,
			markerColumn
		];

		VerticalTimetableColumnVisibilityState.ViewWidthMode? lastMode = null;
		grid.SizeChanged += (s, e) =>
		{
			if (grid.Width <= 0)
				return;

			// 全ての列を実ビュー幅のみで判定する (端末フル幅へのフォールバックなし)。
			// issue #320: Split View 等で実際に縮んだ幅は、その幅なりに正直に評価する。
			VerticalTimetableColumnVisibilityState.ViewWidthMode mode
				= VerticalTimetableColumnVisibilityState.ClassifyWidth(grid.Width);
			if (lastMode == mode)
				return;
			lastMode = mode;
			logger.Debug("TimetableColumnWidthCollection mode -> {0} (width={1})", mode, grid.Width);

			bool isRunTimeVisible = VerticalTimetableColumnVisibilityState.IsRunTimeVisible(mode);
			bool isStaNameNarrow = VerticalTimetableColumnVisibilityState.IsStationNameNarrowMode(mode);
			bool isSpeedLimitVisible = VerticalTimetableColumnVisibilityState.IsRunInOutLimitVisible(mode);
			bool isRemarksVisible = VerticalTimetableColumnVisibilityState.IsRemarksVisible(mode);
			bool isMarkerVisible = VerticalTimetableColumnVisibilityState.IsMarkerVisible(mode);
			bool isFullWidth = mode >= VerticalTimetableColumnVisibilityState.ViewWidthMode.IPAD_MINI_2_3_4_5_V;

			// 運転時分: 非表示時も 0 ではなく細い帯を残す (列車情報出発前ヘッダと幅を揃えるため)。
			// 768pt 未満では表示時も少し詰めて (54px) 幅に余裕を持たせる。
			runTimeColumn.Width = !isRunTimeVisible
				? RUN_TIME_COLUMN_WIDTH_NARROW
				: isFullWidth ? RUN_TIME_COLUMN_WIDTH : RUN_TIME_COLUMN_WIDTH_MID;

			// 停車場名: 後続の記事列が消えたら Star にして余白を吸収する
			stationNameColumn.Width = new(
				isStaNameNarrow ? STA_NAME_COLUMN_WIDTH_NARROW : STA_NAME_COLUMN_WIDTH,
				isRemarksVisible ? GridUnitType.Absolute : GridUnitType.Star
			);

			// 着/発: モードごとに最適幅
			arrivalDepartureTimeColumn.Width = mode switch
			{
				< VerticalTimetableColumnVisibilityState.ViewWidthMode.IPHONE_SE_H
					=> ARR_DEP_COLUMN_WIDTH_NARROW,
				< VerticalTimetableColumnVisibilityState.ViewWidthMode.IPAD_MINI_2_3_4_5_V
					=> ARR_DEP_COLUMN_WIDTH_MINI6,
				_ => ARR_DEP_COLUMN_WIDTH,
			};

			// 着線/発線: 768pt 未満では少し詰めて (54px)、568pt 未満 (狭幅表示) では
			// さらに詰めて (48px) 幅に余裕を持たせる。
			// 常に Absolute に固定する。記事列は表示中は常に Star(1) (下記)
			// なので、制限速度が消えた分の余白は記事列が単独で吸収すればよい。
			// ここを Star にすると記事列 (Star 1) とこの列 (Star 54 相当の幅指定)
			// が競合し、比重の大きいこの列がほぼ全ての余白を奪って記事列が
			// 数 px まで潰れてしまう (iPad の 3:7 分割など 568〜735pt 帯で発生した
			// 「記事だけ実質非表示になる」不具合の原因)。
			double trackNumberColumnWidth = mode switch
			{
				< VerticalTimetableColumnVisibilityState.ViewWidthMode.IPHONE_SE_H
					=> TRACK_NUMBER_COLUMN_WIDTH_NARROW,
				< VerticalTimetableColumnVisibilityState.ViewWidthMode.IPAD_MINI_2_3_4_5_V
					=> TRACK_NUMBER_COLUMN_WIDTH_MID,
				_ => TRACK_NUMBER_COLUMN_WIDTH,
			};
			trackNumberColumn.Width = trackNumberColumnWidth;

			speedLimitColumn.Width = isSpeedLimitVisible ? SPEED_LIMIT_COLUMN_WIDTH : 0;
			remarksColumn.Width = isRemarksVisible ? new(1, GridUnitType.Star) : new(0);
			markerColumn.Width = isMarkerVisible ? MARKER_COLUMN_WIDTH : 0;
		};
	}

	public static readonly AppThemeGenericsValueTypeBindingExtension<double> AppIconOpacity = new(0.075, 0.025);
	public static readonly AppThemeColorBindingExtension AppIconBgColor = new(
		new(0xCC, 0xFF, 0xCC),
		new(0xA3, 0xCC, 0xA3)
	);
	public static readonly string AppIconSource = "appiconfg.png";
	static Style? _appIconStyleResource = null;
	public static Style AppIconStyleResource
	{
		get
		{
			if (_appIconStyleResource is not null)
				return _appIconStyleResource;

			_appIconStyleResource = new Style(typeof(Image))
			{
				Setters =
				{
					new Setter { Property = Image.SourceProperty, Value = AppIconSource },
					new Setter { Property = Image.AspectProperty, Value = Aspect.AspectFit },
					new Setter { Property = Image.MarginProperty, Value = new Thickness(8) },
					// なぜかここでAppThemeBindingでOpacityを設定しても反映されない
				}
			};

			return _appIconStyleResource;
		}
	}

	static Style? _labelStyleResource = null;
	public static Style LabelStyleResource
	{
		get
		{
			if (_labelStyleResource is not null)
				return _labelStyleResource;

			_labelStyleResource = new Style(typeof(Label));
			_labelStyleResource.Setters.Add(Label.HorizontalOptionsProperty, LayoutOptions.Center);
			_labelStyleResource.Setters.Add(Label.VerticalOptionsProperty, LayoutOptions.Center);
			_labelStyleResource.Setters.Add(Label.TextColorProperty, DefaultTextColor);
			_labelStyleResource.Setters.Add(Label.FontSizeProperty, DefaultTextSize);
			_labelStyleResource.Setters.Add(Label.FontFamilyProperty, DefaultFontFamily);
			_labelStyleResource.Setters.Add(Label.MarginProperty, new Thickness(4, 0));
			_labelStyleResource.Setters.Add(Label.LineBreakModeProperty, LineBreakMode.CharacterWrap);
			_labelStyleResource.Setters.Add(Label.LineHeightProperty, DeviceInfo.Platform == DevicePlatform.Android ? 0.75 : 1);
			_labelStyleResource.Setters.Add(Label.FontAutoScalingEnabledProperty, false);

			return _labelStyleResource;
		}
	}

	public static T LabelStyle<T>() where T : Label, new()
	{
		T v = new();

		v.HorizontalOptions = LayoutOptions.Center;
		v.VerticalOptions = LayoutOptions.Center;
		DefaultTextColor.Apply(v, Label.TextColorProperty);
		v.FontSize = DefaultTextSize;
		v.FontFamily = DefaultFontFamily;
		v.Margin = new(4, 0);
		v.LineBreakMode = LineBreakMode.CharacterWrap;

		v.LineHeight = DeviceInfo.Platform == DevicePlatform.Android ? 0.75 : 1.1;

		v.FontAutoScalingEnabled = false;

		return v;
	}
	public static T HtmlAutoDetectLabelStyle<T>() where T : HtmlAutoDetectLabel, new()
	{
		T v = new();

		v.HorizontalOptions = LayoutOptions.Center;
		v.VerticalOptions = LayoutOptions.Center;
		v.CurrentAppThemeColorBindingExtension = DefaultTextColor;
		v.FontSize = DefaultTextSize;
		v.FontFamily = DefaultFontFamily;
		v.Margin = new(4, 0);
		v.LineBreakMode = LineBreakMode.CharacterWrap;

		v.LineHeight = DeviceInfo.Platform == DevicePlatform.Android ? 0.75 : 1.1;

		v.FontAutoScalingEnabled = false;

		return v;
	}

	static Style? _BeforeRemarksStyleResource = null;
	public static Style BeforeRemarksStyleResource
	{
		get
		{
			if (_BeforeRemarksStyleResource is not null)
				return _BeforeRemarksStyleResource;

			_BeforeRemarksStyleResource = new Style(typeof(Label))
			{
				BasedOn = LabelStyleResource
			};

			_BeforeRemarksStyleResource.Setters.Add(Label.HorizontalOptionsProperty, LayoutOptions.Start);
			_BeforeRemarksStyleResource.Setters.Add(Label.VerticalOptionsProperty, LayoutOptions.End);
			_BeforeRemarksStyleResource.Setters.Add(Label.FontSizeProperty, BEFORE_REMARKS_FONT_SIZE);
			_BeforeRemarksStyleResource.Setters.Add(Label.LineHeightProperty, DeviceInfo.Platform == DevicePlatform.Android ? 1.0 : 1.25);
			_BeforeRemarksStyleResource.Setters.Add(Label.MarginProperty, new Thickness(BEFORE_REMARKS_LEFT_MARGIN, -BEFORE_REMARKS_FONT_SIZE, 0, BEFORE_REMARKS_BOTTOM_MARGIN));

			return _BeforeRemarksStyleResource;
		}
	}

	static Style? _BeforeRemarksStyleResourceLow = null;
	// ViewHeightMode.Low (縦の短い画面) 用の BeforeRemarks スタイル。行間を詰め
	// (LineHeight 1.0)、下の余白をなくし (Margin 下端 0)、フォントサイズを
	// DestinationLabel と揃えることで、縮んだ行の枠内に2行表示を収める。
	public static Style BeforeRemarksStyleResourceLow
	{
		get
		{
			if (_BeforeRemarksStyleResourceLow is not null)
				return _BeforeRemarksStyleResourceLow;

			_BeforeRemarksStyleResourceLow = new Style(typeof(Label))
			{
				BasedOn = LabelStyleResource
			};

			_BeforeRemarksStyleResourceLow.Setters.Add(Label.HorizontalOptionsProperty, LayoutOptions.Start);
			_BeforeRemarksStyleResourceLow.Setters.Add(Label.VerticalOptionsProperty, LayoutOptions.End);
			_BeforeRemarksStyleResourceLow.Setters.Add(Label.FontSizeProperty, BEFORE_REMARKS_FONT_SIZE_LOW);
			_BeforeRemarksStyleResourceLow.Setters.Add(Label.LineHeightProperty, BEFORE_REMARKS_LINE_HEIGHT_LOW);
			_BeforeRemarksStyleResourceLow.Setters.Add(Label.MarginProperty, new Thickness(BEFORE_REMARKS_LEFT_MARGIN, -BEFORE_REMARKS_FONT_SIZE_LOW, 0, BEFORE_REMARKS_BOTTOM_MARGIN_LOW));

			return _BeforeRemarksStyleResourceLow;
		}
	}
	public static T AfterRemarksStyle<T>() where T : HtmlAutoDetectLabel, new()
	{
		T v = HtmlAutoDetectLabelStyle<T>();

		v.HorizontalOptions = LayoutOptions.Start;
		v.VerticalOptions = LayoutOptions.Start;
		v.FontSize = AFTER_REMARKS_FONT_SIZE;
		v.FontAttributes = FontAttributes.Bold;
		v.LineHeight = DeviceInfo.Platform == DevicePlatform.Android ? 1.0 : 1.25;
		v.Margin = new(0, 0, 0, -AFTER_REMARKS_FONT_SIZE);

		return v;
	}

	static Style? _headerLabelStyleResource = null;
	public static Style HeaderLabelStyleResource
	{
		get
		{
			if (_headerLabelStyleResource is not null)
				return _headerLabelStyleResource;

			_headerLabelStyleResource = new Style(typeof(Label))
			{
				BasedOn = LabelStyleResource
			};

			_headerLabelStyleResource.Setters.Add(Label.TextColorProperty, HeaderTextColor);
			_headerLabelStyleResource.Setters.Add(Label.MarginProperty, new Thickness(1));

			return _headerLabelStyleResource;
		}
	}
	public static T HeaderLabelStyle<T>() where T : Label, new()
	{
		T v = LabelStyle<T>();

		HeaderTextColor.Apply(v, Label.TextColorProperty);
		v.Margin = new(1);

		return v;
	}

	public static T AffectDateLabelStyle<T>() where T : Label, new()
	{
		T v = LabelStyle<T>();

		v.Margin = new(18, -8, 0, -8);
		v.LineHeight = 1.2;
		v.FontSize = AffectDateFontSize;
		v.HorizontalOptions = LayoutOptions.Start;
		v.VerticalOptions = LayoutOptions.Center;
		v.Text = AffectDateLabelTextPrefix;
		Grid.SetColumnSpan(v, 4);

		return v;
	}

	public static T HakoTabWorkInfoLabelStyle<T>() where T : Label, new()
	{
		T v = AffectDateLabelStyle<T>();

		// Keep AffectDateLabelStyle's negative top/bottom margin (only override left/right) —
		// now that this label matches AffectDate's FontSize/LineHeight, it needs the same bleed
		// to fit its 2 lines in the fixed-height row; zeroing it out clipped the 2nd line off.
		v.Margin = new(0, v.Margin.Top, v.Margin.Left, v.Margin.Bottom);
		// Keep this label's own LineHeight (unlike AffectDateLabelStyle's) — it already looked
		// right, so don't let it drift if AffectDate's LineHeight changes independently.
		v.LineHeight = 1.0;
		v.FontFamily = HakoBoldFontFamily;
		v.FontAttributes = FontAttributes.Bold;
		v.FontSize = AffectDateFontSize;
		v.Text = null;
		v.HorizontalOptions = LayoutOptions.End;
		v.HorizontalTextAlignment = TextAlignment.End;

		return v;
	}

	public static T LargeLabelStyle<T>() where T : Label, new()
	{
		T v = LabelStyle<T>();

		v.FontSize = LargeTextSize;

		return v;
	}
	public static T LargeHtmlAutoDetectLabelStyle<T>() where T : HtmlAutoDetectLabel, new()
	{
		T v = HtmlAutoDetectLabelStyle<T>();

		v.FontSize = LargeTextSize;

		return v;
	}

	static Style? _timetableLabelStyleResource = null;
	public static Style TimetableLabelStyleResource
	{
		get
		{
			if (_timetableLabelStyleResource is not null)
				return _timetableLabelStyleResource;

			_timetableLabelStyleResource = new Style(typeof(Label))
			{
				BasedOn = LabelStyleResource
			};

			_timetableLabelStyleResource.Setters.Add(Label.TextColorProperty, TimetableTextColor);
			_timetableLabelStyleResource.Setters.Add(Label.FontSizeProperty, TimetableFontSize);
			_timetableLabelStyleResource.Setters.Add(Label.FontAttributesProperty, FontAttributes.Bold);
			_timetableLabelStyleResource.Setters.Add(Label.InputTransparentProperty, true);

			return _timetableLabelStyleResource;
		}
	}
	public static T TimetableLabel<T>() where T : Label, new()
	{
		T v = LabelStyle<T>();

		TimetableTextColor.Apply(v, Label.TextColorProperty);
		v.FontSize = TimetableFontSize;
		v.FontAttributes = FontAttributes.Bold;
		v.InputTransparent = true;

		return v;
	}
	public static T TimetableHtmlAutoDetectLabel<T>() where T : HtmlAutoDetectLabel, new()
	{
		T v = HtmlAutoDetectLabelStyle<T>();

		v.CurrentAppThemeColorBindingExtension = TimetableTextColor;
		v.FontSize = TimetableFontSize;
		v.FontAttributes = FontAttributes.Bold;
		v.InputTransparent = true;

		return v;
	}

	public static T TimetableInfoRowHtmlAutoDetectLabel<T>() where T : HtmlAutoDetectLabel, new()
	{
		T v = HtmlAutoDetectLabelStyle<T>();

		v.CurrentAppThemeColorBindingExtension = TimetableTextColor;
		v.Margin = new(0);
		v.FontSize = TimetableFontSize;
		v.InputTransparent = true;

		return v;
	}

	static Style? _timetableLargeNumberLabelStyleResource = null;
	public static Style TimetableLargeNumberLabelStyleResource
	{
		get
		{
			if (_timetableLargeNumberLabelStyleResource is not null)
				return _timetableLargeNumberLabelStyleResource;

			_timetableLargeNumberLabelStyleResource = new Style(typeof(Label))
			{
				BasedOn = TimetableLabelStyleResource
			};

			_timetableLargeNumberLabelStyleResource.Setters.Add(Label.FontFamilyProperty, TimetableNumFontFamily);
			_timetableLargeNumberLabelStyleResource.Setters.Add(Label.VerticalOptionsProperty, LayoutOptions.End);
			_timetableLargeNumberLabelStyleResource.Setters.Add(Label.LineBreakModeProperty, LineBreakMode.NoWrap);

			return _timetableLargeNumberLabelStyleResource;
		}
	}
	public static T TimetableLargeNumberLabel<T>() where T : Label, new()
	{
		T v = TimetableLabel<T>();

		v.FontFamily = "Helvetica";
		v.VerticalOptions = LayoutOptions.End;
		v.LineBreakMode = LineBreakMode.NoWrap;

		return v;
	}

	public static T TimetableRunLimitLabel<T>() where T : Label, new()
	{
		T v = TimetableLargeNumberLabel<T>();

		v.FontSize = TimetableRunLimitFontSize;
		v.Margin = v.Padding = new(0);
		v.VerticalOptions = LayoutOptions.Center;

		return v;
	}

	public static T TimetableDriveTimeMMLabel<T>() where T : Label, new()
	{
		T v = TimetableLargeNumberLabel<T>();

		v.FontSize = DriveTimeMMFontSize;
		v.Margin = v.Padding = new(0);
		v.HorizontalOptions = LayoutOptions.End;

		return v;
	}

	public static T TimetableDriveTimeSSLabel<T>() where T : Label, new()
	{
		T v = TimetableLargeNumberLabel<T>();

		v.FontSize = DriveTimeSSFontSize;
		v.Margin = new(1);
		v.Padding = new(0);
		v.HorizontalOptions = LayoutOptions.Start;

		return v;
	}

	static Style? _timetableDefaultNumberLabelStyleResource = null;
	public static Style TimetableDefaultNumberLabelStyleResource
	{
		get
		{
			if (_timetableDefaultNumberLabelStyleResource is not null)
				return _timetableDefaultNumberLabelStyleResource;

			_timetableDefaultNumberLabelStyleResource = new Style(typeof(Label))
			{
				BasedOn = TimetableLargeNumberLabelStyleResource
			};

			_timetableDefaultNumberLabelStyleResource.Setters.Add(Label.FontSizeProperty, 16);
			_timetableDefaultNumberLabelStyleResource.Setters.Add(Label.MarginProperty, new Thickness(1, 3));

			return _timetableDefaultNumberLabelStyleResource;
		}
	}
	public static T TimetableDefaultNumberLabel<T>() where T : Label, new()
	{
		T v = TimetableLabel<T>();

		v.FontSize = 16;
		v.Margin = new(1, 3);

		return v;
	}

	[GeneratedRegex("<[^>]*>")]
	private static partial Regex HtmlTagRegex();
	[GeneratedRegex("<br[^>]*/?>")]
	private static partial Regex HtmlBrTagRegex();
	[GeneratedRegex("&[^;]+;")]
	private static partial Regex XmlEscapedStrRegex();
	public static double GetTimetableTrackLabelFontSize(string trackName, double currentFontSize)
	{
		bool isTrackNameHtml = trackName.StartsWith('<');
		if (isTrackNameHtml)
		{
			trackName = HtmlBrTagRegex().Replace(trackName, "\n");
			trackName = HtmlTagRegex().Replace(trackName, "");
			trackName = XmlEscapedStrRegex().Replace(trackName, "");
		}
		int maxLineLength = trackName.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(static v => v.Length).Max();
		if (maxLineLength <= 2)
			return currentFontSize;
		else
			return currentFontSize * (2.0 / maxLineLength);
	}

	static readonly AppThemeGenericsBindingExtension<Brush> SeparatorLineBrush = SeparatorLineColor.ToBrushTheme();
	static Style? _horizontalSeparatorLineStyleResource = null;
	public static Style HorizontalSeparatorLineStyleResource
	{
		get
		{
			if (_horizontalSeparatorLineStyleResource is not null)
				return _horizontalSeparatorLineStyleResource;

			_horizontalSeparatorLineStyleResource = new Style(typeof(Line));

			_horizontalSeparatorLineStyleResource.Setters.Add(Line.VerticalOptionsProperty, LayoutOptions.End);
			_horizontalSeparatorLineStyleResource.Setters.Add(Line.StrokeThicknessProperty, 0.5);
			_horizontalSeparatorLineStyleResource.Setters.Add(Line.HeightRequestProperty, 0.5);
			_horizontalSeparatorLineStyleResource.Setters.Add(Line.StrokeProperty, SeparatorLineBrush.Default);
			_horizontalSeparatorLineStyleResource.Setters.Add(Grid.ColumnSpanProperty, 8);
			_horizontalSeparatorLineStyleResource.Setters.Add(Line.X1Property, 0);
			_horizontalSeparatorLineStyleResource.Setters.Add(Line.X2Property, 10000);

			return _horizontalSeparatorLineStyleResource;
		}
	}
	static Style? _verticalSeparatorLineStyleResource = null;
	public static Style VerticalSeparatorLineStyleResource
	{
		get
		{
			if (_verticalSeparatorLineStyleResource is not null)
				return _verticalSeparatorLineStyleResource;

			_verticalSeparatorLineStyleResource = new Style(typeof(Line));

			_verticalSeparatorLineStyleResource.Setters.Add(Line.HorizontalOptionsProperty, LayoutOptions.End);
			_verticalSeparatorLineStyleResource.Setters.Add(Line.MarginProperty, new Thickness(0, 6));
			_verticalSeparatorLineStyleResource.Setters.Add(Line.StrokeProperty, SeparatorLineBrush.Default);
			_verticalSeparatorLineStyleResource.Setters.Add(Line.StrokeThicknessProperty, 1);
			_verticalSeparatorLineStyleResource.Setters.Add(Line.WidthRequestProperty, 1);
			_verticalSeparatorLineStyleResource.Setters.Add(Line.Y1Property, 0);
			_verticalSeparatorLineStyleResource.Setters.Add(Line.Y2Property, 100);

			return _verticalSeparatorLineStyleResource;
		}
	}
	public static Line HorizontalSeparatorLineStyle()
	{
		Line v = new()
		{
			VerticalOptions = LayoutOptions.End,
			StrokeThickness = 0.5,
			HeightRequest = 0.5,
			X1 = 0,
			X2 = 10000,
		};

		SeparatorLineBrush.Apply(v, Line.StrokeProperty);

		return v;
	}
	public static Line TimetableRowHorizontalSeparatorLineStyle()
	{
		Line v = HorizontalSeparatorLineStyle();

		v.Opacity = 0.5;

		return v;
	}
	public static void AddTimetableRowHorizontalSeparatorLineStyle(Grid grid, int row)
		=> AddHorizontalSeparatorLineStyle(grid, TimetableRowHorizontalSeparatorLineStyle(), row);
	public static void AddHorizontalSeparatorLineStyle(Grid grid, Line line, int row)
	{
		Grid.SetRow(line, row);
		Grid.SetColumnSpan(line, 8);
		grid.Add(line);
	}

	public static TimeCell TimeCell()
	{
		TimeCell v = [];

		v.VerticalOptions
			= v.HorizontalOptions
			= LayoutOptions.Center;

		return v;
	}

	public static Grid LastStopLineGrid()
	{
		Grid v = new()
		{
			RowDefinitions =
			{
				new RowDefinition(new GridLength(1, GridUnitType.Star)),
				new RowDefinition(new GridLength(1, GridUnitType.Star)),
				new RowDefinition(new GridLength(1, GridUnitType.Star)),
				new RowDefinition(new GridLength(1, GridUnitType.Star)),
			}
		};

		v.Add(LastStopLine(), row: 1);
		v.Add(LastStopLine(), row: 2);
		v.InputTransparent = true;

		return v;
	}

	static readonly AppThemeGenericsBindingExtension<Brush> LastStopLineBrush = TimetableTextColor.ToBrushTheme();
	public static Line LastStopLine()
	{
		Line v = new()
		{
			StrokeThickness = 4,
			HeightRequest = 4,
			X1 = 22,
			X2 = 106,
			Y1 = 0,
			Y2 = 0,
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.Center,
		};

		LastStopLineBrush.Apply(v, Line.StrokeProperty);

		return v;
	}
}
