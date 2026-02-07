using Microsoft.Maui.Controls;

using TRViS.OriginalStyle1.Controls;
using TRViS.ViewModels;

namespace TRViS.OriginalStyle1.Pages;

using TRViS;

/// <summary>
/// 時刻表ページ
/// 選択された列車の時刻表を表示するページ
/// C#コードビハインド実装（XAML禁止）
/// </summary>
public class TimetablePage : ContentPage
{
	public static readonly string NameOfThisClass = nameof(TimetablePage);

	private AppViewModel _appViewModel = null!;
	private Header _header = null!;
	private TimetableView _timetableView = null!;

	public TimetablePage()
	{
		Title = "Custom Route - Timetable";
		InitializeViewModel();
		InitializeLayout();
		Shell.SetNavBarIsVisible(this, true);

		// 戻るボタンの表示を明示的に設定
		Shell.SetBackButtonBehavior(this, new BackButtonBehavior
		{
			IsEnabled = true,
			IsVisible = true,
			TextOverride = "戻る",
		});
	}

	protected override bool OnBackButtonPressed()
	{
		// Shell のナビゲーションスタックから戻る
		MainThread.BeginInvokeOnMainThread(async () =>
		{
			await Shell.Current.GoToAsync("..");
		});
		return true;
	}

	private void InitializeViewModel()
	{
		_appViewModel = InstanceManager.AppViewModel;
		BindingContext = _appViewModel;
	}

	private void InitializeLayout()
	{
		// レスポンシブレイアウトのために親グリッドを定義
		var mainGrid = new Grid
		{
			RowDefinitions =
			[
				new RowDefinition { Height = new GridLength(60, GridUnitType.Absolute) },
				new RowDefinition { Height = GridLength.Star },
			],
			Padding = 0,
			RowSpacing = 0,
		};

		// ヘッダーコントロール
		_header = new Header();
		_header.SetViewModel(_appViewModel);
		Grid.SetRow(_header, 0);
		mainGrid.Add(_header);

		// 時刻表ビューコントロール
		_timetableView = new TimetableView();
		_timetableView.SetViewModel(_appViewModel);
		Grid.SetRow(_timetableView, 1);
		mainGrid.Add(_timetableView);

		// メインコンテンツ
		Content = mainGrid;
	}
}
