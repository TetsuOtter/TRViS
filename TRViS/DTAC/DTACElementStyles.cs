using Microsoft.Maui.Controls.Shapes;

namespace TRViS.DTAC;

public static class DTACElementStyles
{
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
	public static readonly AppThemeColorBindingExtension DarkGreen = new(
		new(0x00, 0x44, 0x00),
		new(0x00, 0x33, 0x00)
	);

	public static readonly AppThemeColorBindingExtension ForegroundBlackWhite = genColor(0x00, 0xFF);
	public static readonly AppThemeGenericsBindingExtension<Brush> ForegroundBlackWhiteBrush = ForegroundBlackWhite.ToBrushTheme();

	public static readonly AppThemeColorBindingExtension LocationServiceSelectedSideFrameColor = genColor(0xFF, 0xAA);
	public static readonly AppThemeColorBindingExtension LocationServiceSelectedSideTextColor = genColor(0xFF, 0xDD);
	public static readonly AppThemeColorBindingExtension LocationServiceNotSelectedSideBaseColor = genColor(0xFF, 0xDD);

	public static readonly AppThemeColorBindingExtension StartEndRunButtonTextColor = genColor(0xFF, 0xE0);

	public static readonly int DefaultTextSize = 14;
	public static readonly int LargeTextSize = 24;

	public const int BeforeDeparture_AfterArrive_Height = 45;

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
	static public ColumnDefinitionCollection TimetableColumnWidthCollection => new(
		new(new(RUN_TIME_COLUMN_WIDTH)),
		new(new(140)),
		new(new(140)),
		new(new(140)),
		new(new(60)),
		new(new(60)),
		new(new(1, GridUnitType.Star)),
		new(new(64))
		);

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
			_labelStyleResource.Setters.Add(Label.LineHeightProperty, DeviceInfo.Platform == DevicePlatform.Android ? 0.9 : 1.1);

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
		v.Margin = new(4,0);
		v.LineBreakMode = LineBreakMode.CharacterWrap;

		v.LineHeight = DeviceInfo.Platform == DevicePlatform.Android ? 0.9 : 1.1;

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

		v.Margin = new(18, 0);
		v.LineHeight = 1.4;
		v.FontSize = 16;
		v.HorizontalOptions = LayoutOptions.Start;
		v.Text = AffectDateLabelTextPrefix;

		return v;
	}

	public static T HakoTabWorkInfoLabelStyle<T>() where T : Label, new()
	{
		T v = AffectDateLabelStyle<T>();

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
			_timetableLabelStyleResource.Setters.Add(Label.FontSizeProperty, DeviceInfo.Current.Platform == DevicePlatform.iOS ? 28 : 26);
			_timetableLabelStyleResource.Setters.Add(Label.FontAttributesProperty, FontAttributes.Bold);
			_timetableLabelStyleResource.Setters.Add(Label.InputTransparentProperty, true);

			return _timetableLabelStyleResource;
		}
	}
	public static T TimetableLabel<T>() where T : Label, new()
	{
		T v = LabelStyle<T>();

		TimetableTextColor.Apply(v, Label.TextColorProperty);
		v.FontSize = DeviceInfo.Current.Platform == DevicePlatform.iOS ? 28 : 26;
		v.FontAttributes = FontAttributes.Bold;
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

		v.FontSize = DeviceInfo.Current.Platform == DevicePlatform.iOS ? 24 : 22;
		v.Margin = v.Padding = new(0);
		v.VerticalOptions = LayoutOptions.Center;

		return v;
	}

	public static T TimetableDriveTimeMMLabel<T>() where T : Label, new()
	{
		T v = TimetableLargeNumberLabel<T>();

		v.FontSize = 26;
		v.Margin = v.Padding = new(0);
		v.HorizontalOptions = LayoutOptions.End;

		return v;
	}

	public static T TimetableDriveTimeSSLabel<T>() where T : Label, new()
	{
		T v = TimetableLargeNumberLabel<T>();

		v.FontSize = 18;
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
				BasedOn = TimetableLabelStyleResource
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
			_horizontalSeparatorLineStyleResource.Setters.Add(Line.FillProperty, SeparatorLineBrush);
			_horizontalSeparatorLineStyleResource.Setters.Add(Line.BackgroundColorProperty, SeparatorLineColor);
			_horizontalSeparatorLineStyleResource.Setters.Add(Grid.ColumnSpanProperty, 8);

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
			_verticalSeparatorLineStyleResource.Setters.Add(Line.FillProperty, SeparatorLineBrush);
			_verticalSeparatorLineStyleResource.Setters.Add(Line.BackgroundColorProperty, SeparatorLineColor);
			_verticalSeparatorLineStyleResource.Setters.Add(Line.StrokeThicknessProperty, 1);
			_verticalSeparatorLineStyleResource.Setters.Add(Line.WidthRequestProperty, 1);

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
			Fill = SeparatorLineBrush.Default,
		};

		SeparatorLineColor.Apply(v, Line.BackgroundColorProperty);

		return v;
	}
	public static void AddHorizontalSeparatorLineStyle(Grid grid, int row)
		=> AddHorizontalSeparatorLineStyle(grid, HorizontalSeparatorLineStyle(), row);
	public static void AddHorizontalSeparatorLineStyle(Grid grid, Line line, int row)
	{
		Grid.SetRow(line, row);
		Grid.SetColumnSpan(line, 8);
		grid.Add(line);
	}

	public static TimeCell TimeCell()
	{
		TimeCell v = new();

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

		LastStopLineBrush.Apply(v, Line.FillProperty);
		TimetableTextColor.Apply(v, Line.BackgroundColorProperty);

		return v;
	}
}
