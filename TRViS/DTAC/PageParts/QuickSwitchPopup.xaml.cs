using System.Collections.ObjectModel;
using TRViS.IO;
using TRViS.IO.Models;
using TRViS.NetworkSyncService;
using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS.DTAC;

public partial class QuickSwitchPopup : ContentView
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	private AppViewModel ViewModel { get; }

	private enum TabType
	{
		WorkGroup,
		Work,
		TrainSearch
	}

	private TabType _currentTab = TabType.WorkGroup;
	private TabType CurrentTab
	{
		get => _currentTab;
		set
		{
			if (_currentTab == value)
				return;
			_currentTab = value;
			UpdateTabStyles();
		}
	}

	// Store the original Work/Train when displaying a searched train
	private Work? _originalScheduledWork;
	private TrainData? _originalScheduledTrain;
	private bool _isDisplayingSearchedTrain;

	private readonly ObservableCollection<TrainSearchResultViewModel> _searchResults = new();
	private bool _isSearching;

	public QuickSwitchPopup()
	{
		logger.Trace("Creating...");

		ViewModel = InstanceManager.AppViewModel;

		InitializeComponent();

		DTACElementStyles.DefaultBGColor.Apply(this, BackgroundColorProperty);

		// Set up lists
		WorkGroupListView.ItemsSource = ViewModel.WorkGroupList;
		UpdateWorkGroupSelection();
		WorkListView.ItemsSource = ViewModel.WorkList;
		UpdateWorkSelection();

		// Apply styles
		DTACElementStyles.TabAreaBGColor.Apply(WorkGroupListContainer, Border.BackgroundColorProperty);
		DTACElementStyles.TabAreaBGColor.Apply(WorkListContainer, Border.BackgroundColorProperty);
		DTACElementStyles.TabAreaBGColor.Apply(TrainSearchContainer, Border.BackgroundColorProperty);

		// Set up tab buttons
		WorkGroupTabButton.Tapped += WorkGroupTab_Tapped;
		WorkTabButton.Tapped += WorkTab_Tapped;
		TrainSearchTabButton.Tapped += TrainSearchTab_Tapped;

		// Set up search results
		SearchResultsListView.ItemsSource = _searchResults;

		// Initial tab selection is WorkGroup
		CurrentTab = TabType.WorkGroup;
		UpdateTabStyles();

		logger.Trace("Created");
	}

	private void UpdateTabStyles()
	{
		logger.Trace("CurrentTab: {0}", CurrentTab);

		WorkGroupTabButton.IsSelected = CurrentTab == TabType.WorkGroup;
		WorkTabButton.IsSelected = CurrentTab == TabType.Work;
		TrainSearchTabButton.IsSelected = CurrentTab == TabType.TrainSearch;

		// Update list visibility
		WorkGroupListContainer.IsVisible = CurrentTab == TabType.WorkGroup;
		WorkListContainer.IsVisible = CurrentTab == TabType.Work;
		TrainSearchContainer.IsVisible = CurrentTab == TabType.TrainSearch;

		// Update return button visibility
		ReturnToScheduledButton.IsVisible = _isDisplayingSearchedTrain && CurrentTab == TabType.TrainSearch;

		// Scroll to selected item
		if (CurrentTab == TabType.WorkGroup && WorkGroupListView.SelectedItem is not null)
		{
			WorkGroupListView.ScrollTo(WorkGroupListView.SelectedItem, ScrollToPosition.MakeVisible, false);
		}
		else if (CurrentTab == TabType.Work && WorkListView.SelectedItem is not null)
		{
			WorkListView.ScrollTo(WorkListView.SelectedItem, ScrollToPosition.MakeVisible, false);
		}
	}

	private void UpdateWorkGroupSelection()
	{
		if (ViewModel.SelectedWorkGroup is null)
		{
			WorkGroupListView.SelectedItem = null;
			return;
		}

		// IDベースで選択アイテムを検索
		var selectedItem = ViewModel.WorkGroupList?.FirstOrDefault(wg => wg.Id == ViewModel.SelectedWorkGroup.Id);
		WorkGroupListView.SelectedItem = selectedItem;
	}

	private void UpdateWorkSelection()
	{
		if (ViewModel.SelectedWork is null)
		{
			WorkListView.SelectedItem = null;
			return;
		}

		// IDベースで選択アイテムを検索
		var selectedItem = ViewModel.WorkList?.FirstOrDefault(w => w.Id == ViewModel.SelectedWork.Id);
		WorkListView.SelectedItem = selectedItem;
	}

	private void WorkGroupTab_Tapped(object? sender, EventArgs e)
	{
		logger.Info("WorkGroup tab tapped");
		CurrentTab = TabType.WorkGroup;
	}

	private void WorkTab_Tapped(object? sender, EventArgs e)
	{
		logger.Info("Work tab tapped");
		CurrentTab = TabType.Work;
	}

	private void TrainSearchTab_Tapped(object? sender, EventArgs e)
	{
		logger.Info("TrainSearch tab tapped");
		CurrentTab = TabType.TrainSearch;
	}

	private void WorkGroupListView_ItemSelected(object sender, SelectedItemChangedEventArgs e)
	{
		if (e.SelectedItem is WorkGroup selectedWorkGroup)
		{
			logger.Info("WorkGroup selected: {0}", selectedWorkGroup.Name);
			ViewModel.SelectedWorkGroup = selectedWorkGroup;

			// Update Work list with new WorkGroup's works
			WorkListView.ItemsSource = ViewModel.WorkList;
			UpdateWorkSelection();

			// Automatically switch to Work tab
			CurrentTab = TabType.Work;
		}
	}

	private void WorkListView_ItemSelected(object sender, SelectedItemChangedEventArgs e)
	{
		if (e.SelectedItem is Work selectedWork)
		{
			logger.Info("Work selected: {0}", selectedWork.Name);
			ViewModel.SelectedWork = selectedWork;
		}
	}

	// Train Search event handlers
	private async void SearchButton_Clicked(object? sender, EventArgs e)
	{
		await PerformTrainSearchAsync();
	}

	private async void TrainNumberEntry_Completed(object? sender, EventArgs e)
	{
		await PerformTrainSearchAsync();
	}

	private void CancelSearchButton_Clicked(object? sender, EventArgs e)
	{
		logger.Info("Search cancelled");
		TrainNumberEntry.Text = string.Empty;
		_searchResults.Clear();
	}

	private async void SearchResultsListView_ItemSelected(object sender, SelectedItemChangedEventArgs e)
	{
		if (e.SelectedItem is TrainSearchResultViewModel result)
		{
			logger.Info("Search result selected: {0}", result.TrainNumber);
			await ConfirmAndDisplayTrainAsync(result);
			SearchResultsListView.SelectedItem = null;
		}
	}

	private void ReturnToScheduledButton_Clicked(object? sender, EventArgs e)
	{
		logger.Info("Returning to scheduled train");
		ReturnToScheduledTrain();
	}

	private async Task PerformTrainSearchAsync()
	{
		string trainNumber = TrainNumberEntry.Text?.Trim() ?? string.Empty;
		if (string.IsNullOrEmpty(trainNumber))
		{
			logger.Warn("Train number is empty");
			await Application.Current!.MainPage!.DisplayAlert("エラー", "列番を入力してください", "OK");
			return;
		}

		if (_isSearching)
		{
			logger.Warn("Search already in progress");
			return;
		}

		_isSearching = true;
		SearchButton.IsEnabled = false;
		_searchResults.Clear();

		try
		{
			logger.Info("Searching for train: {0}", trainNumber);

			// Get WebSocket service from location service
			var locationService = InstanceManager.LocationService;
			if (locationService is not WebSocketNetworkSyncService wsService)
			{
				logger.Warn("Location service is not WebSocketNetworkSyncService");
				await Application.Current!.MainPage!.DisplayAlert("エラー", "WebSocket接続が必要です", "OK");
				return;
			}

			var response = await wsService.SearchTrainAsync(trainNumber);

			if (response is null)
			{
				logger.Warn("Search timed out or failed");
				await Application.Current!.MainPage!.DisplayAlert("エラー", "検索がタイムアウトしました。サーバーが応答しません。", "OK");
				return;
			}

			if (!response.Success)
			{
				logger.Warn("Search failed: {0}", response.ErrorMessage);
				await Application.Current!.MainPage!.DisplayAlert("エラー", response.ErrorMessage ?? "検索に失敗しました", "OK");
				return;
			}

			if (response.Results is null || response.Results.Length == 0)
			{
				logger.Info("No results found");
				await Application.Current!.MainPage!.DisplayAlert("情報", "該当する列車が見つかりませんでした", "OK");
				return;
			}

			logger.Info("Found {0} results", response.Results.Length);
			foreach (var result in response.Results)
			{
				_searchResults.Add(new TrainSearchResultViewModel(result));
			}
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Error during train search");
			await Application.Current!.MainPage!.DisplayAlert("エラー", "検索中にエラーが発生しました", "OK");
		}
		finally
		{
			_isSearching = false;
			SearchButton.IsEnabled = true;
		}
	}

	private async Task ConfirmAndDisplayTrainAsync(TrainSearchResultViewModel result)
	{
		// Show confirmation dialog
		string message = $"列番: {result.TrainNumber}\n" +
			$"行路: {result.WorkName ?? "不明"}\n" +
			$"開始駅: {result.StartStation ?? "不明"}\n" +
			$"終了駅: {result.EndStation ?? "不明"}\n" +
			$"開始時刻: {result.StartTime ?? "不明"}\n" +
			$"終了時刻: {result.EndTime ?? "不明"}\n\n" +
			$"この列車を表示しますか?";

		bool confirmed = await Application.Current!.MainPage!.DisplayAlert("確認", message, "OK", "キャンセル");
		if (!confirmed)
		{
			logger.Info("User cancelled train selection");
			return;
		}

		// Get full train data from server
		var locationService = InstanceManager.LocationService;
		if (locationService is not WebSocketNetworkSyncService wsService)
		{
			logger.Warn("Location service is not WebSocketNetworkSyncService");
			return;
		}

		logger.Info("Fetching full train data for: {0}", result.TrainId);
		var trainDataResponse = await wsService.GetTrainDataAsync(result.TrainId, result.WorkId);

		if (trainDataResponse is null || !trainDataResponse.Success)
		{
			logger.Warn("Failed to get train data: {0}", trainDataResponse?.ErrorMessage);
			await Application.Current!.MainPage!.DisplayAlert("エラー", 
				trainDataResponse?.ErrorMessage ?? "列車データの取得に失敗しました", "OK");
			return;
		}

		// Save current work/train if not already saved
		if (!_isDisplayingSearchedTrain)
		{
			_originalScheduledWork = ViewModel.SelectedWork;
			_originalScheduledTrain = ViewModel.SelectedTrainData;
			logger.Info("Saved original work: {0}, train: {1}", 
				_originalScheduledWork?.Id, _originalScheduledTrain?.Id);
		}

		// Parse and display the train data
		// Note: trainDataResponse.Data is a JSON string (as per WebSocket protocol spec),
		// not a JSON object, so we deserialize it from string
		try
		{
			if (trainDataResponse.Data is not null)
			{
				var trainData = System.Text.Json.JsonSerializer.Deserialize<TRViS.JsonModels.TrainData>(
					trainDataResponse.Data,
					new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
				);

				if (trainData is not null)
				{
					var convertedTrain = JsonModelsConverter.ConvertTrain(trainData);
					ViewModel.SelectedTrainData = convertedTrain;
					_isDisplayingSearchedTrain = true;
					ReturnToScheduledButton.IsVisible = true;

					logger.Info("Successfully displayed searched train: {0}", result.TrainNumber);
					
					// Note: Hako tab visibility should be handled by the parent page/view model
					// based on whether the train is from the current work or searched
				}
			}
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Failed to parse train data");
			await Application.Current!.MainPage!.DisplayAlert("エラー", "列車データの解析に失敗しました", "OK");
		}
	}

	private void ReturnToScheduledTrain()
	{
		if (_originalScheduledWork is not null)
		{
			ViewModel.SelectedWork = _originalScheduledWork;
		}

		if (_originalScheduledTrain is not null)
		{
			ViewModel.SelectedTrainData = _originalScheduledTrain;
		}

		_isDisplayingSearchedTrain = false;
		ReturnToScheduledButton.IsVisible = false;
		
		logger.Info("Returned to scheduled work: {0}, train: {1}",
			_originalScheduledWork?.Id, _originalScheduledTrain?.Id);
	}
}

// ViewModel for displaying search results in ListView
public class TrainSearchResultViewModel
{
	public string TrainId { get; }
	public string TrainNumber { get; }
	public string WorkId { get; }
	public string? WorkName { get; }
	public string? StartStation { get; }
	public string? EndStation { get; }
	public string? StartTime { get; }
	public string? EndTime { get; }
	public string? Destination { get; }

	public string DisplayText => $"{TrainNumber} - {WorkName ?? "不明"} ({StartStation} → {EndStation})";

	public TrainSearchResultViewModel(TrainSearchResult result)
	{
		TrainId = result.TrainId;
		TrainNumber = result.TrainNumber;
		WorkId = result.WorkId;
		WorkName = result.WorkName;
		StartStation = result.StartStation;
		EndStation = result.EndStation;
		StartTime = result.StartTime;
		EndTime = result.EndTime;
		Destination = result.Destination;
	}
}
