using DependencyPropertyGenerator;

using TRViS.Controls;
using TRViS.IO.Models;
using TRViS.Services;
using TRViS.ValueConverters;
using TRViS.ViewModels;

namespace TRViS.DTAC;

[DependencyProperty<TrainData>("SelectedTrainData")]
[DependencyProperty<string>("AffectDate")]
public partial class VerticalStylePage : ContentView
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	ColumnDefinition MainColumnDefinition { get; } = new(new(1, GridUnitType.Star));
	ColumnDefinition DebugMapColumnDefinition { get; } = new(0);

	public static double TimetableViewActivityIndicatorBorderMaxOpacity { get; } = 0.6;

	VerticalTimetableView TimetableView { get; } = new();
	MyMap? DebugMap = null;

	DTACViewHostViewModel DTACViewHostViewModel { get; }
	TrainData? CurrentShowingTrainData { get; set; }

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

		InitializeComponent();

		DTACElementStyles.SetTimetableColumnWidthCollection(TrainBeforeRemarksArea);

		MainGrid.RowDefinitions = InstanceManager.DTACViewHostViewModel.RowDefinitionsProvider.VerticalStylePageRowDefinitions;
		MainGrid.ColumnDefinitions = new(
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
			PageHeaderArea.IsLocationServiceEnabled = e.NewValue;
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
			DTACElementStyles.DefaultBGColor.Apply(Content, BackgroundColorProperty);
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
					TimetableViewActivityIndicatorBorder.IsVisible = true;
					TimetableViewActivityIndicatorBorder.FadeTo(TimetableViewActivityIndicatorBorderMaxOpacity);
				}
				else
					TimetableViewActivityIndicatorBorder.FadeTo(0).ContinueWith((_) =>
					{
						logger.Debug("TimetableViewActivityIndicatorBorder.FadeTo(0) completed");
						TimetableViewActivityIndicatorBorder.IsVisible = false;
					});

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

		PageHeaderArea.IsRunningChanged += (_, e) =>
		{
			logger.Info("IsRunningChanged: {0}", e.NewValue);
			TimetableView.IsRunStarted = e.NewValue;
		};

		TimetableView.ScrollRequested += VerticalTimetableView_ScrollRequested;

		if (DeviceInfo.Current.Idiom == DeviceIdiom.Phone || DeviceInfo.Current.Idiom == DeviceIdiom.Unknown)
		{
			logger.Info("Device is Phone or Unknown -> set ScrollView to main grid");
			Grid.SetRow(TimetableView, Grid.GetRow(TimetableAreaScrollView));
			TimetableAreaScrollView.IsVisible = false;
			MainGrid.Add(TimetableView);
		}
		else
		{
			logger.Info("Device is not Phone nor Unknown -> set TimetableView to TimetableAreaScrollView");
			TimetableAreaScrollView.Content = TimetableView;
			TimetableAreaScrollView.PropertyChanged += (_, e) =>
			{
				// Bindingに失敗するため、代わり。
				if (e.PropertyName == nameof(TimetableView.Height))
				{
					logger.Debug("TimetableView.Height: {0}", TimetableView.HeightRequest);
					TimetableView.ScrollViewHeight = TimetableAreaScrollView.Height;
				}
			};
		}

		PageHeaderArea.IsLocationServiceEnabledChanged += OnIsLocationServiceEnabledChanged;
		TimetableView.IsLocationServiceEnabledChanged += OnIsLocationServiceEnabledChanged;

		TimetableView.CanUseLocationServiceChanged += (_, canUseLocationService) =>
		{
			logger.Info("CanUseLocationServiceChanged: {0}", canUseLocationService);
			PageHeaderArea.CanUseLocationService = canUseLocationService;
		};
		PageHeaderArea.CanUseLocationService = TimetableView.CanUseLocationService;

		MaxSpeedLabel.CurrentAppThemeColorBindingExtension = DTACElementStyles.DefaultTextColor;
		SpeedTypeLabel.CurrentAppThemeColorBindingExtension = DTACElementStyles.DefaultTextColor;
		NominalTractiveCapacityLabel.CurrentAppThemeColorBindingExtension = DTACElementStyles.DefaultTextColor;
		BeginRemarksLabel.CurrentAppThemeColorBindingExtension = DTACElementStyles.DefaultTextColor;

		logger.Trace("Created");
	}

	private void OnIsLocationServiceEnabledChanged(object? sender, ValueChangedEventArgs<bool> e)
	{
		logger.Info("IsLocationServiceEnabledChanged: {0}", e.NewValue);
		PageHeaderArea.IsLocationServiceEnabled = e.NewValue;
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
			PageHeaderArea.IsRunning = false;
			InstanceManager.DTACMarkerViewModel.IsToggled = false;

			MaxSpeedLabel.Text = ToWideConverter.Convert(newValue?.MaxSpeed);
			SpeedTypeLabel.Text = ToWideConverter.Convert(newValue?.SpeedType);
			NominalTractiveCapacityLabel.Text = ToWideConverter.Convert(newValue?.NominalTractiveCapacity);
			TrainInfo_BeforeDepartureArea.TrainInfoText = newValue?.TrainInfo ?? "";
			TrainInfo_BeforeDepartureArea.BeforeDepartureText = newValue?.BeforeDeparture ?? "";

			BeginRemarksLabel.Text = newValue?.BeginRemarks ?? "";

			SetDestinationString(newValue?.Destination);

			int dayCount = newValue?.DayCount ?? 0;
			this.IsNextDayLabel.IsVisible = dayCount > 0;
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalStylePage.OnSelectedTrainDataChanged");
			Utils.ExitWithAlert(ex);
		}
	}

	partial void OnAffectDateChanged(string? newValue)
	 => PageHeaderArea.AffectDateLabelText = newValue ?? "";

	private async void VerticalTimetableView_ScrollRequested(object? sender, VerticalTimetableView.ScrollRequestedEventArgs e)
	{
		if (DeviceInfo.Current.Idiom != DeviceIdiom.Phone && DeviceInfo.Current.Idiom != DeviceIdiom.Unknown)
		{
			logger.Debug("Device is not Phone nor Unknown -> scroll from {0} to {1}",
				TimetableAreaScrollView.ScrollY,
				e.PositionY);
			await TimetableAreaScrollView.ScrollToAsync(TimetableAreaScrollView.ScrollX, e.PositionY, true);
		}
		else
		{
			logger.Debug("Device is Phone or Unknown -> do nothing");
		}
	}

	void BeforeRemarks_TrainInfo_OpenCloseChanged(object _, ValueChangedEventArgs<bool> e)
		=> InstanceManager.DTACViewHostViewModel.RowDefinitionsProvider.BeforeRemarks_TrainInfo_OpenCloseChanged(this, TrainInfo_BeforeDepartureArea, e);

	string? _DestinationString = null;
	void SetDestinationString(string? value)
	{
		if (_DestinationString == value)
			return;

		_DestinationString = value;
		if (string.IsNullOrEmpty(value))
		{
			DestinationLabel.IsVisible = false;
			DestinationLabel.Text = null;
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

		DestinationLabel.Text = $"（{dstStr}行）";
		DestinationLabel.IsVisible = true;
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
				MainGrid.Remove(DebugMap);
				DebugMap = null;
				logger.Debug("DebugMap removed");
			}
			return;
		}
		DebugMap = new MyMap();
		DebugMap.SetTimetableRowList(TimetableView.SelectedTrainData?.Rows);
		DebugMap.SetIsLocationServiceEnabled(PageHeaderArea.IsLocationServiceEnabled);
		double mainWidth = 768;
		MainColumnDefinition.Width = new(mainWidth);
		DebugMapColumnDefinition.Width = new(1, GridUnitType.Star);
		MainGrid.Add(DebugMap, 1, 0);
		MainGrid.SetRowSpan(DebugMap, MainGrid.RowDefinitions.Count);
	}
}
