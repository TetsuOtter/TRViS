using TRViS.ViewModels;

namespace TRViS.OriginalStyle1.Controls;

/// <summary>
/// 時刻表ページのヘッダーコントロール
/// 列車情報と各種操作ボタンを表示
/// </summary>
public class Header : ContentView
{
	private Label _trainNameLabel = null!;
	private Button _locationToggleButton = null!;
	private Button _themeToggleButton = null!;
	private Button _runStartButton = null!;
	private Button _runStopButton = null!;

	private AppViewModel? _viewModel;

	public Header()
	{
		InitializeLayout();
	}

	private void InitializeLayout()
	{
		var mainGrid = new Grid
		{
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

		Grid.SetRow(_trainNameLabel, 0);
		Grid.SetColumn(_trainNameLabel, 0);
		mainGrid.Add(_trainNameLabel);

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

		Content = mainGrid;
	}

	/// <summary>
	/// ViewModelをバインド
	/// </summary>
	public void SetViewModel(AppViewModel viewModel)
	{
		_viewModel = viewModel;

		if (_viewModel != null)
		{
			_viewModel.PropertyChanged += (s, e) =>
			{
				if (e.PropertyName == nameof(AppViewModel.SelectedTrainData))
				{
					UpdateTrainInfo();
				}
				else if (e.PropertyName == nameof(AppViewModel.CurrentAppTheme))
				{
					UpdateThemeButtonState();
				}
			};

			UpdateTrainInfo();
			UpdateThemeButtonState();
		}
	}

	private void UpdateTrainInfo()
	{
		_trainNameLabel.Text = _viewModel?.SelectedTrainData?.TrainNumber ?? "Unknown";
	}

	private void UpdateThemeButtonState()
	{
		if (_viewModel != null)
		{
			_themeToggleButton.Text = _viewModel.CurrentAppTheme == AppTheme.Dark ? "☀️" : "🌙";
		}
	}

	private void OnLocationToggleClicked(object? sender, EventArgs e)
	{
		// 位置情報サービス機能は今後実装
	}

	private void OnThemeToggleClicked(object? sender, EventArgs e)
	{
		if (_viewModel != null)
		{
			var newTheme = _viewModel.CurrentAppTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
			_viewModel.CurrentAppTheme = newTheme;
		}
	}

	private void OnRunStartClicked(object? sender, EventArgs e)
	{
		// 運行開始機能は今後実装
	}

	private void OnRunStopClicked(object? sender, EventArgs e)
	{
		// 運行停止機能は今後実装
	}
}
