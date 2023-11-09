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
	public static readonly AppThemeColorBindingExtension SeparatorLineColor = genColor(0xDD, baseDarkColor + 0x33);
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

	public static T LabelStyle<T>() where T : Label, new()
	{
		T v = new();

		v.HorizontalOptions = LayoutOptions.Center;
		v.VerticalOptions = LayoutOptions.Center;
		DefaultTextColor.Apply(v, Label.TextColorProperty);
		v.FontSize = DefaultTextSize;
		v.FontFamily = DefaultFontFamily;
		v.Margin = new(4);
		v.LineBreakMode = LineBreakMode.CharacterWrap;

		v.LineHeight = DeviceInfo.Platform == DevicePlatform.Android ? 0.9 : 1.1;

		return v;
	}

	public static T HeaderLabelStyle<T>() where T : Label, new()
	{
		T v = LabelStyle<T>();

		HeaderTextColor.Apply(v, Label.TextColorProperty);

		return v;
	}

	public static T AffectDateLabelStyle<T>() where T : Label, new()
	{
		T v = LabelStyle<T>();

		v.Margin = new(18, 4);
		v.LineHeight = 1.2;
		v.FontSize = 18;
		v.HorizontalOptions = LayoutOptions.Start;
		v.Text = AffectDateLabelTextPrefix;

		return v;
	}

	public static T HakoTabWorkInfoLabelStyle<T>() where T : Label, new()
	{
		T v = LabelStyle<T>();

		v.Margin = new(12, 4);
		v.LineHeight = 1.2;
		v.FontAttributes = FontAttributes.Bold;
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

	public static T TimetableLabel<T>() where T : Label, new()
	{
		T v = LabelStyle<T>();

		TimetableTextColor.Apply(v, Label.TextColorProperty);
		v.FontSize = DeviceInfo.Current.Platform == DevicePlatform.iOS ? 28 : 26;
		v.FontAttributes = FontAttributes.Bold;
		v.InputTransparent = true;

		return v;
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

	public static T TimetableDefaultNumberLabel<T>() where T : Label, new()
	{
		T v = TimetableLabel<T>();

		v.FontSize = 16;
		v.Margin = new(1, 3);

		return v;
	}

	public static Line HorizontalSeparatorLineStyle()
	{
		Line v = new();

		v.VerticalOptions = LayoutOptions.End;
		SeparatorLineColor.Apply(v, Line.BackgroundColorProperty);
		v.StrokeThickness = 0.5;
		v.HeightRequest = 0.5;

		Grid.SetColumnSpan(v, 8);

		return v;
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

		TimetableTextColor.Apply(v, Line.BackgroundColorProperty);

		return v;
	}
}
