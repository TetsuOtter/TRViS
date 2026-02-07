using Microsoft.Maui.Controls;

using TRViS.CustomRoute.Controls;
using TRViS.ViewModels;

namespace TRViS.CustomRoute.Pages;

using TRViS;

/// <summary>
/// CustomRoute時刻表ページ
/// 選択された列車の時刻表を表示するページ
/// C#コードビハインド実装（XAML禁止）
/// </summary>
public class CustomRouteTimetablePage : ContentPage
{
	public static readonly string NameOfThisClass = nameof(CustomRouteTimetablePage);

	private AppViewModel _appViewModel = null!;
	private CustomRouteHeader _header = null!;
	private CustomRouteTimetableView _timetableView = null!;

	public CustomRouteTimetablePage()
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
				new RowDefinition { Height = new GridLength(180, GridUnitType.Absolute) },    // ヘッダー
				new RowDefinition { Height = GridLength.Star },                                // 時刻表（残り全部）
			],
			Padding = 0,
			RowSpacing = 0,
		};

		// ヘッダーコントロール
		_header = new CustomRouteHeader();
		_header.SetViewModel(_appViewModel);
		Grid.SetRow(_header, 0);
		mainGrid.Add(_header);

		// 時刻表ビューコントロール
		_timetableView = new CustomRouteTimetableView();
		_timetableView.SetViewModel(_appViewModel);
		Grid.SetRow(_timetableView, 1);
		mainGrid.Add(_timetableView);

		// メインコンテンツ
		Content = mainGrid;
	}
}
