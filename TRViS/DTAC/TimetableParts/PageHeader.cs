using DependencyPropertyGenerator;

using TRViS.Services;

namespace TRViS.DTAC;

[DependencyProperty<bool>("IsOpen")]
[DependencyProperty<bool>("IsLocationServiceEnabled")]
[DependencyProperty<bool>("HasHorizontalTimetable")]
public partial class PageHeader : Grid
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	static readonly ColumnDefinitionCollection DefaultColumnDefinitions = new()
	{
		new ColumnDefinition(new GridLength(1, GridUnitType.Star)),

		// Fixed-width columns total: 110 + 186 + 128 + 60 = 484
		new ColumnDefinition(110),  // Horizontal timetable button
		new ColumnDefinition(186),  // Start/End run button
		new ColumnDefinition(128),  // Location service button
		new ColumnDefinition(60),   // Open/Close button
	};

	#region Affect Date Label

	readonly Label AffectDateLabel = DTACElementStyles.AffectDateLabelStyle<Label>();

	string _AffectDateLabelText = "";
	public string AffectDateLabelText
	{
		get => _AffectDateLabelText;
		set
		{
			if (_AffectDateLabelText == value)
			{
				logger.Trace("newValue: {0} (unchanged)", value);
				return;
			}

			_AffectDateLabelText = value;

			AffectDateLabel.Text = DTACElementStyles.AffectDateLabelTextPrefix + value;
			logger.Info("AffectDateLabelText: {0}", value);
		}
	}
	#endregion

	#region Start / End Run Button
	readonly StartEndRunButton StartEndRunButton = new();

	public event EventHandler<ValueChangedEventArgs<bool>>? IsRunningChanged
	{
		add => StartEndRunButton.IsCheckedChanged += value;
		remove => StartEndRunButton.IsCheckedChanged -= value;
	}

	public bool IsRunning
	{
		get => StartEndRunButton.IsChecked;
		set
		{
			// UpdateLocationServiceButtonStatus はイベントハンドラ側で行う
			StartEndRunButton.IsChecked = value;
			logger.Info("IsRunning: {0}", value);
		}
	}

	private void StartEndRunButton_IsCheckedChanged(object? sender, ValueChangedEventArgs<bool> e)
	{
		logger.Trace("newValue: {0}", e.NewValue);

		UpdateLocationServiceButtonStatus();
	}
	#endregion

	#region Location Service Button
	readonly LocationServiceButton LocationServiceButton = new();
	private bool _CanUseLocationService = false;
	public bool CanUseLocationService
	{
		get => _CanUseLocationService;
		set
		{
			if (_CanUseLocationService == value)
				return;
			_CanUseLocationService = value;
			UpdateLocationServiceButtonStatus();
			if (!value)
				LocationServiceButton.IsChecked = false;
			logger.Info("CanUseLocationService: {0}", value);
		}
	}
	void UpdateLocationServiceButtonStatus() => LocationServiceButton.IsEnabled = CanUseLocationService && IsRunning;

	public event EventHandler<ValueChangedEventArgs<bool>>? IsLocationServiceEnabledChanged
	{
		add => LocationServiceButton.IsCheckedChanged += value;
		remove => LocationServiceButton.IsCheckedChanged -= value;
	}

	partial void OnIsLocationServiceEnabledChanged(bool newValue)
	{
		logger.Info("IsLocationServiceEnabled: {0}", newValue);
		LocationServiceButton.IsChecked = newValue;
	}

	private void LocationServiceButton_IsCheckedChanged(object? sender, ValueChangedEventArgs<bool> e)
	{
		logger.Trace("newValue: {0}", e.NewValue);
		IsLocationServiceEnabled = e.NewValue;
	}
	#endregion

	#region Open / Close Button
	readonly OpenCloseButton OpenCloseButton = new();

	public event EventHandler<ValueChangedEventArgs<bool>>? IsOpenChanged
	{
		add => OpenCloseButton.IsOpenChanged += value;
		remove => OpenCloseButton.IsOpenChanged -= value;
	}

	partial void OnIsOpenChanged(bool newValue)
	{
		logger.Info("OpenCloseButton.IsOpen: {0}", newValue);
		OpenCloseButton.IsOpen = newValue;
	}

	private void OpenCloseButton_IsOpenChanged(object? sender, ValueChangedEventArgs<bool> e)
	{
		logger.Trace("newValue: {0}", e.NewValue);
		IsOpen = e.NewValue;
	}
	#endregion

	#region Horizontal Timetable Button
	readonly Button HorizontalTimetableButton = new()
	{
		Text = "横型時刻表",
		FontSize = 14,
		FontFamily = DTACElementStyles.DefaultFontFamily,
		FontAttributes = FontAttributes.Bold,
		BackgroundColor = Colors.Transparent,
		VerticalOptions = LayoutOptions.Center,
		HorizontalOptions = LayoutOptions.Center,
		Padding = new(4, 2),
		IsVisible = false
	};

	partial void OnHasHorizontalTimetableChanged(bool newValue)
	{
		logger.Info("HasHorizontalTimetable: {0}", newValue);
		HorizontalTimetableButton.IsVisible = newValue;
	}

	private async void HorizontalTimetableButton_Clicked(object? sender, EventArgs e)
	{
		logger.Info("HorizontalTimetableButton_Clicked");
		await Shell.Current.GoToAsync(HorizontalTimetablePage.NameOfThisClass);
	}
	#endregion

	public PageHeader()
	{
		logger.Trace("Creating...");

		ColumnDefinitions = DefaultColumnDefinitions;

		HorizontalTimetableButton.Clicked += HorizontalTimetableButton_Clicked;
		DTACElementStyles.DefaultTextColor.Apply(HorizontalTimetableButton, Button.TextColorProperty);

		StartEndRunButton.VerticalOptions = LayoutOptions.Center;
		StartEndRunButton.HorizontalOptions = LayoutOptions.End;
		StartEndRunButton.Margin = new(2);
		StartEndRunButton.IsCheckedChanged += StartEndRunButton_IsCheckedChanged;

		LocationServiceButton.IsEnabled = false;
		LocationServiceButton.Margin = new(4, 8);
		LocationServiceButton.IsCheckedChanged += LocationServiceButton_IsCheckedChanged;

		OpenCloseButton.TextWhenOpen = "\xe5ce";
		OpenCloseButton.TextWhenClosed = "\xe5cf";
		OpenCloseButton.IsOpenChanged += OpenCloseButton_IsOpenChanged;
		OpenCloseButton.HorizontalOptions = LayoutOptions.Center;
		OpenCloseButton.VerticalOptions = LayoutOptions.Center;

		this.Add(
			AffectDateLabel,
			column: 0
		);
		this.Add(HorizontalTimetableButton,
			column: 1
		);
		this.Add(StartEndRunButton,
			column: 2
		);
		this.Add(LocationServiceButton,
			column: 3
		);
		this.Add(
			OpenCloseButton,
			column: 4
		);

		logger.Trace("Created");
	}
}
