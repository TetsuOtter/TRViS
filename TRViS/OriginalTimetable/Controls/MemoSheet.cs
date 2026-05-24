using TRViS.ViewModels;

namespace TRViS.OriginalTimetable.Controls;

// 下から薄く出る簡易 bottom sheet。タップ外側で閉じる + キャンセル/保存/削除。
// drag-to-dismiss は次スライス送り (TODO)。
// 親 Grid に AbsoluteLayout-like に乗せて使う想定 — IsVisible で開閉制御。
public class MemoSheet : ContentView
{
	private readonly OriginalTimetableViewModel _vm;
	private string _trainId = string.Empty;
	private string _rowId = string.Empty;
	private readonly Editor _editor;
	private readonly Label _stationLabel;

	public event EventHandler? Closed;

	public MemoSheet()
		: this(InstanceManager.OriginalTimetableViewModel) { }

	public MemoSheet(OriginalTimetableViewModel vm)
	{
		_vm = vm;
		IsVisible = false;
		InputTransparent = false;

		_editor = new Editor
		{
			Placeholder = "例: 行違列車おくれ・徐行60→40 etc.",
			MinimumHeightRequest = 88,
			AutoSize = EditorAutoSizeOption.TextChanges,
			FontSize = 16,
		};
		_editor.SetAppThemeColor(Editor.BackgroundColorProperty,
			(Color)Application.Current!.Resources["OT_Bg_Light"],
			(Color)Application.Current.Resources["OT_Bg_Dark"]);
		_editor.SetAppThemeColor(Editor.TextColorProperty,
			(Color)Application.Current!.Resources["OT_Fg_Light"],
			(Color)Application.Current.Resources["OT_Fg_Dark"]);

		_stationLabel = new Label
		{
			FontSize = 22,
			FontAttributes = FontAttributes.Bold,
		};
		_stationLabel.SetAppThemeColor(Label.TextColorProperty,
			(Color)Application.Current!.Resources["OT_Fg_Light"],
			(Color)Application.Current.Resources["OT_Fg_Dark"]);

		var saveBtn = new Button
		{
			Text = "保存",
			HorizontalOptions = LayoutOptions.Fill,
			MinimumHeightRequest = 48,
			BackgroundColor = (Color)Application.Current!.Resources["OT_Accent_Light"],
			TextColor = (Color)Application.Current.Resources["OT_AccentFg_Light"],
		};
		saveBtn.SetAppThemeColor(Button.BackgroundColorProperty,
			(Color)Application.Current.Resources["OT_Accent_Light"],
			(Color)Application.Current.Resources["OT_Accent_Dark"]);
		saveBtn.SetAppThemeColor(Button.TextColorProperty,
			(Color)Application.Current.Resources["OT_AccentFg_Light"],
			(Color)Application.Current.Resources["OT_AccentFg_Dark"]);
		saveBtn.Clicked += (_, _) => Save();

		var cancelBtn = new Button
		{
			Text = "キャンセル",
			HorizontalOptions = LayoutOptions.Fill,
			MinimumHeightRequest = 48,
		};
		cancelBtn.SetAppThemeColor(Button.BackgroundColorProperty,
			(Color)Application.Current.Resources["OT_BgSoft_Light"],
			(Color)Application.Current.Resources["OT_BgSoft_Dark"]);
		cancelBtn.SetAppThemeColor(Button.TextColorProperty,
			(Color)Application.Current.Resources["OT_Fg_Light"],
			(Color)Application.Current.Resources["OT_Fg_Dark"]);
		cancelBtn.Clicked += (_, _) => Close();

		var deleteBtn = new Button
		{
			Text = "削除",
			HorizontalOptions = LayoutOptions.Fill,
			MinimumHeightRequest = 48,
			BackgroundColor = Colors.Transparent,
		};
		deleteBtn.SetAppThemeColor(Button.TextColorProperty,
			(Color)Application.Current.Resources["OT_WarnFg_Light"],
			(Color)Application.Current.Resources["OT_WarnFg_Dark"]);
		deleteBtn.Clicked += (_, _) => { _vm.SetMemo(_trainId, _rowId, null); Close(); };

		var sheet = new VerticalStackLayout
		{
			Padding = new Thickness(18, 12, 18, 18),
			Spacing = 10,
			VerticalOptions = LayoutOptions.End,
		};
		sheet.SetAppThemeColor(BackgroundColorProperty,
			(Color)Application.Current.Resources["OT_Bg_Light"],
			(Color)Application.Current.Resources["OT_Bg_Dark"]);
		sheet.Children.Add(_stationLabel);
		sheet.Children.Add(_editor);
		sheet.Children.Add(new HorizontalStackLayout
		{
			Spacing = 8,
			HorizontalOptions = LayoutOptions.Fill,
			Children = { cancelBtn, deleteBtn, saveBtn },
		});

		var scrim = new BoxView
		{
			Color = new Color(0, 0, 0, 0.25f),
			HorizontalOptions = LayoutOptions.Fill,
			VerticalOptions = LayoutOptions.Fill,
		};
		var scrimTap = new TapGestureRecognizer();
		scrimTap.Tapped += (_, _) => Close();
		scrim.GestureRecognizers.Add(scrimTap);

		var root = new Grid();
		root.Children.Add(scrim);
		root.Children.Add(sheet);
		Content = root;
	}

	public void Open(string trainId, string rowId, string stationName)
	{
		_trainId = trainId;
		_rowId = rowId;
		_stationLabel.Text = stationName;
		_editor.Text = _vm.GetMemo(trainId, rowId);
		IsVisible = true;
	}

	public void Close()
	{
		IsVisible = false;
		Closed?.Invoke(this, EventArgs.Empty);
	}

	private void Save()
	{
		_vm.SetMemo(_trainId, _rowId, _editor.Text);
		Close();
	}
}
