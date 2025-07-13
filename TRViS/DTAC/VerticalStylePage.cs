using DependencyPropertyGenerator;

using Microsoft.Maui.Controls.Shapes;

using TRViS.Controls;
using TRViS.IO.Models;
using TRViS.Services;
using TRViS.ValueConverters;
using TRViS.ValueConverters.DTAC;
using TRViS.ViewModels;

namespace TRViS.DTAC;

[DependencyProperty<TrainData>("SelectedTrainData")]
[DependencyProperty<string>("AffectDate")]
public partial class VerticalStylePage : ContentView
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	public static readonly TrainNumberConverter TrainNumberConverter = new();

	ColumnDefinition MainColumnDefinition { get; } = new(new(1, GridUnitType.Star));
	ColumnDefinition DebugMapColumnDefinition { get; } = new(0);

	public static double TimetableViewActivityIndicatorBorderMaxOpacity { get; } = 0.6;

	VerticalTimetableView TimetableView { get; } = new();
	MyMap? DebugMap = null;

	DTACViewHostViewModel DTACViewHostViewModel { get; }
	TrainData? CurrentShowingTrainData { get; set; }

	private readonly Grid mainGrid;
	private readonly PageHeader pageHeaderArea;
	private readonly Grid trainInfoHeaderGrid;
	private readonly Label trainLabel;
	private readonly Line trainSeparatorLine;
	private readonly Label maxSpeedHeaderLabel;
	private readonly Line maxSpeedSeparatorLine;
	private readonly Label speedTypeHeaderLabel;
	private readonly Line speedTypeSeparatorLine;
	private readonly Label tractiveCapacityHeaderLabel;
	private readonly Grid trainInfoContentGrid;
	private readonly Label trainNumberLabel;
	private readonly Line trainNumberSeparatorLine;
	private readonly HtmlAutoDetectLabel maxSpeedLabel;
	private readonly Line maxSpeedContentSeparatorLine;
	private readonly HtmlAutoDetectLabel speedTypeLabel;
	private readonly Line speedTypeContentSeparatorLine;
	private readonly HtmlAutoDetectLabel nominalTractiveCapacityLabel;
	private readonly TrainInfo_BeforeDeparture trainInfoBeforeDepartureArea;
	private readonly Line beforeDepartureSeparatorLine;
	private readonly Grid trainBeforeRemarksArea;
	private readonly Line remarksTopSeparatorLine;
	private readonly Label isNextDayLabel;
	private readonly Line nextDayVerticalSeparatorLine;
	private readonly Border carCountBorder;
	private readonly Grid carCountGrid;
	private readonly Label carCountLabel;
	private readonly Label carCountUnitLabel;
	private readonly Line carCountVerticalSeparatorLine;
	private readonly HtmlAutoDetectLabel beginRemarksLabel;
	private readonly Label destinationLabel;
	private readonly TimetableHeader timetableHeader;
	private readonly Image backgroundAppIcon;
	private readonly ScrollView timetableAreaScrollView;
	private readonly Border timetableViewActivityIndicatorBorder;
	private readonly ActivityIndicator activityIndicator;

	public VerticalStylePage()
	{
		logger.Trace("Creating...");

		DTACViewHostViewModel = InstanceManager.DTACViewHostViewModel;
		DTACViewHostViewModel.PropertyChanged += (_, e) =>
		{
			try
			{
				switch (e.PropertyName)
				{
					case nameof(DTACViewHostViewModel.IsViewHostVisible):
					case nameof(DTACViewHostViewModel.IsVerticalViewMode):
						OnSelectedTrainDataChanged(SelectedTrainData);
						break;
				}
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalStylePage.DTACViewHostViewModel.PropertyChanged");
				Utils.ExitWithAlert(ex);
			}
		};

		mainGrid = new Grid
		{
			IgnoreSafeArea = true
		};

		pageHeaderArea = new PageHeader
		{
			IsOpen = false
		};
		pageHeaderArea.IsOpenChanged += BeforeRemarks_TrainInfo_OpenCloseChanged;
		DTACElementStyles.Instance.DefaultBGColor.Apply(pageHeaderArea, BackgroundColorProperty);
		Grid.SetRow(pageHeaderArea, 0);

		trainInfoHeaderGrid = new Grid
		{
			ColumnDefinitions = InstanceManager.DTACViewHostViewModel.ColumnDefinitionsProvider.TrainInfoHeaderColumnDefinitions,
		};
		DTACElementStyles.Instance.HeaderBackgroundColor.Apply(trainInfoHeaderGrid, BackgroundColorProperty);
		Grid.SetRow(trainInfoHeaderGrid, 1);

		trainLabel = DTACElementStyles.Instance.HeaderLabelStyle<Label>();
		trainLabel.Text = "列　車";
		Grid.SetColumn(trainLabel, 0);
		trainSeparatorLine = DTACElementStyles.Instance.VerticalSeparatorLineStyle();
		Grid.SetColumn(trainSeparatorLine, 0);

		maxSpeedHeaderLabel = DTACElementStyles.Instance.HeaderLabelStyle<Label>();
		maxSpeedHeaderLabel.Text = "最高速度\n(Ｋｍ / ｈ)";
		maxSpeedHeaderLabel.HorizontalTextAlignment = TextAlignment.Center;
		Grid.SetColumn(maxSpeedHeaderLabel, 1);
		maxSpeedSeparatorLine = DTACElementStyles.Instance.VerticalSeparatorLineStyle();
		Grid.SetColumn(maxSpeedSeparatorLine, 1);

		speedTypeHeaderLabel = DTACElementStyles.Instance.HeaderLabelStyle<Label>();
		speedTypeHeaderLabel.Text = "速度種別";
		Grid.SetColumn(speedTypeHeaderLabel, 2);
		speedTypeSeparatorLine = DTACElementStyles.Instance.VerticalSeparatorLineStyle();
		Grid.SetColumn(speedTypeSeparatorLine, 2);

		tractiveCapacityHeaderLabel = DTACElementStyles.Instance.HeaderLabelStyle<Label>();
		tractiveCapacityHeaderLabel.Text = "けん引定数";
		Grid.SetColumn(tractiveCapacityHeaderLabel, 3);

		trainInfoHeaderGrid.Children.Add(trainLabel);
		trainInfoHeaderGrid.Children.Add(trainSeparatorLine);
		trainInfoHeaderGrid.Children.Add(maxSpeedHeaderLabel);
		trainInfoHeaderGrid.Children.Add(maxSpeedSeparatorLine);
		trainInfoHeaderGrid.Children.Add(speedTypeHeaderLabel);
		trainInfoHeaderGrid.Children.Add(speedTypeSeparatorLine);
		trainInfoHeaderGrid.Children.Add(tractiveCapacityHeaderLabel);

		trainInfoContentGrid = new Grid
		{
			ColumnDefinitions = InstanceManager.DTACViewHostViewModel.ColumnDefinitionsProvider.TrainInfoHeaderColumnDefinitions,
		};
		DTACElementStyles.Instance.DefaultBGColor.Apply(trainInfoContentGrid, BackgroundColorProperty);
		Grid.SetRow(trainInfoContentGrid, 2);

		trainNumberLabel = DTACElementStyles.Instance.LabelStyle<Label>();
		trainNumberLabel.FontSize = 24;
		trainNumberLabel.FontAttributes = FontAttributes.Bold;
		DTACElementStyles.Instance.TrainNumNextDayTextColor.Apply(trainNumberLabel, Label.TextColorProperty);
		trainNumberLabel.SetBinding(Label.TextProperty, new Binding("TrainNumber", converter: TrainNumberConverter));
		Grid.SetColumn(trainNumberLabel, 0);
		trainNumberSeparatorLine = DTACElementStyles.Instance.VerticalSeparatorLineStyle();
		Grid.SetColumn(trainNumberSeparatorLine, 0);

		maxSpeedLabel = DTACElementStyles.Instance.HtmlAutoDetectLabelStyle<HtmlAutoDetectLabel>();
		maxSpeedLabel.HorizontalTextAlignment = TextAlignment.End;
		maxSpeedLabel.HorizontalOptions = LayoutOptions.End;
		Grid.SetColumn(maxSpeedLabel, 1);
		maxSpeedContentSeparatorLine = DTACElementStyles.Instance.VerticalSeparatorLineStyle();
		Grid.SetColumn(maxSpeedContentSeparatorLine, 1);

		speedTypeLabel = DTACElementStyles.Instance.HtmlAutoDetectLabelStyle<HtmlAutoDetectLabel>();
		speedTypeLabel.HorizontalTextAlignment = TextAlignment.End;
		speedTypeLabel.HorizontalOptions = LayoutOptions.End;
		Grid.SetColumn(speedTypeLabel, 2);
		speedTypeContentSeparatorLine = DTACElementStyles.Instance.VerticalSeparatorLineStyle();
		Grid.SetColumn(speedTypeContentSeparatorLine, 2);

		nominalTractiveCapacityLabel = DTACElementStyles.Instance.HtmlAutoDetectLabelStyle<HtmlAutoDetectLabel>();
		nominalTractiveCapacityLabel.HorizontalTextAlignment = TextAlignment.End;
		nominalTractiveCapacityLabel.HorizontalOptions = LayoutOptions.End;
		Grid.SetColumn(nominalTractiveCapacityLabel, 3);

		trainInfoContentGrid.Children.Add(trainNumberLabel);
		trainInfoContentGrid.Children.Add(trainNumberSeparatorLine);
		trainInfoContentGrid.Children.Add(maxSpeedLabel);
		trainInfoContentGrid.Children.Add(maxSpeedContentSeparatorLine);
		trainInfoContentGrid.Children.Add(speedTypeLabel);
		trainInfoContentGrid.Children.Add(speedTypeContentSeparatorLine);
		trainInfoContentGrid.Children.Add(nominalTractiveCapacityLabel);

		trainInfoBeforeDepartureArea = new TrainInfo_BeforeDeparture();
		Grid.SetRow(trainInfoBeforeDepartureArea, 3);

		beforeDepartureSeparatorLine = DTACElementStyles.Instance.HorizontalSeparatorLineStyle();
		beforeDepartureSeparatorLine.VerticalOptions = LayoutOptions.Start;
		Grid.SetRow(beforeDepartureSeparatorLine, 3);

		trainBeforeRemarksArea = new Grid
		{
			ColumnDefinitions = InstanceManager.DTACViewHostViewModel.ColumnDefinitionsProvider.TimetableRowColumnDefinitions,
		};
		DTACElementStyles.Instance.DefaultBGColor.Apply(trainBeforeRemarksArea, BackgroundColorProperty);
		Grid.SetRow(trainBeforeRemarksArea, 4);

		remarksTopSeparatorLine = DTACElementStyles.Instance.HorizontalSeparatorLineStyle();
		remarksTopSeparatorLine.VerticalOptions = LayoutOptions.Start;

		isNextDayLabel = DTACElementStyles.Instance.LabelStyle<Label>();
		isNextDayLabel.IsVisible = false;
		isNextDayLabel.Text = "(翌)";
		isNextDayLabel.FontSize = 24;
		isNextDayLabel.HorizontalOptions = LayoutOptions.Center;
		isNextDayLabel.VerticalOptions = LayoutOptions.Center;
		isNextDayLabel.TextColor = Color.FromArgb("#33d");
		Grid.SetColumn(isNextDayLabel, 0);

		nextDayVerticalSeparatorLine = DTACElementStyles.Instance.VerticalSeparatorLineStyle();
		Grid.SetColumn(nextDayVerticalSeparatorLine, 0);

		carCountGrid = new Grid
		{
			Margin = new Thickness(0),
			Padding = new Thickness(4),
			ColumnDefinitions =
			[
				new ColumnDefinition(GridLength.Star),
				new ColumnDefinition(GridLength.Auto)
			]
		};

		carCountLabel = DTACElementStyles.Instance.TimetableLargeNumberLabel<Label>();
		carCountLabel.VerticalOptions = LayoutOptions.Center;
		carCountLabel.Padding = new Thickness(18, 0, 8, 0);
		carCountLabel.HorizontalTextAlignment = TextAlignment.End;
		carCountLabel.SetBinding(Label.TextProperty, new Binding("CarCount"));
		Grid.SetColumn(carCountLabel, 0);

		carCountUnitLabel = DTACElementStyles.Instance.LabelStyle<Label>();
		carCountUnitLabel.FontSize = 18;
		carCountUnitLabel.VerticalOptions = LayoutOptions.End;
		carCountUnitLabel.HorizontalOptions = LayoutOptions.End;
		carCountUnitLabel.Text = "両";
		Grid.SetColumn(carCountUnitLabel, 1);

		carCountGrid.Children.Add(carCountLabel);
		carCountGrid.Children.Add(carCountUnitLabel);

		carCountBorder = new Border
		{
			Stroke = Colors.Transparent,
			Padding = new Thickness(0),
			Margin = new Thickness(16, 6),
			StrokeShape = new RoundRectangle { CornerRadius = 4 },
			Shadow = new Shadow
			{
				Brush = Colors.Black,
				Offset = new Point(2, 2),
				Radius = 2,
				Opacity = 0.2f
			},
			Content = carCountGrid
		};
		DTACElementStyles.Instance.CarCountBGColor.Apply(carCountBorder, BackgroundColorProperty);
		carCountBorder.SetBinding(Border.IsVisibleProperty, new Binding("CarCount", converter: IsOneOrMoreIntConverter.Default));
		Grid.SetColumn(carCountBorder, 1);

		carCountVerticalSeparatorLine = DTACElementStyles.Instance.VerticalSeparatorLineStyle();
		Grid.SetColumn(carCountVerticalSeparatorLine, 1);

		beginRemarksLabel = DTACElementStyles.Instance.AfterRemarksStyle<HtmlAutoDetectLabel>();
		Grid.SetColumn(beginRemarksLabel, 2);
		Grid.SetColumnSpan(beginRemarksLabel, 6);

		destinationLabel = DTACElementStyles.Instance.LabelStyle<Label>();
		destinationLabel.Margin = new Thickness(0, 8);
		destinationLabel.HorizontalOptions = LayoutOptions.Start;
		destinationLabel.VerticalOptions = LayoutOptions.Start;
		Grid.SetColumn(destinationLabel, 6);
		Grid.SetColumnSpan(destinationLabel, 2);

		trainBeforeRemarksArea.Children.Add(remarksTopSeparatorLine);
		trainBeforeRemarksArea.Children.Add(isNextDayLabel);
		trainBeforeRemarksArea.Children.Add(nextDayVerticalSeparatorLine);
		trainBeforeRemarksArea.Children.Add(carCountBorder);
		trainBeforeRemarksArea.Children.Add(carCountVerticalSeparatorLine);
		trainBeforeRemarksArea.Children.Add(beginRemarksLabel);
		trainBeforeRemarksArea.Children.Add(destinationLabel);

		timetableHeader = new TimetableHeader
		{
			FontSize_Large = 28
		};
		DTACElementStyles.Instance.HeaderBackgroundColor.Apply(timetableHeader, BackgroundColorProperty);
		Grid.SetRow(timetableHeader, 5);

		backgroundAppIcon = DTACElementStyles.Instance.BackgroundAppIconImage();
		Grid.SetRow(backgroundAppIcon, 6);

		timetableAreaScrollView = new ScrollView();
		Grid.SetRow(timetableAreaScrollView, 6);

		activityIndicator = new ActivityIndicator
		{
			IsRunning = true
		};

		timetableViewActivityIndicatorBorder = new Border
		{
			Opacity = TimetableViewActivityIndicatorBorderMaxOpacity,
			VerticalOptions = LayoutOptions.Start,
			IsVisible = false,
			HeightRequest = 50,
			WidthRequest = 50,
			Margin = new Thickness(8),
			StrokeShape = new RoundRectangle { CornerRadius = 25 },
			Shadow = new Shadow
			{
				Brush = Colors.Black,
				Offset = new Point(2, 2),
				Radius = 2,
				Opacity = 0.2f
			},
			Content = activityIndicator
		};
		Grid.SetRow(timetableViewActivityIndicatorBorder, 6);

		mainGrid.Children.Add(pageHeaderArea);
		mainGrid.Children.Add(trainInfoHeaderGrid);
		mainGrid.Children.Add(trainInfoContentGrid);
		mainGrid.Children.Add(trainInfoBeforeDepartureArea);
		mainGrid.Children.Add(beforeDepartureSeparatorLine);
		mainGrid.Children.Add(trainBeforeRemarksArea);
		mainGrid.Children.Add(timetableHeader);
		mainGrid.Children.Add(backgroundAppIcon);
		mainGrid.Children.Add(timetableAreaScrollView);
		mainGrid.Children.Add(timetableViewActivityIndicatorBorder);

		Content = mainGrid;

		InstanceManager.DTACViewHostViewModel.ColumnDefinitionsProvider.ViewWidthModeChanged += (_, e) =>
		{
			if (InstanceManager.DTACViewHostViewModel.ColumnDefinitionsProvider.IsStaNameColumnNarrow)
			{
				carCountBorder.Margin = new(8, 4);
			}
			else
			{
				carCountBorder.Margin = new(16, 6);
			}
		};

		mainGrid.RowDefinitions = InstanceManager.DTACViewHostViewModel.RowDefinitionsProvider.VerticalStylePageRowDefinitions;
		mainGrid.ColumnDefinitions = new(
			MainColumnDefinition,
			DebugMapColumnDefinition
		);
		EasterEggPageViewModel eevm = InstanceManager.EasterEggPageViewModel;
		eevm.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(EasterEggPageViewModel.ShowMapWhenLandscape))
			{
				MainThread.BeginInvokeOnMainThread(OnMayChangeDebugMapVisible);
			}
		};

		DeviceDisplay.Current.MainDisplayInfoChanged += (_, e) =>
		{
			logger.Debug("MainDisplayInfoChanged: {0}", e.DisplayInfo);
			OnMayChangeDebugMapVisible();
		};

		TimetableView.IsLocationServiceEnabledChanged += (_, e) =>
		{
			logger.Info("IsLocationServiceEnabledChanged: {0}", e.NewValue);
			pageHeaderArea.IsLocationServiceEnabled = e.NewValue;
		};

		InstanceManager.LocationService.OnGpsLocationUpdated += (_, e) =>
		{
			if (DebugMap is null || e is null)
			{
				return;
			}
			logger.Debug("OnGpsLocationUpdated: {0}", e);
			DebugMap.SetCurrentLocation(e.Latitude, e.Longitude, e.Accuracy ?? 20);
		};

		OnMayChangeDebugMapVisible();

		if (DeviceInfo.Current.Idiom == DeviceIdiom.Phone || DeviceInfo.Current.Idiom == DeviceIdiom.Unknown)
		{
			logger.Info("Device is Phone or Unknown -> make it to fill-scrollable");
			this.Content.VerticalOptions = LayoutOptions.Start;
			Content = new ScrollView()
			{
				Content = this.Content,
			};
			DTACElementStyles.Instance.DefaultBGColor.Apply(Content, BackgroundColorProperty);
		}

		TimetableView.IsBusyChanged += (s, _) =>
		{
			if (s is not VerticalTimetableView v)
				return;

			logger.Info("IsBusyChanged: {0}", v.IsBusy);

			try
			{
				if (v.IsBusy)
				{
					timetableViewActivityIndicatorBorder.IsVisible = true;
					timetableViewActivityIndicatorBorder.FadeToAsync(TimetableViewActivityIndicatorBorderMaxOpacity);
				}
				else
				{
					var fadeTask = timetableViewActivityIndicatorBorder.FadeToAsync(0);
					fadeTask.ContinueWith((_) =>
					{
						MainThread.BeginInvokeOnMainThread(() =>
						{
							logger.Debug("TimetableViewActivityIndicatorBorder.FadeToAsync(0) completed");
							timetableViewActivityIndicatorBorder.IsVisible = false;
						});
					});
				}

				// iPhoneにて、画面を回転させないとScrollViewのDesiredSizeが正常に更新されないバグに対応するため
				if (Content is ScrollView sv)
				{
					double heightRequest = InstanceManager.DTACViewHostViewModel.RowDefinitionsProvider.ContentOtherThanTimetableHeight + Math.Max(0, TimetableView.HeightRequest);
					logger.Debug("set full-scrollable-ScrollView.HeightRequest -> Max(this.HeightRequest: {0}, heightRequest: {1})", this.HeightRequest, heightRequest);
					sv.Content.HeightRequest = Math.Max(this.Height, heightRequest);
				}
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalStylePage.TimetableView.IsBusyChanged");
				Utils.ExitWithAlert(ex);
			}
		};

		TimetableView.IgnoreSafeArea = false;
		TimetableView.VerticalOptions = LayoutOptions.Start;

		pageHeaderArea.IsRunningChanged += (_, e) =>
		{
			logger.Info("IsRunningChanged: {0}", e.NewValue);
			TimetableView.IsRunStarted = e.NewValue;
		};

		TimetableView.ScrollRequested += VerticalTimetableView_ScrollRequested;

		if (DeviceInfo.Current.Idiom == DeviceIdiom.Phone || DeviceInfo.Current.Idiom == DeviceIdiom.Unknown)
		{
			logger.Info("Device is Phone or Unknown -> set ScrollView to main grid");
			Grid.SetRow(TimetableView, Grid.GetRow(timetableAreaScrollView));
			timetableAreaScrollView.IsVisible = false;
			mainGrid.Add(TimetableView);
		}
		else
		{
			logger.Info("Device is not Phone nor Unknown -> set TimetableView to TimetableAreaScrollView");
			timetableAreaScrollView.Content = TimetableView;
			timetableAreaScrollView.PropertyChanged += (_, e) =>
			{
				// Bindingに失敗するため、代わり。
				if (e.PropertyName == nameof(TimetableView.Height))
				{
					logger.Debug("TimetableView.Height: {0}", TimetableView.HeightRequest);
					TimetableView.ScrollViewHeight = timetableAreaScrollView.Height;
				}
			};
		}

		pageHeaderArea.IsLocationServiceEnabledChanged += OnIsLocationServiceEnabledChanged;
		TimetableView.IsLocationServiceEnabledChanged += OnIsLocationServiceEnabledChanged;

		TimetableView.CanUseLocationServiceChanged += (_, canUseLocationService) =>
		{
			logger.Info("CanUseLocationServiceChanged: {0}", canUseLocationService);
			pageHeaderArea.CanUseLocationService = canUseLocationService;
		};
		pageHeaderArea.CanUseLocationService = TimetableView.CanUseLocationService;

		maxSpeedLabel.CurrentAppThemeColorBindingExtension = DTACElementStyles.Instance.DefaultTextColor;
		speedTypeLabel.CurrentAppThemeColorBindingExtension = DTACElementStyles.Instance.DefaultTextColor;
		nominalTractiveCapacityLabel.CurrentAppThemeColorBindingExtension = DTACElementStyles.Instance.DefaultTextColor;
		beginRemarksLabel.CurrentAppThemeColorBindingExtension = DTACElementStyles.Instance.DefaultTextColor;

		logger.Trace("Created");
	}

	private void OnIsLocationServiceEnabledChanged(object? sender, ValueChangedEventArgs<bool> e)
	{
		logger.Info("IsLocationServiceEnabledChanged: {0}", e.NewValue);
		pageHeaderArea.IsLocationServiceEnabled = e.NewValue;
		TimetableView.IsLocationServiceEnabled = e.NewValue;
		DebugMap?.SetIsLocationServiceEnabled(e.NewValue);
	}

	partial void OnSelectedTrainDataChanged(TrainData? newValue)
	{
		if (CurrentShowingTrainData == newValue)
		{
			logger.Debug("CurrentShowingTrainData == newValue -> do nothing");
			return;
		}
		if (!DTACViewHostViewModel.IsViewHostVisible || !DTACViewHostViewModel.IsVerticalViewMode)
		{
			logger.Debug("IsViewHostVisible: {0}, IsVerticalViewMode: {1} -> lazy load",
				DTACViewHostViewModel.IsViewHostVisible,
				DTACViewHostViewModel.IsVerticalViewMode
			);
			return;
		}

		try
		{
			VerticalTimetableView_ScrollRequested(this, new(0));
			CurrentShowingTrainData = newValue;
			logger.Info("SelectedTrainDataChanged: {0}", newValue);
			BindingContext = newValue;
			TimetableView.SelectedTrainData = newValue;
			MainThread.BeginInvokeOnMainThread(() =>
			{
				DebugMap?.SetTimetableRowList(newValue?.Rows);
			});
			pageHeaderArea.IsRunning = false;
			InstanceManager.DTACMarkerViewModel.IsToggled = false;

			maxSpeedLabel.Text = ToWideConverter.Convert(newValue?.MaxSpeed);
			speedTypeLabel.Text = ToWideConverter.Convert(newValue?.SpeedType);
			nominalTractiveCapacityLabel.Text = ToWideConverter.Convert(newValue?.NominalTractiveCapacity);
			trainInfoBeforeDepartureArea.TrainInfoText = newValue?.TrainInfo ?? "";
			trainInfoBeforeDepartureArea.BeforeDepartureText = newValue?.BeforeDeparture ?? "";

			beginRemarksLabel.Text = newValue?.BeginRemarks ?? "";

			SetDestinationString(newValue?.Destination);

			int dayCount = newValue?.DayCount ?? 0;
			this.isNextDayLabel.IsVisible = dayCount > 0;
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalStylePage.OnSelectedTrainDataChanged");
			Utils.ExitWithAlert(ex);
		}
	}

	partial void OnAffectDateChanged(string? newValue)
	 => pageHeaderArea.AffectDateLabelText = newValue ?? "";

	private async void VerticalTimetableView_ScrollRequested(object? sender, VerticalTimetableView.ScrollRequestedEventArgs e)
	{
		if (DeviceInfo.Current.Idiom != DeviceIdiom.Phone && DeviceInfo.Current.Idiom != DeviceIdiom.Unknown)
		{
			logger.Debug("Device is not Phone nor Unknown -> scroll from {0} to {1}",
				timetableAreaScrollView.ScrollY,
				e.PositionY);
			await timetableAreaScrollView.ScrollToAsync(timetableAreaScrollView.ScrollX, e.PositionY, true);
		}
		else
		{
			logger.Debug("Device is Phone or Unknown -> do nothing");
		}
	}

	void BeforeRemarks_TrainInfo_OpenCloseChanged(object? sender, ValueChangedEventArgs<bool> e)
		=> InstanceManager.DTACViewHostViewModel.RowDefinitionsProvider.BeforeRemarks_TrainInfo_OpenCloseChanged(this, trainInfoBeforeDepartureArea, e);

	string? _DestinationString = null;
	void SetDestinationString(string? value)
	{
		if (_DestinationString == value)
			return;

		_DestinationString = value;
		if (string.IsNullOrEmpty(value))
		{
			destinationLabel.IsVisible = false;
			destinationLabel.Text = null;
			return;
		}

		string dstStr = value;
		switch (value.Length)
		{
			case 1:
				dstStr = $"{Utils.SPACE_CHAR}{value}{Utils.SPACE_CHAR}";
				break;
			case 2:
				dstStr = $"{value[0]}{Utils.SPACE_CHAR}{value[1]}";
				break;
		}

		destinationLabel.Text = $"（{dstStr}行）";
		destinationLabel.IsVisible = true;
	}

	private void OnMayChangeDebugMapVisible()
	{
		bool isEnabled = InstanceManager.EasterEggPageViewModel.ShowMapWhenLandscape;
		bool isLandscape = DeviceDisplay.Current.MainDisplayInfo.Orientation == DisplayOrientation.Landscape;
		bool isVisible = isEnabled && isLandscape;
		logger.Debug("isEnabled: {0}, isLandscape: {1}, isVisible: {2}", isEnabled, isLandscape, isVisible);
		if (isVisible)
		{
			if (DebugMap is not null)
			{
				return;
			}
		}
		else
		{
			if (DebugMap is not null)
			{
				DebugMap.IsEnabled = false;
				DebugMapColumnDefinition.Width = new(0);
				MainColumnDefinition.Width = new(1, GridUnitType.Star);
				mainGrid.Remove(DebugMap);
				DebugMap = null;
				logger.Debug("DebugMap removed");
			}
			return;
		}
		DebugMap = new MyMap();
		DebugMap.SetTimetableRowList(TimetableView.SelectedTrainData?.Rows);
		DebugMap.SetIsLocationServiceEnabled(pageHeaderArea.IsLocationServiceEnabled);
		double mainWidth = 768;
		MainColumnDefinition.Width = new(mainWidth);
		DebugMapColumnDefinition.Width = new(1, GridUnitType.Star);
		mainGrid.Add(DebugMap, 1, 0);
		mainGrid.SetRowSpan(DebugMap, mainGrid.RowDefinitions.Count);
	}
}
