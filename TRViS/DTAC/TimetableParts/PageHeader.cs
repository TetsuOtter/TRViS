using DependencyPropertyGenerator;

using TRViS.Services;
using TRViS.Utils;

namespace TRViS.DTAC;

[DependencyProperty<bool>("IsOpen")]
[DependencyProperty<bool>("HasHorizontalTimetable")]
public partial class PageHeader : Grid
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	static readonly ColumnDefinitionCollection DefaultColumnDefinitions =
	[
		new ColumnDefinition(new GridLength(1, GridUnitType.Star)),

		new ColumnDefinition(110),
		new ColumnDefinition(176),
		new ColumnDefinition(134),
		new ColumnDefinition(60),
	];

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

	public Action? StartButtonTappedCallback { get; set; }

	public bool IsRunning
	{
		get => StartEndRunButton.IsChecked;
		set
		{
			StartEndRunButton.IsChecked = value;
			UpdateLocationServiceButtonStatus();
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

	public Action? LocationServiceButtonTappedCallback { get; set; }

	private bool _isLocationServiceEnabled = false;
	public bool IsLocationServiceEnabled
	{
		get => _isLocationServiceEnabled;
		set
		{
			if (_isLocationServiceEnabled == value)
				return;
			_isLocationServiceEnabled = value;
			LocationServiceButton.IsChecked = value;
			logger.Info("IsLocationServiceEnabled: {0}", value);
		}
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
	readonly Border HorizontalTimetableButtonBorder;
	readonly Label HorizontalTimetableButtonLabel;

	static Border CreateHorizontalTimetableButton(out Label label)
	{
		label = new Label
		{
			Text = "横型時刻表",
			FontSize = 16,
			FontFamily = DTACElementStyles.DefaultFontFamily,
			FontAttributes = FontAttributes.Bold,
			TextColor = DTACElementStyles.StartEndRunButtonTextColor.Default,
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.Center,
			Margin = new(8, 4)
		};
		DTACElementStyles.StartEndRunButtonTextColor.Apply(label, Label.TextColorProperty);

		var border = new Border
		{
			Margin = new(2),
			Padding = new(4),
			Stroke = Colors.Transparent,
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.Center,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
			BackgroundColor = Colors.White,
			IsVisible = false,
			Content = label,
			Shadow = new Shadow
			{
				Brush = Colors.Black,
				Offset = new(3, 3),
				Radius = 3,
				Opacity = 0.2f
			}
		};

		border.GestureRecognizers.Add(new TapGestureRecognizer());

		return border;
	}

	partial void OnHasHorizontalTimetableChanged(bool newValue)
	{
		logger.Info("HasHorizontalTimetable: {0}", newValue);
		HorizontalTimetableButtonBorder.IsVisible = newValue;
	}

	private async void HorizontalTimetableButton_Tapped(object? sender, TappedEventArgs e)
	{
		logger.Info("HorizontalTimetableButton_Tapped");
		await Shell.Current.GoToAsync(HorizontalTimetablePage.NameOfThisClass, true);
	}
	#endregion

	public PageHeader()
	{
		logger.Trace("Creating...");

		ColumnDefinitions = DefaultColumnDefinitions;

		HorizontalTimetableButtonBorder = CreateHorizontalTimetableButton(out HorizontalTimetableButtonLabel);
		if (HorizontalTimetableButtonBorder.GestureRecognizers.Count > 0
			&& HorizontalTimetableButtonBorder.GestureRecognizers[0] is TapGestureRecognizer tapGesture)
		{
			tapGesture.Tapped += HorizontalTimetableButton_Tapped;
		}

		StartEndRunButton.Margin = new(2, 8);
		StartEndRunButton.IsCheckedChanged += StartEndRunButton_IsCheckedChanged;
		StartEndRunButton.Tapped += (_, _) =>
		{
			logger.Info("StartEndRunButton tapped");
			StartButtonTappedCallback?.Invoke();
		};

		LocationServiceButton.IsEnabled = false;
		LocationServiceButton.Margin = new(4, 8, 4, 10);
		LocationServiceButton.Tapped += (_, _) =>
		{
			logger.Info("LocationServiceButton tapped");
			LocationServiceButtonTappedCallback?.Invoke();
		};

		OpenCloseButton.TextWhenOpen = "\xe5ce";
		OpenCloseButton.TextWhenClosed = "\xe5cf";
		OpenCloseButton.IsOpenChanged += OpenCloseButton_IsOpenChanged;
		OpenCloseButton.HorizontalOptions = LayoutOptions.Center;
		OpenCloseButton.VerticalOptions = LayoutOptions.Center;

		this.Add(
			AffectDateLabel,
			column: 0
		);
		this.Add(HorizontalTimetableButtonBorder,
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
