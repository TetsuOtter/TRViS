using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using TRViS.ViewModels;

namespace TRViS.CustomRoute.Controls;

/// <summary>
/// CustomRoute時刻表ビューコントロール
/// 駅情報と時刻を縦型で表示
/// </summary>
public class CustomRouteTimetableView : ContentView
{
	private CollectionView _timetableCollectionView = null!;
	private Label _debugLabel = null!;
	private AppViewModel? _viewModel;

	public CustomRouteTimetableView()
	{
		InitializeLayout();
	}

	private void InitializeLayout()
	{
		_timetableCollectionView = new CollectionView
		{
			SelectionMode = SelectionMode.Single,
		};

		// Itemテンプレート
		var itemTemplate = new DataTemplate(() =>
		{
			var mainGrid = new Grid
			{
				ColumnDefinitions =
				[
					new ColumnDefinition { Width = new GridLength(60, GridUnitType.Absolute) },  // 到着時刻
					new ColumnDefinition { Width = new GridLength(60, GridUnitType.Absolute) },  // 出発時刻
					new ColumnDefinition { Width = GridLength.Star },                             // 駅名
					new ColumnDefinition { Width = new GridLength(40, GridUnitType.Absolute) },  // マーカー
				],
				ColumnSpacing = 5,
				Padding = 10,
				RowSpacing = 2,
			};

			// 到着時刻
			var arrivalLabel = new Label
			{
				FontSize = 12,
				HorizontalTextAlignment = TextAlignment.Center,
				VerticalTextAlignment = TextAlignment.Center,
			};
			arrivalLabel.SetBinding(Label.TextProperty, "ArrivalTime", stringFormat: "{0}");
			Grid.SetColumn(arrivalLabel, 0);
			mainGrid.Add(arrivalLabel);

			// 出発時刻
			var departureLabel = new Label
			{
				FontSize = 12,
				HorizontalTextAlignment = TextAlignment.Center,
				VerticalTextAlignment = TextAlignment.Center,
			};
			departureLabel.SetBinding(Label.TextProperty, "DepartureTime", stringFormat: "{0}");
			Grid.SetColumn(departureLabel, 1);
			mainGrid.Add(departureLabel);

			// 駅名
			var stationNameLabel = new Label
			{
				FontSize = 14,
				FontAttributes = FontAttributes.Bold,
				VerticalTextAlignment = TextAlignment.Center,
			};
			stationNameLabel.SetBinding(Label.TextProperty, "StationName");
			Grid.SetColumn(stationNameLabel, 2);
			mainGrid.Add(stationNameLabel);

			// 現在位置マーカー
			var markerLabel = new Label
			{
				Text = "●",
				FontSize = 16,
				TextColor = Colors.Red,
				HorizontalTextAlignment = TextAlignment.Center,
				VerticalTextAlignment = TextAlignment.Center,
				IsVisible = false,
			};
			markerLabel.SetBinding(Label.IsVisibleProperty, "IsLocationMarkerOnThisRow");
			Grid.SetColumn(markerLabel, 3);
			mainGrid.Add(markerLabel);

			// タップ可能なボーダー
			var border = new Border
			{
				Content = mainGrid,
				Stroke = Colors.LightGray,
				StrokeThickness = 1,
				StrokeShape = new RoundRectangle { CornerRadius = 5 },
				Padding = 0,
				Margin = new Thickness(0, 2, 0, 2),
			};

			return border;
		});

		_timetableCollectionView.ItemTemplate = itemTemplate;

		// デバッグ用：TimetableRows が入力されているか確認
		_debugLabel = new Label
		{
			Text = "Rows: 0",
			FontSize = 16,
			TextColor = Colors.Red,
			Padding = 10,
		};

		var mainLayout = new VerticalStackLayout
		{
			Children =
			{
				_debugLabel,
				new ScrollView
				{
					Content = _timetableCollectionView,
					Orientation = ScrollOrientation.Vertical,
					BackgroundColor = Colors.LightGray,
				},
			},
		};

		Content = mainLayout;
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
		_debugLabel.Text = $"Rows: {rows.Count}";
	}

	private void OnRowSelected(object selectedItem)
	{
		// 今後実装
	}
}
