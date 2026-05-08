using TRViS.IO.RequestInfo;
using TRViS.Services;
using TRViS.Utils;

namespace TRViS.RootPages;

public class SelectOnlineResourcePopup : ContentPage
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	// AutomationIds. Mirrored in TRViS.UITests/AutomationIds.cs (SelectOnlineResource).
	internal const string AutomationId_CloseButton = "SelectOnlineResource.CloseButton";
	internal const string AutomationId_LoadButton = "SelectOnlineResource.LoadButton";
	internal const string AutomationId_UrlInput = "SelectOnlineResource.UrlInput";
	internal const string AutomationId_UrlHistoryList = "SelectOnlineResource.UrlHistoryList";
	internal const string AutomationId_AdviceLabel = "SelectOnlineResource.AdviceLabel";
	internal const string AutomationId_UrlHistoryItemPrefix = "SelectOnlineResource.UrlHistoryItem.";

	readonly Button CloseButton = new()
	{
		AutomationId = AutomationId_CloseButton,
		Text = "Close",
		HorizontalOptions = LayoutOptions.End,
		Margin = new(4),
	};

	readonly Button LoadButton = new()
	{
		AutomationId = AutomationId_LoadButton,
		Text = "Load🌍",
		HorizontalOptions = LayoutOptions.End,
		Margin = new(4),
	};

	readonly Entry UrlInput = new()
	{
		AutomationId = AutomationId_UrlInput,
		Placeholder = "https://",
		Margin = new(4),
		ClearButtonVisibility = ClearButtonVisibility.Never,
		IsSpellCheckEnabled = false,
		IsTextPredictionEnabled = false,
		Keyboard = Keyboard.Url,
		MaxLength = 1024,
		ReturnType = ReturnType.Go,
	};

	readonly CollectionView UrlHistoryListView = new()
	{
		AutomationId = AutomationId_UrlHistoryList,
		SelectionMode = SelectionMode.Single,
		Margin = new(4),
	};

	readonly Label AdviceLabel = new()
	{
		AutomationId = AutomationId_AdviceLabel,
		Text = "URLを入力するか、履歴から選択してください。",
		Margin = new(4),
		HorizontalOptions = LayoutOptions.Start,
		VerticalOptions = LayoutOptions.Center,
	};

	// Re-entrancy guard for selection <-> text-input synchronization.
	// Without this, SelectionChanged sets UrlInput.Text -> TextChanged sets
	// UrlHistoryListView.SelectedItem -> SelectionChanged fires again with
	// the same value but a fresh SelectedItems collection, which on some
	// MAUI handlers visually clears the row highlight before the user sees it.
	private bool _isSyncingSelectionAndText = false;

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

		RootStyles.BackgroundColor.Apply(this, BackgroundColorProperty);
		Padding = new(8);

		RootStyles.BackgroundBlackWhite.Apply(UrlInput, Button.BackgroundColorProperty);
		RootStyles.TableTextColor.Apply(UrlInput, Button.TextColorProperty);

		RootStyles.BackgroundBlackWhite.ToBrushTheme().Apply(UrlHistoryListView, CollectionView.BackgroundProperty);

		RootStyles.TableTextColor.Apply(AdviceLabel, Label.TextColorProperty);

		UrlHistoryListView.ItemTemplate = new DataTemplate(() =>
		{
			// Per-row AutomationId is "SelectOnlineResource.UrlHistoryItem.<url>" so
			// UI tests can locate a known seeded URL by accessibility id.
			Label label = new()
			{
				Padding = new(4),
			};
			RootStyles.TableTextColor.Apply(label, Label.TextColorProperty);
			label.SetBinding(Label.TextProperty, static (string? v) => v);
			// Use the classic Binding form with a converter so the compiled-binding
			// generator (BSG) doesn't choke on ternary/null-coalescing expressions.
			label.SetBinding(Label.AutomationIdProperty, new Binding(
				path: ".",
				mode: BindingMode.OneWay,
				converter: UrlHistoryItemAutomationIdConverter.Instance));
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

		CloseButton.Clicked += async (s, e) => await Close();

		Command DoLoadCommand = new(DoLoad);
		UrlInput.ReturnCommand = DoLoadCommand;
		LoadButton.Command = DoLoadCommand;

		UrlHistoryListView.SelectionChanged += (_, e) =>
		{
			logger.Debug("UrlHistoryListView.SelectionChanged");
			if (_isSyncingSelectionAndText)
			{
				logger.Trace("Selection change is part of a text->selection sync; skipping");
				return;
			}
			if (e.CurrentSelection.FirstOrDefault() is string selectedItem)
			{
				_isSyncingSelectionAndText = true;
				try
				{
					UrlInput.Text = selectedItem;
					logger.Trace("UrlInput.Text = {0}", UrlInput.Text);
				}
				finally
				{
					_isSyncingSelectionAndText = false;
				}
			}
		};

		UrlInput.TextChanged += (_, e) =>
		{
			logger.Debug("UrlInput.TextChanged -> set UrlHistoryListView.SelectedItem = {0}", e.NewTextValue);
			if (_isSyncingSelectionAndText)
			{
				logger.Trace("Text change is part of a selection->text sync; skipping");
				return;
			}
			_isSyncingSelectionAndText = true;
			try
			{
				UrlHistoryListView.SelectedItem = e.NewTextValue;
			}
			finally
			{
				_isSyncingSelectionAndText = false;
			}
		};

		grid.Add(CloseButton, column: 1);
		Grid.SetColumnSpan(UrlHistoryListView, 2);
		Grid.SetRow(UrlHistoryListView, 1);
		grid.Add(UrlHistoryListView);
		Grid.SetColumnSpan(UrlInput, 2);
		Grid.SetRow(UrlInput, 2);
		grid.Add(UrlInput);
		grid.Add(AdviceLabel, row: 3);
		grid.Add(LoadButton, column: 1, row: 3);
		grid.Add(LoadingIndicator, column: 1, row: 3);
		RootStyles.BackgroundColor.Apply(grid, Grid.BackgroundColorProperty);

		Content = grid;

		logger.Debug("initialize completed");
	}

	void setInputIsEnabled(bool isEnabled)
	{
		UrlInput.IsEnabled = isEnabled;
		UrlHistoryListView.IsEnabled = isEnabled;
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
				await Util.DisplayAlertAsync("Cannot Load from Web", "URLを入力してください。", "OK");
				return;
			}

			string urlText = UrlInput.Text;
			bool execResult;
			if (urlText.StartsWith("trvis://"))
			{
				// String overload runs the full pipeline including the test-only
				// seed-url-history handler (DEBUG builds), then falls through to
				// AppLinkInfo.FromAppLink for normal trvis:// links.
				execResult = await InstanceManager.AppViewModel.HandleAppLinkUriAsync(urlText, CancellationToken.None);
			}
			else
			{
				AppLinkInfo appLinkInfo = new(
					AppLinkInfo.FileType.Json,
					Version: new(1, 0),
					ResourceUri: new(urlText)
				);
				execResult = await InstanceManager.AppViewModel.HandleAppLinkUriAsync(appLinkInfo, CancellationToken.None);
			}
			if (execResult)
			{
				await Close();
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

	private async Task Close()
	{
		logger.Debug("SelectOnlineResourcePopup.Closing");
		await Navigation.PopModalAsync();
	}

	/// <summary>
	/// Converts a URL string to its per-row AutomationId. Defined as a classic
	/// IValueConverter so MAUI's compiled-binding generator can process the data
	/// template without rejecting an inline ternary expression.
	/// </summary>
	private sealed class UrlHistoryItemAutomationIdConverter : IValueConverter
	{
		public static readonly UrlHistoryItemAutomationIdConverter Instance = new();

		public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
			=> value is string s
				? AutomationId_UrlHistoryItemPrefix + s
				: AutomationId_UrlHistoryItemPrefix;

		public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
			=> throw new NotSupportedException();
	}

	internal void OnOpened()
	{
		logger.Debug("SelectOnlineResourcePopup.Opened");
		MainThread.BeginInvokeOnMainThread(() =>
		{
			// Materialize into a List so CollectionView's selection mapping
			// uses stable string references (a fresh IEnumerable each enumeration
			// can confuse SelectedItem reference equality on some MAUI handlers).
			UrlHistoryListView.ItemsSource = InstanceManager.AppViewModel.ExternalResourceUrlHistory.Reverse().ToList();
		});
	}
}
