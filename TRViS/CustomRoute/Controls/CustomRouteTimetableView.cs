using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

using TRViS.Controls;
using TRViS.CustomRoute.Converters;
using TRViS.DTAC;
using TRViS.IO.Models;
using TRViS.ViewModels;

namespace TRViS.CustomRoute.Controls;

/// <summary>
/// CustomRoute時刻表ビューコントロール
/// 駅情報と時刻を縦型で表示
/// </summary>
public class CustomRouteTimetableView : ContentView
{
	private CollectionView _timetableCollectionView = null!;
	private AppViewModel? _viewModel;
	private double _screenWidth;

	public CustomRouteTimetableView()
	{
		InitializeLayout();
	}

	private void InitializeLayout()
	{
		const double ROW_HEIGHT = 60;  // 行の高さ統一

		_timetableCollectionView = new CollectionView
		{
			SelectionMode = SelectionMode.Single,
			ItemSizingStrategy = ItemSizingStrategy.MeasureFirstItem,
		};

		// ヘッダー行
		var headerGrid = CreateHeaderGrid();

		// Itemテンプレート
		var itemTemplate = new DataTemplate(() =>
		{
			var mainGrid = new Grid
			{
				ColumnDefinitions =
				[
					new ColumnDefinition { Width = GridLength.Star },                              // 駅名（可変幅）
					new ColumnDefinition { Width = new GridLength(120, GridUnitType.Absolute) },  // 着時刻
					new ColumnDefinition { Width = new GridLength(120, GridUnitType.Absolute) },  // 発時刻
					new ColumnDefinition { Width = new GridLength(80, GridUnitType.Absolute) },   // 番線
					new ColumnDefinition { Width = new GridLength(60, GridUnitType.Absolute) },   // 制限（上段：RunInLimit、下段：RunOutLimit）
					new ColumnDefinition { Width = new GridLength(100, GridUnitType.Absolute) },  // 記事
				],
				RowDefinitions =
				[
					new RowDefinition { Height = new GridLength(ROW_HEIGHT, GridUnitType.Absolute) },
				],
				ColumnSpacing = 4,
				Padding = new Thickness(8, 6, 8, 6),
				RowSpacing = 0,
				MinimumHeightRequest = ROW_HEIGHT,
				HeightRequest = ROW_HEIGHT,
			};

			// 駅名（HtmlAutoDetectLabel使用）
			var stationNameLabel = new HtmlAutoDetectLabel
			{
				FontSize = 18,
				FontAttributes = FontAttributes.Bold,
				VerticalOptions = LayoutOptions.Center,
				HorizontalOptions = LayoutOptions.FillAndExpand,
				CurrentAppThemeColorBindingExtension = DTACElementStyles.DefaultTextColor,
				Padding = new Thickness(0, 2),
			};
			stationNameLabel.SetBinding(HtmlAutoDetectLabel.TextProperty, new Binding(nameof(TimetableRow.StationName), BindingMode.OneWay));
			Grid.SetColumn(stationNameLabel, 0);
			mainGrid.Add(stationNameLabel);

			// 到着時刻
			var arrivalLabel = new Label
			{
				FontSize = 16,
				HorizontalTextAlignment = TextAlignment.Center,
				VerticalTextAlignment = TextAlignment.Center,
			};
			arrivalLabel.SetBinding(Label.TextProperty, new Binding(nameof(TimetableRow.ArriveTime), converter: new TimeDataConverter()));
			Grid.SetColumn(arrivalLabel, 1);
			mainGrid.Add(arrivalLabel);

			// 出発時刻
			var departureLabel = new Label
			{
				FontSize = 16,
				HorizontalTextAlignment = TextAlignment.Center,
				VerticalTextAlignment = TextAlignment.Center,
			};
			departureLabel.SetBinding(Label.TextProperty, new Binding(nameof(TimetableRow.DepartureTime), converter: new TimeDataConverter()));
			Grid.SetColumn(departureLabel, 2);
			mainGrid.Add(departureLabel);

			// 番線（HtmlAutoDetectLabel使用）
			var trackNameLabel = new HtmlAutoDetectLabel
			{
				FontSize = 14,
				HorizontalTextAlignment = TextAlignment.Center,
				VerticalOptions = LayoutOptions.Center,
				HorizontalOptions = LayoutOptions.FillAndExpand,
				CurrentAppThemeColorBindingExtension = DTACElementStyles.DefaultTextColor,
				Padding = new Thickness(0, 2),
			};
			trackNameLabel.SetBinding(HtmlAutoDetectLabel.TextProperty, new Binding(nameof(TimetableRow.TrackName), BindingMode.OneWay));
			Grid.SetColumn(trackNameLabel, 3);
			mainGrid.Add(trackNameLabel);

			// 走行入場制限（上段）と走行出場制限（下段）をまとめて表示
			var limitGrid = new Grid
			{
				RowDefinitions =
				[
					new RowDefinition { Height = GridLength.Star },  // 上段
					new RowDefinition { Height = GridLength.Star },  // 下段
				],
				RowSpacing = 0,
				Padding = 0,
				Margin = 0,
			};

			var runInLimitLabel = new Label
			{
				FontSize = 14,
				HorizontalTextAlignment = TextAlignment.Start,
				VerticalTextAlignment = TextAlignment.End,
			};
			runInLimitLabel.SetBinding(Label.TextProperty, new Binding(nameof(TimetableRow.RunInLimit), stringFormat: "{0} "));
			Grid.SetRow(runInLimitLabel, 0);
			limitGrid.Add(runInLimitLabel);

			var runOutLimitLabel = new Label
			{
				FontSize = 14,
				HorizontalTextAlignment = TextAlignment.End,
				VerticalTextAlignment = TextAlignment.Start,
			};
			runOutLimitLabel.SetBinding(Label.TextProperty, new Binding(nameof(TimetableRow.RunOutLimit), stringFormat: "/ {0}"));
			Grid.SetRow(runOutLimitLabel, 1);
			limitGrid.Add(runOutLimitLabel);

			Grid.SetColumn(limitGrid, 4);
			mainGrid.Add(limitGrid);

			// 記事（HtmlAutoDetectLabel使用）
			var remarksLabel = new HtmlAutoDetectLabel
			{
				FontSize = 14,
				HorizontalOptions = LayoutOptions.FillAndExpand,
				VerticalOptions = LayoutOptions.Center,
				CurrentAppThemeColorBindingExtension = DTACElementStyles.DefaultTextColor,
				Padding = new Thickness(0, 2),
			};
			remarksLabel.SetBinding(HtmlAutoDetectLabel.TextProperty, new Binding(nameof(TimetableRow.Remarks), BindingMode.OneWay));
			Grid.SetColumn(remarksLabel, 5);
			mainGrid.Add(remarksLabel);

			// タップ可能なボーダー
			var border = new Border
			{
				Content = mainGrid,
				Stroke = Colors.LightGray,
				StrokeThickness = 1,
				StrokeShape = new RoundRectangle { CornerRadius = 5 },
				Padding = 0,
				Margin = new Thickness(0, 1, 0, 1),
				MinimumHeightRequest = ROW_HEIGHT,
				HeightRequest = ROW_HEIGHT,
			};

			// レスポンシブ対応：画面幅が小さい場合、一部列を非表示
			border.SizeChanged += (s, e) =>
			{
				if (border.Width > 0)
				{
					_screenWidth = border.Width;
					UpdateColumnVisibility();
				}
			};

			return border;
		});

		_timetableCollectionView.ItemTemplate = itemTemplate;

		// GridでレイアウトしてCollectionViewのスクロール機能を有効化
		var mainLayout = new Grid
		{
			RowDefinitions =
			[
				new RowDefinition { Height = new GridLength(40, GridUnitType.Absolute) },  // ヘッダー
				new RowDefinition { Height = GridLength.Star },                             // 時刻表（残り全部）
			],
			ColumnDefinitions =
			[
				new ColumnDefinition { Width = GridLength.Star },
			],
			Padding = 0,
			RowSpacing = 0,
			ColumnSpacing = 0,
		};

		Grid.SetRow(headerGrid, 0);
		mainLayout.Add(headerGrid);

		Grid.SetRow(_timetableCollectionView, 1);
		mainLayout.Add(_timetableCollectionView);

		Content = mainLayout;
	}

	/// <summary>
	/// ヘッダー行を作成
	/// </summary>
	private static Grid CreateHeaderGrid()
	{
		var headerGrid = new Grid
		{
			ColumnDefinitions =
			[
				new ColumnDefinition { Width = GridLength.Star },                              // 駅名
				new ColumnDefinition { Width = new GridLength(120, GridUnitType.Absolute) },  // 着時刻
				new ColumnDefinition { Width = new GridLength(120, GridUnitType.Absolute) },  // 発時刻
				new ColumnDefinition { Width = new GridLength(60, GridUnitType.Absolute) },   // 番線
				new ColumnDefinition { Width = new GridLength(40, GridUnitType.Absolute) },   // 制限
				new ColumnDefinition { Width = new GridLength(100, GridUnitType.Absolute) },  // 記事
			],
			ColumnSpacing = 4,
			Padding = new Thickness(8, 6, 8, 6),
			HeightRequest = 40,
		};

		// ダークモード対応：背景色をテーマに応じて自動切り替え
		headerGrid.SetAppThemeColor(BackgroundColorProperty, Colors.WhiteSmoke, Colors.Black);

		// ヘッダーラベルのスタイル
		var headerLabelStyle = new Style(typeof(Label))
		{
			Setters =
			{
				new Setter { Property = Label.FontSizeProperty, Value = 14d },
				new Setter { Property = Label.FontAttributesProperty, Value = FontAttributes.Bold },
				new Setter { Property = Label.HorizontalTextAlignmentProperty, Value = TextAlignment.Center },
				new Setter { Property = Label.VerticalTextAlignmentProperty, Value = TextAlignment.Center },
			},
		};

		var headers = new[] { "駅名", "着時刻", "発時刻", "番線", "制限", "記事" };
		for (int i = 0; i < headers.Length; i++)
		{
			var headerLabel = new Label
			{
				Text = headers[i],
				Style = headerLabelStyle,
			};
			if (i == 0)
			{
				headerLabel.HorizontalTextAlignment = TextAlignment.Start;
			}

			// ダークモード対応：テキスト色をテーマに応じて自動切り替え
			headerLabel.SetAppThemeColor(Label.TextColorProperty, Colors.Black, Colors.White);

			Grid.SetColumn(headerLabel, i);
			headerGrid.Add(headerLabel);
		}

		return headerGrid;
	}

	/// <summary>
	/// 画面幅に応じて列の表示/非表示を更新
	/// </summary>
	private void UpdateColumnVisibility()
	{
		// TODO: 必要に応じて列の表示/非表示を制御
		// 現在は全列を表示するシンプル実装
	}

	/// <summary>
	/// ViewModelをバインド
	/// </summary>
	public void SetViewModel(AppViewModel viewModel)
	{
		_viewModel = viewModel;

		if (_viewModel != null)
		{
			UpdateTimetableRows();

			// SelectedTrainData変更時に時刻表を更新
			_viewModel.PropertyChanged += (s, e) =>
			{
				if (e.PropertyName == nameof(AppViewModel.SelectedTrainData))
				{
					UpdateTimetableRows();
				}
			};
		}
	}

	private void UpdateTimetableRows()
	{
		var rows = _viewModel?.SelectedTrainData?.Rows ?? [];
		_timetableCollectionView.ItemsSource = rows;
	}

	private void OnRowSelected(object selectedItem)
	{
		// 今後実装
	}
}
