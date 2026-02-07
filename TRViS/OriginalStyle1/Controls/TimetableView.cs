using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

using TRViS.Controls;
using TRViS.OriginalStyle1.Converters;
using TRViS.DTAC;
using TRViS.IO.Models;
using TRViS.ViewModels;

namespace TRViS.OriginalStyle1.Controls;

/// <summary>
/// 時刻表ビューコントロール
/// 駅情報と時刻を縦型で表示
/// </summary>
public class TimetableView : ContentView
{
	private CollectionView _timetableCollectionView = null!;
	private AppViewModel? _viewModel;

	public TimetableView()
	{
		InitializeLayout();
	}

	private void InitializeLayout()
	{
		_timetableCollectionView = new CollectionView
		{
			SelectionMode = SelectionMode.Single,
			ItemSizingStrategy = ItemSizingStrategy.MeasureFirstItem,
		};

		// ヘッダー行
		var headerGrid = CreateHeaderGrid();

		// Itemテンプレート - TimetableRowViewを使用
		var itemTemplate = new DataTemplate(() => new TimetableRowView());

		_timetableCollectionView.ItemTemplate = itemTemplate;

		// GridでレイアウトしてCollectionViewのスクロール機能を有効化
		var mainLayout = new Grid
		{
			RowDefinitions =
			[
				new RowDefinition { Height = new GridLength(TimetableConstants.HEADER_HEIGHT, GridUnitType.Absolute) },  // ヘッダー
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
			ColumnDefinitions = TimetableConstants.CreateColumnDefinitions(),
			ColumnSpacing = TimetableConstants.COLUMN_SPACING,
			Padding = new Thickness(8, 6, 8, 6),
			HeightRequest = TimetableConstants.HEADER_HEIGHT,
		};

		// ダークモード対応：背景色をテーマに応じて自動切り替え
		headerGrid.SetAppThemeColor(BackgroundColorProperty, Colors.WhiteSmoke, Colors.Black);

		// ヘッダーラベルのスタイル
		for (int i = 0; i < TimetableConstants.ColumnHeaders.Length; i++)
		{
			var headerLabel = new Label
			{
				Text = TimetableConstants.ColumnHeaders[i],
				FontSize = 14,
				FontAttributes = FontAttributes.Bold,
				HorizontalTextAlignment = TextAlignment.Center,
				VerticalTextAlignment = TextAlignment.Center,
			};
			if (i == TimetableConstants.ColumnIndex.StationName)
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
