namespace TRViS.OriginalTimetable.Controls;

// 区間切替 (max-speed change) を示す薄い帯。V1 行間に挿入。
public class SectionBreakHeader : Border
{
	public static readonly BindableProperty SpeedKmhProperty = BindableProperty.Create(
		nameof(SpeedKmh), typeof(int?), typeof(SectionBreakHeader), default(int?),
		propertyChanged: OnTextChanged);
	public static readonly BindableProperty SpeedClassProperty = BindableProperty.Create(
		nameof(SpeedClass), typeof(string), typeof(SectionBreakHeader), default(string),
		propertyChanged: OnTextChanged);

	public int? SpeedKmh
	{
		get => (int?)GetValue(SpeedKmhProperty);
		set => SetValue(SpeedKmhProperty, value);
	}
	public string? SpeedClass
	{
		get => (string?)GetValue(SpeedClassProperty);
		set => SetValue(SpeedClassProperty, value);
	}

	private readonly Label _label;

	public SectionBreakHeader()
	{
		Padding = new Thickness(10, 6);
		StrokeThickness = 0;
		Background = (Brush?)Application.Current?.Resources["OT_AccentSoft"];
		_label = new Label
		{
			FontSize = 13,
			FontAttributes = FontAttributes.Bold,
			VerticalTextAlignment = TextAlignment.Center,
			TextColor = (Color?)Application.Current?.Resources["OT_AccentFgStrong_Light"] ?? Colors.Black,
		};
		_label.SetAppThemeColor(Label.TextColorProperty,
			(Color)Application.Current!.Resources["OT_AccentFgStrong_Light"],
			(Color)Application.Current.Resources["OT_AccentFgStrong_Dark"]);
		Content = _label;
		UpdateText();
	}

	private static void OnTextChanged(BindableObject b, object o, object n)
	{
		if (b is SectionBreakHeader h)
			h.UpdateText();
	}

	private void UpdateText()
	{
		string kmh = SpeedKmh is int v ? v.ToString() : "—";
		string klass = string.IsNullOrEmpty(SpeedClass) ? "" : $" · {SpeedClass}";
		_label.Text = $"▼ 区間切替 — 最高 {kmh}km/h{klass}";
	}
}
