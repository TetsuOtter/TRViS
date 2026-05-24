using TRViS.Controls;
using TRViS.Utils;

namespace TRViS.OriginalTimetable.Controls;

// 1行省略 ↔ 全文展開のセクション。タップで切替。
// TODO(next slice): max-height アニメーション (今回は単純な IsVisible 切替)。
public class NoteFold : ContentView
{
	public static readonly BindableProperty TextProperty = BindableProperty.Create(
		nameof(Text), typeof(string), typeof(NoteFold), default(string),
		propertyChanged: OnTextChanged);
	public static readonly BindableProperty IsOpenProperty = BindableProperty.Create(
		nameof(IsOpen), typeof(bool), typeof(NoteFold), false,
		propertyChanged: OnOpenChanged);
	public static readonly BindableProperty LabelProperty = BindableProperty.Create(
		nameof(Label), typeof(string), typeof(NoteFold), "記事");

	public string? Text
	{
		get => (string?)GetValue(TextProperty);
		set => SetValue(TextProperty, value);
	}
	public bool IsOpen
	{
		get => (bool)GetValue(IsOpenProperty);
		set => SetValue(IsOpenProperty, value);
	}
	public string Label
	{
		get => (string)GetValue(LabelProperty);
		set => SetValue(LabelProperty, value);
	}

	private readonly Microsoft.Maui.Controls.Label _summary;
	private readonly HtmlAutoDetectLabel _body;
	private readonly Microsoft.Maui.Controls.Label _disclosure;

	public NoteFold()
	{
		_disclosure = new Microsoft.Maui.Controls.Label
		{
			Text = MaterialIcons.ExpandMore,
			FontFamily = "MaterialIconsRegular",
			FontSize = 16,
			VerticalTextAlignment = TextAlignment.Center,
		};
		_summary = new Microsoft.Maui.Controls.Label
		{
			LineBreakMode = LineBreakMode.TailTruncation,
			FontSize = 13,
			MaxLines = 1,
			VerticalTextAlignment = TextAlignment.Center,
		};
		_summary.SetAppThemeColor(Microsoft.Maui.Controls.Label.TextColorProperty,
			(Color)Application.Current!.Resources["OT_Muted_Light"],
			(Color)Application.Current.Resources["OT_Muted_Dark"]);

		_body = new HtmlAutoDetectLabel
		{
			FontSize = 13,
			IsVisible = false,
		};
		_body.SetAppThemeColor(BackgroundColorProperty,
			(Color)Application.Current.Resources["OT_BgSoft_Light"],
			(Color)Application.Current.Resources["OT_BgSoft_Dark"]);

		var header = new Grid
		{
			ColumnDefinitions =
			{
				new ColumnDefinition(GridLength.Auto),
				new ColumnDefinition(GridLength.Star),
			},
			ColumnSpacing = 6,
			Padding = new Thickness(8, 4),
		};
		header.Add(_disclosure, 0, 0);
		header.Add(_summary, 1, 0);

		var headerTap = new TapGestureRecognizer();
		headerTap.Tapped += (_, _) => IsOpen = !IsOpen;
		header.GestureRecognizers.Add(headerTap);

		var root = new VerticalStackLayout { Spacing = 0 };
		root.Add(header);
		root.Add(_body);
		Content = root;
	}

	private static void OnTextChanged(BindableObject b, object oldV, object newV)
	{
		if (b is NoteFold f)
		{
			string? text = newV as string;
			f._summary.Text = text ?? string.Empty;
			f._body.Text = text;
		}
	}

	private static void OnOpenChanged(BindableObject b, object oldV, object newV)
	{
		if (b is NoteFold f && newV is bool isOpen)
		{
			f._body.IsVisible = isOpen;
			f._disclosure.Rotation = isOpen ? 180 : 0;
		}
	}
}
