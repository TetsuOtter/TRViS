using System.Text.RegularExpressions;

using Microsoft.Maui.Controls.Shapes;

using TRViS.Controls;
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
	public static readonly AppThemeColorBindingExtension HeaderBackgroundColor = genColor(0xDD, baseDarkColor + 0x18);
	public static readonly AppThemeColorBindingExtension SeparatorLineColor = genColor(0xAA, baseDarkColor + 0x33);
	public static readonly AppThemeColorBindingExtension DefaultBGColor = genColor(0xFF, baseDarkColor);
	public static readonly AppThemeColorBindingExtension CarCountBGColor = genColor(0xFE, baseDarkColor + 0x11);
	public static readonly AppThemeColorBindingExtension TabAreaBGColor = genColor(0xEE, baseDarkColor - 0x20);
	public static readonly AppThemeColorBindingExtension TabButtonBGColor = genColor(0xDD, baseDarkColor - 0x11);

	public static readonly AppThemeColorBindingExtension OpenCloseButtonBGColor = genColor(0xFE, 0x4A);
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
		new(0x00, 0x44, 0x00),
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
	public const double AffectDateFontSize = 18;
	public const double BEFORE_REMARKS_FONT_SIZE = 17;
	public const double AFTER_REMARKS_FONT_SIZE = 20;
	public static readonly double TimetableFontSize = DeviceInfo.Current.Platform == DevicePlatform.iOS ? 32 : 30;
	public static readonly double TimetableRunLimitFontSize = DeviceInfo.Current.Platform == DevicePlatform.iOS ? 26 : 24;
	public static readonly double DriveTimeMMFontSize = DeviceInfo.Current.Platform == DevicePlatform.iOS ? 28 : 26;
	public static readonly double DriveTimeSSFontSize = DeviceInfo.Current.Platform == DevicePlatform.iOS ? 18 : 16;

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

	public const string AffectDateLabelTextPrefix = "行路施行日\n";

	public static readonly Shadow DefaultShadow = new()
	{
		Brush = Colors.Black,
		Offset = new(3, 3),
		Radius = 3,
		Opacity = 0.2f
	};

	public const double RUN_TIME_COLUMN_WIDTH = 60;
	private const int DEFAULT_TIME_COLUMN_WIDTH = 140;
	private const int NARROW_TIME_COLUMN_WIDTH = 134;
	public static void SetTimetableColumnWidthCollection(Grid grid)
	{
		ColumnDefinition runTimeColumn = new(new(RUN_TIME_COLUMN_WIDTH));
		ColumnDefinition stationNameColumn = new(new(140));
		ColumnDefinition arrivalDepartureTimeColumn = new(new(140));
		ColumnDefinition trackNumberColumn = new(new(60));
		ColumnDefinition speedLimitColumn = new(new(60));
		ColumnDefinition remarksColumn = new(new(1, GridUnitType.Star));
		ColumnDefinition markerColumn = new(new(64));
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
		grid.SizeChanged += (s, e) =>
		{
			logger.Debug("TimetableColumnWidthCollection SizeChanged (height={0}, width={1})", grid.Height, grid.Width);
			if (0 < grid.Width && grid.Width < 768)
			{
				if (arrivalDepartureTimeColumn.Width.Value != NARROW_TIME_COLUMN_WIDTH)
				{
					arrivalDepartureTimeColumn.Width = new(NARROW_TIME_COLUMN_WIDTH);
					logger.Debug("TimetableColumnWidthCollection SetArrDepCol Width: NARROW_TIME_COLUMN_WIDTH");
				}
			}
			else if (arrivalDepartureTimeColumn.Width.Value != DEFAULT_TIME_COLUMN_WIDTH)
			{
				arrivalDepartureTimeColumn.Width = new(DEFAULT_TIME_COLUMN_WIDTH);
				logger.Debug("TimetableColumnWidthCollection SetArrDepCol Width: DEFAULT_TIME_COLUMN_WIDTH");
			}
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
			_BeforeRemarksStyleResource.Setters.Add(Label.MarginProperty, new Thickness(BEFORE_REMARKS_LEFT_MARGIN, -BEFORE_REMARKS_FONT_SIZE, 0, 8));

			return _BeforeRemarksStyleResource;
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
		v.LineHeight = 1.4;
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

		v.Margin = new(0, 0, v.Margin.Left, 0);
		v.FontAttributes = FontAttributes.Bold;
		v.FontSize = DefaultTextSize;
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
