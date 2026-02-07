using Microsoft.Maui.Controls;
using TRViS.CustomRoute.ViewModels;

namespace TRViS.CustomRoute.Controls;

/// <summary>
/// 列車選択コントロール
/// スクロール可能なリストで列車を選択できる
/// </summary>
public class TrainSelector : ContentView
{
	private CollectionView _trainCollectionView = null!;
	private CustomRouteTimetableViewModel? _viewModel;

	public TrainSelector()
	{
		InitializeLayout();
	}

	private void InitializeLayout()
	{
		// 列車リストビュー
		_trainCollectionView = new CollectionView
		{
			SelectionMode = SelectionMode.Single,
			SelectionChangedCommand = new Command<object>(OnTrainSelected),
			SelectionChangedCommandParameter = new object(),
		};

		// Itemテンプレートの定義
		var itemTemplate = new DataTemplate(() =>
		{
			var grid = new Grid
			{
				ColumnDefinitions =
				[
					new ColumnDefinition { Width = new GridLength(80, GridUnitType.Absolute) },
					new ColumnDefinition { Width = GridLength.Star },
					new ColumnDefinition { Width = new GridLength(100, GridUnitType.Absolute) },
				],
				ColumnSpacing = 10,
				Padding = 10,
			};

			// 列車番号
			var trainNumberLabel = new Label
			{
				FontSize = 14,
				FontAttributes = FontAttributes.Bold,
				VerticalTextAlignment = TextAlignment.Center,
			};
			trainNumberLabel.SetBinding(Label.TextProperty, "TrainNumber");
			Grid.SetColumn(trainNumberLabel, 0);
			grid.Add(trainNumberLabel);

			// 列車名
			var trainNameLabel = new Label
			{
				FontSize = 12,
				VerticalTextAlignment = TextAlignment.Center,
			};
			trainNameLabel.SetBinding(Label.TextProperty, "TrainName");
			Grid.SetColumn(trainNameLabel, 1);
			grid.Add(trainNameLabel);

			// 路線ID
			var lineIdLabel = new Label
			{
				FontSize = 11,
				TextColor = Colors.Gray,
				VerticalTextAlignment = TextAlignment.Center,
				HorizontalTextAlignment = TextAlignment.End,
			};
			lineIdLabel.SetBinding(Label.TextProperty, "LineId");
			Grid.SetColumn(lineIdLabel, 2);
			grid.Add(lineIdLabel);

			// タップ時のハイライト
			var frame = new Frame
			{
				Content = grid,
				BorderColor = Colors.Transparent,
				CornerRadius = 5,
				Padding = 0,
				HasShadow = false,
			};

			return frame;
		});

		_trainCollectionView.ItemTemplate = itemTemplate;

		Content = _trainCollectionView;
	}

	/// <summary>
	/// ViewModelをバインド
	/// </summary>
	public void SetViewModel(CustomRouteTimetableViewModel viewModel)
	{
		_viewModel = viewModel;

		if (_viewModel != null)
		{
			_trainCollectionView.ItemsSource = _viewModel.TrainList;

			// 選択変更時にViewModelを更新
			_viewModel.PropertyChanged += (s, e) =>
			{
				if (e.PropertyName == nameof(CustomRouteTimetableViewModel.SelectedTrainIndex))
				{
					if (_viewModel.SelectedTrainIndex >= 0 && _viewModel.SelectedTrainIndex < _viewModel.TrainList.Count)
					{
						_trainCollectionView.SelectedItem = _viewModel.TrainList[_viewModel.SelectedTrainIndex];
					}
				}
			};
		}
	}

	private void OnTrainSelected(object selectedItem)
	{
		if (_viewModel == null || selectedItem is not CustomRouteTrainListItemViewModel train)
		{
			return;
		}

		_viewModel.SelectTrain(train.Index);
	}
}
