using Microsoft.Maui.Controls;
using TRViS.CustomRoute.ViewModels;
using TRViS.CustomRoute.Controls;
using TRViS.IO.Models;

namespace TRViS.CustomRoute.Pages;

/// <summary>
/// CustomRoute時刻表ページ
/// 独自路線の時刻表を表示するメインページ
/// C#コードビハインド実装（XAML禁止）
/// </summary>
public class CustomRouteTimetablePage : ContentPage
{
	private CustomRouteTimetableViewModel _viewModel = null!;
	private CustomRouteHeader _header = null!;
	private TrainSelector _trainSelector = null!;
	private CustomRouteTimetableView _timetableView = null!;

	public CustomRouteTimetablePage()
	{
		Title = "Custom Route Timetable";
		InitializeViewModel();
		InitializeLayout();
	}

	private void InitializeViewModel()
	{
		_viewModel = new CustomRouteTimetableViewModel();
		BindingContext = _viewModel;
	}

	private void InitializeLayout()
	{
		// レスポンシブレイアウトのために親グリッドを定義
		var mainGrid = new Grid
		{
			RowDefinitions =
			[
				new RowDefinition { Height = new GridLength(180, GridUnitType.Absolute) },    // ヘッダー
				new RowDefinition { Height = new GridLength(80, GridUnitType.Absolute) },     // 列車選択
				new RowDefinition { Height = GridLength.Star },                                // 時刻表（残り全部）
			],
			Padding = 0,
			RowSpacing = 0,
		};

		// ヘッダーコントロール
		_header = new CustomRouteHeader();
		_header.SetViewModel(_viewModel);
		Grid.SetRow(_header, 0);
		mainGrid.Add(_header);

		// 列車選択コントロール
		_trainSelector = new TrainSelector();
		_trainSelector.SetViewModel(_viewModel);
		var trainSelectorFrame = new Frame
		{
			Content = _trainSelector,
			BorderColor = Colors.LightGray,
			CornerRadius = 0,
			Padding = 5,
			Margin = 0,
		};
		Grid.SetRow(trainSelectorFrame, 1);
		mainGrid.Add(trainSelectorFrame);

		// 時刻表ビューコントロール
		_timetableView = new CustomRouteTimetableView();
		_timetableView.SetViewModel(_viewModel);
		Grid.SetRow(_timetableView, 2);
		mainGrid.Add(_timetableView);

		// メインコンテンツ
		Content = mainGrid;

		// ページロード時に初期データを読み込む
		Loaded += OnPageLoaded;
	}

	private void OnPageLoaded(object? sender, EventArgs e)
	{
		// ここで実際の列車データを読み込む
		// TODO: アプリケーションの実装に応じて、データソースから列車データを取得
		// _viewModel.SetTrains(trainData);
	}

	/// <summary>
	/// 外部から列車データを設定するメソッド
	/// </summary>
	public void SetTrainData(IReadOnlyList<TrainData> trains)
	{
		_viewModel.SetTrains(trains);
	}
}
