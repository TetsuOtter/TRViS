using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using TRViS.CustomRoute.ViewModels;

namespace TRViS.CustomRoute.Controls;

/// <summary>
/// CustomRoute時刻表ページのヘッダーコントロール
/// 列車情報と各種操作ボタンを表示
/// </summary>
public class CustomRouteHeader : ContentView
{
	private Label _trainNameLabel = null!;
	private Label _trainNumberLabel = null!;
	private Label _lineIdLabel = null!;
	private Button _locationToggleButton = null!;
	private Button _themeToggleButton = null!;
	private Button _runStartButton = null!;
	private Button _runStopButton = null!;

	private CustomRouteTimetableViewModel? _viewModel;

	public CustomRouteHeader()
	{
		InitializeLayout();
	}

	private void InitializeLayout()
	{
		var mainGrid = new Grid
		{
			RowDefinitions =
			[
				new RowDefinition { Height = GridLength.Star },
				new RowDefinition { Height = new GridLength(50, GridUnitType.Absolute) },
			],
			ColumnDefinitions =
			[
				new ColumnDefinition { Width = GridLength.Star },
				new ColumnDefinition { Width = new GridLength(50, GridUnitType.Absolute) },
				new ColumnDefinition { Width = new GridLength(50, GridUnitType.Absolute) },
				new ColumnDefinition { Width = new GridLength(50, GridUnitType.Absolute) },
				new ColumnDefinition { Width = new GridLength(50, GridUnitType.Absolute) },
			],
			Padding = 10,
			ColumnSpacing = 5,
			RowSpacing = 5,
		};

		// 列車情報エリア
		_trainNameLabel = new Label
		{
			Text = "Train Name",
			FontSize = 20,
			FontAttributes = FontAttributes.Bold,
			VerticalTextAlignment = TextAlignment.Center,
		};

		_trainNumberLabel = new Label
		{
			Text = "Number",
			FontSize = 14,
			VerticalTextAlignment = TextAlignment.Center,
		};

		_lineIdLabel = new Label
		{
			Text = "Line",
			FontSize = 12,
			VerticalTextAlignment = TextAlignment.Center,
		};

		var trainInfoStack = new VerticalStackLayout
		{
			Spacing = 2,
			Children = { _trainNameLabel, _trainNumberLabel, _lineIdLabel }
		};

		Grid.SetRow(trainInfoStack, 0);
		Grid.SetColumn(trainInfoStack, 0);
		mainGrid.Add(trainInfoStack);

		// ボタンエリア
		_locationToggleButton = new Button
		{
			Text = "📍",
			FontSize = 20,
			Padding = 5,
		};
		_locationToggleButton.Clicked += OnLocationToggleClicked;
		Grid.SetRow(_locationToggleButton, 0);
		Grid.SetColumn(_locationToggleButton, 1);
		mainGrid.Add(_locationToggleButton);

		_themeToggleButton = new Button
		{
			Text = "🌙",
			FontSize = 20,
			Padding = 5,
		};
		_themeToggleButton.Clicked += OnThemeToggleClicked;
		Grid.SetRow(_themeToggleButton, 0);
		Grid.SetColumn(_themeToggleButton, 2);
		mainGrid.Add(_themeToggleButton);

		// 運行制御ボタン
		_runStartButton = new Button
		{
			Text = "▶",
			FontSize = 20,
			Padding = 5,
		};
		_runStartButton.Clicked += OnRunStartClicked;
		Grid.SetRow(_runStartButton, 0);
		Grid.SetColumn(_runStartButton, 3);
		mainGrid.Add(_runStartButton);

		_runStopButton = new Button
		{
			Text = "⏹",
			FontSize = 20,
			Padding = 5,
		};
		_runStopButton.Clicked += OnRunStopClicked;
		Grid.SetRow(_runStopButton, 0);
		Grid.SetColumn(_runStopButton, 4);
		mainGrid.Add(_runStopButton);

		// 下部の列車選択エリア (スペース確保)
		var selectionLabel = new Label
		{
			Text = "Train Selection Area",
			FontSize = 12,
			VerticalTextAlignment = TextAlignment.Center,
			HorizontalOptions = LayoutOptions.Center,
		};
		Grid.SetRow(selectionLabel, 1);
		Grid.SetColumnSpan(selectionLabel, 5);
		mainGrid.Add(selectionLabel);

		Content = mainGrid;
	}

	/// <summary>
	/// ViewModelをバインド
	/// </summary>
	public void SetViewModel(CustomRouteTimetableViewModel viewModel)
	{
		_viewModel = viewModel;

		if (_viewModel != null)
		{
			_viewModel.PropertyChanged += (s, e) =>
			{
				if (e.PropertyName == nameof(CustomRouteTimetableViewModel.SelectedTrainInfo))
				{
					UpdateTrainInfo();
				}
				else if (e.PropertyName == nameof(CustomRouteTimetableViewModel.IsLocationServiceEnabled))
				{
					UpdateLocationButtonState();
				}
				else if (e.PropertyName == nameof(CustomRouteTimetableViewModel.CurrentAppTheme))
				{
					UpdateThemeButtonState();
				}
				else if (e.PropertyName == nameof(CustomRouteTimetableViewModel.IsRunStarted))
				{
					UpdateRunButtonState();
				}
			};

			UpdateTrainInfo();
			UpdateLocationButtonState();
			UpdateThemeButtonState();
			UpdateRunButtonState();
		}
	}

	private void UpdateTrainInfo()
	{
		if (_viewModel?.SelectedTrainInfo != null)
		{
			_trainNameLabel.Text = _viewModel.SelectedTrainInfo.TrainName ?? "Unknown";
			_trainNumberLabel.Text = _viewModel.SelectedTrainInfo.TrainNumber ?? "-";
			_lineIdLabel.Text = _viewModel.SelectedTrainInfo.LineId ?? "-";
		}
	}

	private void UpdateLocationButtonState()
	{
		if (_viewModel != null)
		{
			_locationToggleButton.BackgroundColor = _viewModel.IsLocationServiceEnabled
				? Colors.Green
				: Colors.Gray;
		}
	}

	private void UpdateThemeButtonState()
	{
		if (_viewModel != null)
		{
			_themeToggleButton.Text = _viewModel.CurrentAppTheme == AppTheme.Dark ? "☀️" : "🌙";
		}
	}

	private void UpdateRunButtonState()
	{
		if (_viewModel != null)
		{
			_runStartButton.IsEnabled = !_viewModel.IsRunStarted;
			_runStopButton.IsEnabled = _viewModel.IsRunStarted;
		}
	}

	private void OnLocationToggleClicked(object? sender, EventArgs e)
	{
		if (_viewModel != null)
		{
			_viewModel.IsLocationServiceEnabled = !_viewModel.IsLocationServiceEnabled;
		}
	}

	private void OnThemeToggleClicked(object? sender, EventArgs e)
	{
		if (_viewModel != null)
		{
			var newTheme = _viewModel.CurrentAppTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
			_viewModel.ChangeTheme(newTheme);
		}
	}

	private void OnRunStartClicked(object? sender, EventArgs e)
	{
		if (_viewModel != null)
		{
			_viewModel.StartRun();
		}
	}

	private void OnRunStopClicked(object? sender, EventArgs e)
	{
		if (_viewModel != null)
		{
			_viewModel.StopRun();
		}
	}
}
