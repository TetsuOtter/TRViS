using System.Text.RegularExpressions;

using Microsoft.Maui.Controls.Shapes;

using TRViS.Controls;
using TRViS.Services;

namespace TRViS.DTAC;

public partial class DTACElementStyles
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	public static readonly DTACElementStyles Instance = new();

	static Color genColor(byte value)
		=> new(value, value, value);
	static AppThemeColorBindingExtension genColor(byte defaultColorValue, byte darkColorValue)
		=> new(genColor(defaultColorValue), genColor(darkColorValue));

	const byte baseDarkColor = 0x25;

	public readonly AppThemeColorBindingExtension DefaultTextColor = genColor(0x33, 0xFF);
	public readonly AppThemeColorBindingExtension HeaderTextColor = genColor(0x55, 0xFF);
	public readonly AppThemeColorBindingExtension TimetableTextColor = genColor(0x00, 0xDD);
	public readonly AppThemeColorBindingExtension TimetableTextInvColor = genColor(0xFF, 0xFF);
	public readonly AppThemeColorBindingExtension TrainNumNextDayTextColor = new(
		new(0x33, 0x33, 0xDD),
		new(0x44, 0x99, 0xFF)
	);
	public readonly AppThemeColorBindingExtension HeaderBackgroundColor = genColor(0xDD, baseDarkColor + 0x18);
	public readonly AppThemeColorBindingExtension SeparatorLineColor = genColor(0xAA, baseDarkColor + 0x33);
	public readonly AppThemeColorBindingExtension DefaultBGColor = genColor(0xFF, baseDarkColor);
	public readonly AppThemeColorBindingExtension CarCountBGColor = genColor(0xFE, baseDarkColor + 0x11);
	public readonly AppThemeColorBindingExtension TabAreaBGColor = genColor(0xEE, baseDarkColor - 0x20);
	public readonly AppThemeColorBindingExtension TabButtonBGColor = genColor(0xDD, baseDarkColor - 0x11);

	public readonly AppThemeColorBindingExtension OpenCloseButtonBGColor = genColor(0xFE, 0x4A);
	public readonly AppThemeColorBindingExtension OpenCloseButtonTextColor = genColor(0xAA, 0x99);
	public readonly AppThemeColorBindingExtension MarkerButtonIconColor = new(
		new(0x00, 0x44, 0x00),
		new(0x00, 0x99, 0x00)
	);
	public readonly AppThemeColorBindingExtension MarkerMarkButtonBGColor = genColor(0xFA, 0x4A);
	public readonly AppThemeGenericsBindingExtension<Brush> MarkerMarkButtonBGColorBrush;

	public readonly AppThemeColorBindingExtension DefaultGreen = new(
		new(0x00, 0x80, 0x00),
		new(0x00, 0x80, 0x00)
	);
	public readonly AppThemeColorBindingExtension SemiDarkGreen = new(
		new(0x00, 0x77, 0x00),
		new(0x00, 0x77, 0x00)
	);
	public readonly AppThemeColorBindingExtension DarkGreen = new(
		new(0x00, 0x44, 0x00),
		new(0x00, 0x33, 0x00)
	);

	public readonly AppThemeColorBindingExtension ForegroundBlackWhite = genColor(0x00, 0xFF);
	public readonly AppThemeGenericsBindingExtension<Brush> ForegroundBlackWhiteBrush;

	public readonly AppThemeColorBindingExtension LocationServiceSelectedSideBorderColor = genColor(0xFF, 0xAA);
	public readonly AppThemeColorBindingExtension LocationServiceSelectedSideDisabledBorderColor = genColor(0xDD, 0x99);
	public readonly AppThemeColorBindingExtension LocationServiceSelectedSideTextColor = genColor(0xFF, 0xDD);
	public readonly AppThemeColorBindingExtension LocationServiceNotSelectedSideTextColor = genColor(0x00, 0x00);
	public readonly AppThemeColorBindingExtension LocationServiceNotSelectedSideBaseColor = genColor(0xFF, 0xDD);
	public readonly AppThemeColorBindingExtension LocationServiceNotSelectedSideDisabledBaseColor = genColor(0xDD, 0x99);

	public readonly AppThemeColorBindingExtension StartEndRunButtonTextColor = genColor(0xFF, 0xE0);

	public readonly double DefaultTextSize = 14;
	public readonly double DefaultTextSizePlus = 15;
	public readonly double LargeTextSize = 24;
	public readonly double TimetableFontSize = DeviceInfo.Current.Platform == DevicePlatform.iOS ? 28 : 26;
	public readonly double TimetableFontSizeNarrow;
	public readonly double TimetableRunLimitFontSize = DeviceInfo.Current.Platform == DevicePlatform.iOS ? 24 : 22;

	public const int BeforeDeparture_AfterArrive_Height = 45;

	public const int TimetableRowMarkerBackgroundZIndex = 0;
	public const int TimetableRowLocationBoxZIndex = 5;
	public const int TimetableRowRunTimeTextZIndex = 10;

	public const string DefaultFontFamily = "Hiragino Sans";
	public const string MaterialIconFontFamily = "MaterialIconsRegular";
	public const string TimetableNumFontFamily = "Helvetica";

	public const string AffectDateLabelTextPrefix = "行路施行日\n";

	public readonly Shadow DefaultShadow = new()
	{
		Brush = Colors.Black,
		Offset = new(3, 3),
		Radius = 3,
		Opacity = 0.2f
	};

	public readonly AppThemeGenericsValueTypeBindingExtension<double> AppIconOpacity = new(0.075, 0.025);
	public readonly AppThemeColorBindingExtension AppIconBgColor = new(
		new(0xCC, 0xFF, 0xCC),
		new(0xA3, 0xCC, 0xA3)
	);
	public readonly string AppIconSource = "appiconfg.png";
	private Style? _appIconStyleResource = null;
	public Style AppIconStyleResource
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

	private Style? _labelStyleResource = null;
	public Style LabelStyleResource
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
			_labelStyleResource.Setters.Add(Label.LineHeightProperty, DeviceInfo.Platform == DevicePlatform.Android ? 0.75 : 1.1);
			_labelStyleResource.Setters.Add(Label.FontAutoScalingEnabledProperty, false);

			return _labelStyleResource;
		}
	}

	public T LabelStyle<T>() where T : Label, new()
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
	public T HtmlAutoDetectLabelStyle<T>() where T : HtmlAutoDetectLabel, new()
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

	private Style? _BeforeRemarksStyleResource = null;
	public Style BeforeRemarksStyleResource
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
			_BeforeRemarksStyleResource.Setters.Add(Label.FontSizeProperty, DefaultTextSizePlus);
			_BeforeRemarksStyleResource.Setters.Add(Label.LineHeightProperty, DeviceInfo.Platform == DevicePlatform.Android ? 1.0 : 1.5);
			_BeforeRemarksStyleResource.Setters.Add(Label.MarginProperty, new Thickness(32, 0, 0, 10));

			return _BeforeRemarksStyleResource;
		}
	}
	public T AfterRemarksStyle<T>() where T : HtmlAutoDetectLabel, new()
	{
		T v = HtmlAutoDetectLabelStyle<T>();

		v.HorizontalOptions = LayoutOptions.Start;
		v.VerticalOptions = LayoutOptions.Start;
		v.FontSize = DefaultTextSizePlus;
		v.FontAttributes = FontAttributes.Bold;
		v.LineHeight = DeviceInfo.Platform == DevicePlatform.Android ? 1.0 : 1.6;
		// LineHeight分だけ上に隙間が空くため、MarginTopは設定しない
		v.Margin = new(32, 0, 0, 0);

		return v;
	}

	private Style? _headerLabelStyleResource = null;
	public Style HeaderLabelStyleResource
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
	public T HeaderLabelStyle<T>() where T : Label, new()
	{
		T v = LabelStyle<T>();

		HeaderTextColor.Apply(v, Label.TextColorProperty);
		v.Margin = new(1);

		return v;
	}

	public T AffectDateLabelStyle<T>() where T : Label, new()
	{
		T v = LabelStyle<T>();

		v.Margin = new(18, 0, 0, 0);
		v.LineHeight = 1.4;
		v.FontSize = 16;
		v.HorizontalOptions = LayoutOptions.Start;
		v.Text = AffectDateLabelTextPrefix;

		return v;
	}

	public T HakoTabWorkInfoLabelStyle<T>() where T : Label, new()
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

	public T LargeLabelStyle<T>() where T : Label, new()
	{
		T v = LabelStyle<T>();

		v.FontSize = LargeTextSize;

		return v;
	}
	public T LargeHtmlAutoDetectLabelStyle<T>() where T : HtmlAutoDetectLabel, new()
	{
		T v = HtmlAutoDetectLabelStyle<T>();

		v.FontSize = LargeTextSize;

		return v;
	}

	private Style? _timetableLabelStyleResource = null;
	public Style TimetableLabelStyleResource
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
	public T TimetableLabel<T>() where T : Label, new()
	{
		T v = LabelStyle<T>();

		TimetableTextColor.Apply(v, Label.TextColorProperty);
		v.FontSize = TimetableFontSize;
		v.FontAttributes = FontAttributes.Bold;
		v.InputTransparent = true;

		return v;
	}
	public T TimetableHtmlAutoDetectLabel<T>() where T : HtmlAutoDetectLabel, new()
	{
		T v = HtmlAutoDetectLabelStyle<T>();

		v.CurrentAppThemeColorBindingExtension = TimetableTextColor;
		v.FontSize = TimetableFontSize;
		v.FontAttributes = FontAttributes.Bold;
		v.InputTransparent = true;

		return v;
	}

	private Style? _timetableLargeNumberLabelStyleResource = null;
	public Style TimetableLargeNumberLabelStyleResource
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
	public T TimetableLargeNumberLabel<T>() where T : Label, new()
	{
		T v = TimetableLabel<T>();

		v.FontFamily = "Helvetica";
		v.VerticalOptions = LayoutOptions.End;
		v.LineBreakMode = LineBreakMode.NoWrap;

		return v;
	}

	public T TimetableRunLimitLabel<T>() where T : Label, new()
	{
		T v = TimetableLargeNumberLabel<T>();

		v.FontSize = TimetableRunLimitFontSize;
		v.Margin = v.Padding = new(0);
		v.VerticalOptions = LayoutOptions.Center;

		return v;
	}

	public T TimetableDriveTimeMMLabel<T>() where T : Label, new()
	{
		T v = TimetableLargeNumberLabel<T>();

		v.FontSize = 26;
		v.Margin = v.Padding = new(0);
		v.HorizontalOptions = LayoutOptions.End;

		return v;
	}

	public T TimetableDriveTimeSSLabel<T>() where T : Label, new()
	{
		T v = TimetableLargeNumberLabel<T>();

		v.FontSize = 18;
		v.Margin = new(1);
		v.Padding = new(0);
		v.HorizontalOptions = LayoutOptions.Start;

		return v;
	}

	private Style? _timetableDefaultNumberLabelStyleResource = null;
	public Style TimetableDefaultNumberLabelStyleResource
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
	public T TimetableDefaultNumberLabel<T>() where T : Label, new()
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
	public double GetTimetableTrackLabelFontSize(string trackName, double currentFontSize)
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

	private readonly AppThemeGenericsBindingExtension<Brush> SeparatorLineBrush;
	private Style? _horizontalSeparatorLineStyleResource = null;
	public Style HorizontalSeparatorLineStyleResource
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
	private Style? _verticalSeparatorLineStyleResource = null;
	public Style VerticalSeparatorLineStyleResource
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
	public Line HorizontalSeparatorLineStyle()
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
	public Line TimetableRowHorizontalSeparatorLineStyle()
	{
		Line v = HorizontalSeparatorLineStyle();

		v.Opacity = 0.5;

		return v;
	}
	public void AddTimetableRowHorizontalSeparatorLineStyle(Grid grid, int row)
		=> AddHorizontalSeparatorLineStyle(grid, TimetableRowHorizontalSeparatorLineStyle(), row);
	public void AddHorizontalSeparatorLineStyle(Grid grid, Line line, int row)
	{
		Grid.SetRow(line, row);
		Grid.SetColumnSpan(line, 8);
		grid.Add(line);
	}

	public TimeCell TimeCell()
	{
		TimeCell v = new();

		v.VerticalOptions
			= v.HorizontalOptions
			= LayoutOptions.Center;

		return v;
	}

	public Grid LastStopLineGrid()
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

	private readonly AppThemeGenericsBindingExtension<Brush> LastStopLineBrush;
	public Line LastStopLine()
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

	private DTACElementStyles()
	{
		ForegroundBlackWhiteBrush = ForegroundBlackWhite.ToBrushTheme();
		MarkerMarkButtonBGColorBrush = MarkerMarkButtonBGColor.ToBrushTheme();
		TimetableFontSizeNarrow = TimetableFontSize - 4;
		SeparatorLineBrush = SeparatorLineColor.ToBrushTheme();
		LastStopLineBrush = TimetableTextColor.ToBrushTheme();
	}
}
