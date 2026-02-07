using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;

using TRViS.IO.Models;

namespace TRViS.CustomRoute.Controls;

/// <summary>
/// 列車選択コントロール
/// スクロール可能なリストで列車を選択できる
/// </summary>
public class TrainSelector : ContentView
{
	private CollectionView _trainCollectionView = null!;

	/// <summary>
	/// 列車が選択されたときに発火するイベント
	/// </summary>
	public event EventHandler<TrainData>? TrainSelected;

	public TrainSelector()
	{
		InitializeLayout();
	}

	private void InitializeLayout()
	{
		// 列車リストビュー（デフォルトで縦スクロール）
		_trainCollectionView = new CollectionView
		{
			SelectionMode = SelectionMode.Single,
		};

		// SelectionChanged イベントをハンドル
		_trainCollectionView.SelectionChanged += OnCollectionViewSelectionChanged;

		// Itemテンプレートの定義（見やすいサイズ設定）
		var itemTemplate = new DataTemplate(() =>
		{
			var grid = new Grid
			{
				ColumnDefinitions =
				[
					new ColumnDefinition { Width = new GridLength(120, GridUnitType.Absolute) },
					new ColumnDefinition { Width = GridLength.Star },
					new ColumnDefinition { Width = new GridLength(140, GridUnitType.Absolute) },
				],
				ColumnSpacing = 15,
				Padding = 15,
				HeightRequest = 120,
				VerticalOptions = LayoutOptions.Center,
				RowSpacing = 5,
			};

			// 列車番号
			var trainNumberLabel = new Label
			{
				FontSize = 18,
				FontAttributes = FontAttributes.Bold,
				VerticalTextAlignment = TextAlignment.Center,
				VerticalOptions = LayoutOptions.Center,
			};
			trainNumberLabel.SetBinding(Label.TextProperty, new Binding(nameof(TrainData.TrainNumber)));
			Grid.SetColumn(trainNumberLabel, 0);
			grid.Add(trainNumberLabel);

			// 列車名
			var trainNameLabel = new Label
			{
				FontSize = 16,
				VerticalTextAlignment = TextAlignment.Center,
				VerticalOptions = LayoutOptions.Center,
			};
			trainNameLabel.SetBinding(Label.TextProperty, new Binding(nameof(TrainData.TrainNumber)));
			Grid.SetColumn(trainNameLabel, 1);
			grid.Add(trainNameLabel);

			// 路線ID
			var lineIdLabel = new Label
			{
				FontSize = 13,
				TextColor = Colors.Gray,
				VerticalTextAlignment = TextAlignment.Center,
				HorizontalTextAlignment = TextAlignment.End,
				VerticalOptions = LayoutOptions.Center,
			};
			lineIdLabel.SetBinding(Label.TextProperty, new Binding(nameof(TrainData.Id)));
			Grid.SetColumn(lineIdLabel, 2);
			grid.Add(lineIdLabel);

			// タップ時のハイライト効果
			var border = new Border
			{
				Content = grid,
				Stroke = Colors.LightGray,
				StrokeThickness = 1,
				StrokeShape = new RoundRectangle { CornerRadius = 8 },
				Padding = 0,
				Margin = new Thickness(0, 5, 0, 5),
			};

			return border;
		});

		_trainCollectionView.ItemTemplate = itemTemplate;

		Content = _trainCollectionView;
	}

	/// <summary>
	/// 列車リストを設定
	/// </summary>
	public void SetTrainList(IReadOnlyList<TrainData>? trainList)
	{
		_trainCollectionView.ItemsSource = trainList;
	}

	private void OnCollectionViewSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (e.CurrentSelection.Count == 0)
		{
			return;
		}

		if (e.CurrentSelection[0] is not TrainData trainData)
		{
			return;
		}

		TrainSelected?.Invoke(this, trainData);

		// 同じ列車の再選択を可能にするため、選択状態をクリア
		_trainCollectionView.SelectedItem = null;
	}
}
