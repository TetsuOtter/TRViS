using TR.Maui.AnchorPopover;

using TRViS.ViewModels;

namespace TRViS.OriginalTimetable.Controls;

// 4択 (None/Flag/Caution/Star) の小型ポップオーバー。MarkerButton.xaml.cs と同じ
// AnchorPopover を使う。Target に渡された View をアンカーにする。
public class MarkerPopover : ContentView
{
	private readonly OriginalTimetableViewModel _vm;
	private string _trainId = string.Empty;
	private string _rowId = string.Empty;
	private IAnchorPopover? _popover;

	public MarkerPopover()
		: this(InstanceManager.OriginalTimetableViewModel) { }

	public MarkerPopover(OriginalTimetableViewModel vm)
	{
		_vm = vm;
		BackgroundColor = (Color?)Application.Current?.Resources["OT_BgSoft_Light"] ?? Colors.White;
		this.SetAppThemeColor(
			BackgroundColorProperty,
			(Color)Application.Current!.Resources["OT_BgSoft_Light"],
			(Color)Application.Current.Resources["OT_BgSoft_Dark"]);

		WidthRequest = 232;
		MinimumHeightRequest = 220;
		Padding = 6;
		Content = BuildList();
	}

	private View BuildList()
	{
		var stack = new VerticalStackLayout { Spacing = 2 };

		foreach (var kind in new[] { MarkerKind.Flag, MarkerKind.Caution, MarkerKind.Star })
		{
			stack.Add(MakeRow(kind, label: kind switch
			{
				MarkerKind.Flag => "通過済み (Flag)",
				MarkerKind.Caution => "要注意 (Caution)",
				MarkerKind.Star => "メモあり (Star)",
				_ => kind.ToString(),
			}));
		}

		var clearBtn = new Button
		{
			Text = "クリア",
			BackgroundColor = Colors.Transparent,
			HorizontalOptions = LayoutOptions.Fill,
			Margin = new Thickness(0, 4, 0, 0),
			MinimumHeightRequest = 44,
		};
		clearBtn.SetAppThemeColor(Button.TextColorProperty,
			(Color)Application.Current!.Resources["OT_Muted_Light"],
			(Color)Application.Current.Resources["OT_Muted_Dark"]);
		clearBtn.Clicked += (_, _) => Pick(MarkerKind.None);
		stack.Add(clearBtn);

		return stack;
	}

	private View MakeRow(MarkerKind kind, string label)
	{
		var btn = new Button
		{
			Text = label,
			HorizontalOptions = LayoutOptions.Fill,
			MinimumHeightRequest = 44,
			BackgroundColor = Colors.Transparent,
			Padding = new Thickness(10, 8),
		};
		btn.SetAppThemeColor(Button.TextColorProperty,
			(Color)Application.Current!.Resources["OT_Fg_Light"],
			(Color)Application.Current.Resources["OT_Fg_Dark"]);
		btn.Clicked += (_, _) => Pick(kind);
		return btn;
	}

	private async void Pick(MarkerKind kind)
	{
		_vm.SetMarker(_trainId, _rowId, kind);
		if (_popover is not null)
			await _popover.DismissAsync();
	}

	// 呼び出し側用 API: ShowAsync(anchor, trainId, rowId)
	public async Task ShowAsync(View anchor, string trainId, string rowId)
	{
		_trainId = trainId;
		_rowId = rowId;

		_popover = AnchorPopover.Create();
		var options = new PopoverOptions
		{
			PreferredWidth = 240,
			PreferredHeight = 280,
			DismissOnTapOutside = true,
		};
		await _popover.ShowAsync(this, anchor, options);
	}
}
