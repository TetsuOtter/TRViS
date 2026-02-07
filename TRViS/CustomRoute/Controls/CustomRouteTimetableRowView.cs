using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

using TRViS.Controls;
using TRViS.CustomRoute.Converters;
using TRViS.DTAC;
using TRViS.IO.Models;

namespace TRViS.CustomRoute.Controls;

/// <summary>
/// CustomRoute時刻表の行コンポーネント
/// TimetableRowデータを表示するための再利用可能なビュー
/// </summary>
public class CustomRouteTimetableRowView : Border
{
	private TimetableRow? _currentRow;

	// ラベルへの参照
	private HtmlAutoDetectLabel _stationNameLabel = null!;
	private Label _arrivalLabel = null!;
	private Label _departureLabel = null!;
	private HtmlAutoDetectLabel _trackNameLabel = null!;
	private Label _runInLimitLabel = null!;
	private Label _runOutLimitLabel = null!;
	private HtmlAutoDetectLabel _remarksLabel = null!;

	public CustomRouteTimetableRowView()
	{
		InitializeLayout();
		this.BindingContextChanged += OnBindingContextChanged;
	}

	private void OnBindingContextChanged(object? sender, EventArgs e)
	{
		if (this.BindingContext is TimetableRow row)
		{
			SetRow(row);
		}
	}

	public void SetRow(TimetableRow row)
	{
		_currentRow = row;
		UpdateAllValues();
	}

	private void UpdateAllValues()
	{
		if (_currentRow is null)
			return;

		_stationNameLabel.Text = _currentRow.StationName;
		_arrivalLabel.Text = TimeDataConverter.Convert(_currentRow.ArriveTime);
		_departureLabel.Text = TimeDataConverter.Convert(_currentRow.DepartureTime);
		_trackNameLabel.Text = _currentRow.TrackName;
		_runInLimitLabel.Text = $"{_currentRow.RunInLimit} ";
		_runOutLimitLabel.Text = $"/ {_currentRow.RunOutLimit}";
		_runOutLimitLabel.IsVisible = _currentRow.RunOutLimit is not null;
		_remarksLabel.Text = _currentRow.Remarks;
	}

	private void InitializeLayout()
	{
		var mainGrid = new Grid
		{
			ColumnDefinitions = CustomRouteTimetableConstants.CreateColumnDefinitions(),
			ColumnSpacing = CustomRouteTimetableConstants.COLUMN_SPACING,
			Padding = new Thickness(8, 6, 8, 6),
			RowSpacing = 0,
			MinimumHeightRequest = CustomRouteTimetableConstants.ROW_HEIGHT,
			HeightRequest = CustomRouteTimetableConstants.ROW_HEIGHT,
		};

		// 駅名（HtmlAutoDetectLabel使用）
		_stationNameLabel = new HtmlAutoDetectLabel
		{
			FontSize = 18,
			FontAttributes = FontAttributes.Bold,
			VerticalOptions = LayoutOptions.Center,
			Padding = new Thickness(0, 2),
		};
		Grid.SetColumn(_stationNameLabel, CustomRouteTimetableConstants.ColumnIndex.StationName);
		mainGrid.Add(_stationNameLabel);

		// 到着時刻
		_arrivalLabel = new Label
		{
			FontSize = 16,
			HorizontalTextAlignment = TextAlignment.Center,
			VerticalTextAlignment = TextAlignment.Center,
		};
		Grid.SetColumn(_arrivalLabel, CustomRouteTimetableConstants.ColumnIndex.ArrivalTime);
		mainGrid.Add(_arrivalLabel);

		// 出発時刻
		_departureLabel = new Label
		{
			FontSize = 16,
			HorizontalTextAlignment = TextAlignment.Center,
			VerticalTextAlignment = TextAlignment.Center,
		};
		Grid.SetColumn(_departureLabel, CustomRouteTimetableConstants.ColumnIndex.DepartureTime);
		mainGrid.Add(_departureLabel);

		// 番線（HtmlAutoDetectLabel使用）
		_trackNameLabel = new HtmlAutoDetectLabel
		{
			FontSize = 14,
			HorizontalTextAlignment = TextAlignment.Center,
			VerticalOptions = LayoutOptions.Center,
			Padding = new Thickness(0, 2),
		};
		Grid.SetColumn(_trackNameLabel, CustomRouteTimetableConstants.ColumnIndex.TrackName);
		mainGrid.Add(_trackNameLabel);

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

		_runInLimitLabel = new Label
		{
			FontSize = 14,
			HorizontalTextAlignment = TextAlignment.Start,
			VerticalTextAlignment = TextAlignment.End,
		};
		Grid.SetRow(_runInLimitLabel, 0);
		limitGrid.Add(_runInLimitLabel);

		_runOutLimitLabel = new Label
		{
			FontSize = 14,
			HorizontalTextAlignment = TextAlignment.End,
			VerticalTextAlignment = TextAlignment.Start,
		};
		Grid.SetRow(_runOutLimitLabel, 1);
		limitGrid.Add(_runOutLimitLabel);

		Grid.SetColumn(limitGrid, CustomRouteTimetableConstants.ColumnIndex.Limit);
		mainGrid.Add(limitGrid);

		// 記事（HtmlAutoDetectLabel使用）
		_remarksLabel = new HtmlAutoDetectLabel
		{
			FontSize = 14,
			VerticalOptions = LayoutOptions.Center,
			Padding = new Thickness(0, 2),
		};
		Grid.SetColumn(_remarksLabel, CustomRouteTimetableConstants.ColumnIndex.Remarks);
		mainGrid.Add(_remarksLabel);

		// Border のプロパティを設定
		Content = mainGrid;
		Stroke = Colors.LightGray;
		StrokeThickness = 1;
		StrokeShape = new RoundRectangle { CornerRadius = 5 };
		Padding = 0;
		Margin = new Thickness(0, 1, 0, 1);
		MinimumHeightRequest = CustomRouteTimetableConstants.ROW_HEIGHT;
		HeightRequest = CustomRouteTimetableConstants.ROW_HEIGHT;
	}
}

