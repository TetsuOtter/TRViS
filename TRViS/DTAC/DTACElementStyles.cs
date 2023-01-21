using Microsoft.Maui.Controls.Shapes;

namespace TRViS.DTAC;

public static class DTACElementStyles
{
	public static readonly Color DefaultTextColor = new(0x33, 0x33, 0x33);
	public static readonly Color HeaderTextColor = new(0x55, 0x55, 0x55);
	public static readonly Color HeaderBackgroundColor = new(0xdd, 0xdd, 0xdd);
	public static readonly Color SeparatorLineColor = new(0xaa, 0xaa, 0xaa);

	public static readonly int DefaultTextSize = 14;
	public static readonly int LargeTextSize = 24;

	public static readonly int BeforeDeparture_AfterArrive_Height = 48;

	public const double RUN_TIME_COLUMN_WIDTH = 48;
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
		v.TextColor = DefaultTextColor;
		v.FontSize = DefaultTextSize;
		v.FontFamily = "Hiragino Sans";
		v.Margin = new(4);
		v.LineBreakMode = LineBreakMode.CharacterWrap;

		v.LineHeight = DeviceInfo.Platform == DevicePlatform.Android ? 0.9 : 1;

		return v;
	}

	public static T HeaderLabelStyle<T>() where T : Label, new()
	{
		T v = LabelStyle<T>();

		v.TextColor = HeaderTextColor;

		return v;
	}

	public static T LargeLabelStyle<T>() where T : Label, new()
	{
		T v = LabelStyle<T>();

		v.FontSize = LargeTextSize;

		return v;
	}

	public static Line HorizontalSeparatorLineStyle()
	{
		Line v = new();

		v.VerticalOptions = LayoutOptions.End;
		v.BackgroundColor = SeparatorLineColor;
		v.StrokeThickness = 0.5;
		v.HeightRequest = 0.5;

		return v;
	}
}
