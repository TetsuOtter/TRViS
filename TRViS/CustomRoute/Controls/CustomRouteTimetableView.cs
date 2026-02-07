using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using TRViS.CustomRoute.ViewModels;

namespace TRViS.CustomRoute.Controls;

/// <summary>
/// CustomRoute時刻表ビューコントロール
/// 駅情報と時刻を縦型で表示
/// </summary>
public class CustomRouteTimetableView : ContentView
{
	private CollectionView _timetableCollectionView = null!;
	private CustomRouteTimetableViewModel? _viewModel;

	public CustomRouteTimetableView()
	{
		InitializeLayout();
	}

	private void InitializeLayout()
	{
		_timetableCollectionView = new CollectionView
		{
			SelectionMode = SelectionMode.Single,
			SelectionChangedCommand = new Command<object>(OnRowSelected),
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

			// タップ可能なフレーム
			var frame = new Frame
			{
				Content = mainGrid,
				BorderColor = Colors.LightGray,
				CornerRadius = 5,
				Padding = 0,
				HasShadow = false,
				Margin = new Thickness(0, 2, 0, 2),
			};

			return frame;
		});

		_timetableCollectionView.ItemTemplate = itemTemplate;

		Content = new ScrollView
		{
			Content = _timetableCollectionView,
			Orientation = ScrollOrientation.Vertical,
		};
	}

	/// <summary>
	/// ViewModelをバインド
	/// </summary>
	public void SetViewModel(CustomRouteTimetableViewModel viewModel)
	{
		_viewModel = viewModel;

		if (_viewModel != null)
		{
			_timetableCollectionView.ItemsSource = _viewModel.TimetableRows;

			// LocationMarkerPosition変更時にスクロール
			_viewModel.PropertyChanged += (s, e) =>
			{
				if (e.PropertyName == nameof(CustomRouteTimetableViewModel.LocationMarkerPosition)
					&& _viewModel.LocationMarkerPosition >= 0)
				{
					ScrollToMarker(_viewModel.LocationMarkerPosition);
				}
			};
		}
	}

	private void OnRowSelected(object? selectedItem)
	{
		if (_viewModel == null || selectedItem is not CustomRouteTimetableRowViewModel row)
		{
			return;
		}

		if (!_viewModel.IsRunStarted)
		{
			return;
		}

		// 駅をタップして位置を設定
		_viewModel.SetLocationAtStation(row.RowIndex);
	}

	private void ScrollToMarker(int markerPosition)
	{
		if (markerPosition >= 0 && markerPosition < _viewModel?.TimetableRows.Count)
		{
			// スクロール位置を調整（マーカーが画面中央に来るように）
			_timetableCollectionView.ScrollTo(markerPosition, -1, ScrollToPosition.MakeVisible, true);
		}
	}
}
