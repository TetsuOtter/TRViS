using CommunityToolkit.Maui.Views;

using TRViS.IO.RequestInfo;
using TRViS.Services;

namespace TRViS.RootPages;

public class SelectOnlineResourcePopup : Popup
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	readonly Button CloseButton = new()
	{
		Text = "Close",
		HorizontalOptions = LayoutOptions.End,
		Margin = new(4),
	};

	readonly Button LoadButton = new()
	{
		Text = "Load🌍",
		HorizontalOptions = LayoutOptions.End,
		Margin = new(4),
	};

	readonly Entry UrlInput = new()
	{
		Placeholder = "https://",
		Margin = new(4),
		ClearButtonVisibility = ClearButtonVisibility.Never,
		IsSpellCheckEnabled = false,
		IsTextPredictionEnabled = false,
		Keyboard = Keyboard.Url,
		MaxLength = 1024,
		ReturnType = ReturnType.Go,
	};

	readonly CollectionView UrlHistoryCollectionView = new()
	{
		Margin = new(4),
	};

	readonly Label AdviceLabel = new()
	{
		Text = "URLを入力するか、履歴から選択してください。",
		Margin = new(4),
		HorizontalOptions = LayoutOptions.Start,
		VerticalOptions = LayoutOptions.Center,
	};

	readonly ActivityIndicator LoadingIndicator = new()
	{
		IsRunning = false,
		IsVisible = false,
		HorizontalOptions = LayoutOptions.Center,
		VerticalOptions = LayoutOptions.Center,
	};

	public SelectOnlineResourcePopup()
	{
		logger.Debug("New SelectOnlineResourcePopup()");
		Size = new(480, 480);
		RootStyles.BackgroundColor.Apply(this, Popup.ColorProperty);
#if WINDOWS
		// 別ウィンドウを開くだけでも閉じてしまうため
		CanBeDismissedByTappingOutsideOfPopup = false;
#endif

		RootStyles.BackgroundBlackWhite.Apply(UrlInput, Button.BackgroundColorProperty);
		RootStyles.TableTextColor.Apply(UrlInput, Button.TextColorProperty);

		RootStyles.BackgroundBlackWhite.ToBrushTheme().Apply(UrlHistoryCollectionView, CollectionView.BackgroundProperty);

		RootStyles.TableTextColor.Apply(AdviceLabel, Label.TextColorProperty);

		UrlHistoryCollectionView.ItemTemplate = new DataTemplate(() =>
		{
			Label label = new();
			RootStyles.TableTextColor.Apply(label, Label.TextColorProperty);
			label.SetBinding(Label.TextProperty, static (string? v) => v);
			label.Padding = new Thickness(8);
			label.VerticalOptions = LayoutOptions.Center;
			return label;
		});

		// 本当にiOS 15以前のみで有効なプロパティなのかは不明
		bool isBeforeiOS15 = DeviceInfo.Platform == DevicePlatform.iOS && DeviceInfo.Version.Major < 15;
		if (!isBeforeiOS15)
			UrlInput.ClearButtonVisibility = ClearButtonVisibility.WhileEditing;

		Grid grid = new()
		{
			RowDefinitions =
			{
				new(new(1, GridUnitType.Auto)),
				new(new(1, GridUnitType.Star)),
				new(new(1, GridUnitType.Auto)),
				new(new(1, GridUnitType.Auto)),
			},
			ColumnDefinitions =
			{
				new(new(1, GridUnitType.Star)),
				new(new(1, GridUnitType.Auto)),
			},
			Padding = new(8),
		};

		CloseButton.Clicked += (s, e) => CloseAsync();

		Command DoLoadCommand = new(DoLoad);
		UrlInput.ReturnCommand = DoLoadCommand;
		LoadButton.Command = DoLoadCommand;

		UrlHistoryCollectionView.SelectionChanged += (_, e) =>
		{
			logger.Debug("UrlHistoryCollectionView.SelectionChanged");
			if (e.CurrentSelection.FirstOrDefault() is string selectedUrl)
			{
				UrlInput.Text = selectedUrl;
				logger.Trace("UrlInput.Text = {0}", UrlInput.Text);
			}
		};

		UrlInput.TextChanged += (_, e) =>
		{
			logger.Debug("UrlInput.TextChanged -> set UrlHistoryCollectionView.SelectedItem = {0}", e.NewTextValue);
			UrlHistoryCollectionView.SelectedItem = e.NewTextValue;
		};

		grid.Add(CloseButton, column: 1);
		Grid.SetColumnSpan(UrlHistoryCollectionView, 2);
		Grid.SetRow(UrlHistoryCollectionView, 1);
		grid.Add(UrlHistoryCollectionView);
		Grid.SetColumnSpan(UrlInput, 2);
		Grid.SetRow(UrlInput, 2);
		grid.Add(UrlInput);
		grid.Add(AdviceLabel, row: 3);
		grid.Add(LoadButton, column: 1, row: 3);
		grid.Add(LoadingIndicator, column: 1, row: 3);

		Content = grid;

		Opened += (_, _) =>
		{
			logger.Debug("SelectOnlineResourcePopup.Opened");
			UrlHistoryCollectionView.ItemsSource = InstanceManager.AppViewModel.ExternalResourceUrlHistory.Reverse();
		};
		Closed += (_, _) =>
		{
			logger.Debug("SelectOnlineResourcePopup.Closed");
		};

		logger.Debug("initialize completed");
	}

	void setInputIsEnabled(bool isEnabled)
	{
		UrlInput.IsEnabled = isEnabled;
		UrlHistoryCollectionView.IsEnabled = isEnabled;
		LoadButton.IsEnabled = isEnabled;
		CloseButton.IsEnabled = isEnabled;

		LoadingIndicator.IsRunning = !isEnabled;
		LoadingIndicator.IsVisible = !isEnabled;
	}

	private async void DoLoad()
	{
		try
		{
			setInputIsEnabled(false);
			if (string.IsNullOrEmpty(UrlInput.Text))
			{
				logger.Info("URL is null or empty");
				await Utils.DisplayAlert("Cannot Load from Web", "URLを入力してください。", "OK");
				return;
			}

			AppLinkInfo appLinkInfo = UrlInput.Text.StartsWith("trvis://")
				? AppLinkInfo.FromAppLink(UrlInput.Text)
				: new(
					AppLinkInfo.FileType.Json,
					Version: new(1, 0),
					ResourceUri: new(UrlInput.Text)
				);
			bool execResult = await InstanceManager.AppViewModel.HandleAppLinkUriAsync(appLinkInfo, CancellationToken.None);
			if (execResult)
			{
				await CloseAsync();
				return;
			}
		}
		catch (Exception ex)
		{
			logger.Error(ex, "DoLoad() failed");
		}
		finally
		{
			setInputIsEnabled(true);
		}
	}
}
