using TR.Maui.AnchorPopover;

using TRViS.IO.Models;
using TRViS.Localization;
using TRViS.NetworkSyncService;
using TRViS.Services;
using TRViS.Utils;
using TRViS.ViewModels;

namespace TRViS.DTAC;

public partial class QuickSwitchPopup : ContentView
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	private AppViewModel ViewModel { get; }
	private IAnchorPopover? _popover;

	private enum Tab { WorkGroup, Work, Search }

	private Tab _currentTab = Tab.WorkGroup;
	private Tab CurrentTab
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

	/// <param name="useNumericKeypad">
	/// True to show the software numeric keypad next to the search results (wide
	/// screens: tablet, landscape, desktop) and make TrainNumberEntry read-only.
	/// False to fall back to the OS keyboard (narrow screens, e.g. iPhone portrait,
	/// where there isn't room for a list + keypad side by side) and hide the keypad.
	/// </param>
	public QuickSwitchPopup(bool useNumericKeypad = true)
	{
		logger.Trace("Creating...");

		ViewModel = Adapters.PresenterFactory.GetRawAppViewModel();

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

		// Localized text for the search UI
		SearchTabButton.ButtonText = AppResources.QuickSwitch_Tab_Search;
		TrainNumberEntry.Placeholder = AppResources.QuickSwitch_Search_NumberPlaceholder;
		MatchModePrefixLabel.Text = AppResources.QuickSwitch_Search_MatchMode_Prefix;
		MatchModeContainsLabel.Text = AppResources.QuickSwitch_Search_MatchMode_Contains;
		MatchModeExactLabel.Text = AppResources.QuickSwitch_Search_MatchMode_Exact;
		MatchModePrefixRadio.IsChecked = true; // TrainSearchMatchMode.Prefix (default)

		if (useNumericKeypad)
		{
			TrainNumberEntry.IsReadOnly = true;
		}
		else
		{
			NumericKeypad.IsVisible = false;
			SearchContainer.ColumnSpacing = 0;
			TrainNumberEntry.IsReadOnly = false;
			TrainNumberEntry.Keyboard = Keyboard.Numeric;
		}

		// The search tab is available only when connected to a WebSocket server that
		// advertises the TrainSearch feature (ServerInfo.Features) — this also covers
		// offline (disconnected/reconnecting), since IsTrainSearchAvailable requires an
		// active connection.
		SearchTabButton.IsVisible = ViewModel.IsTrainSearchAvailable;

		// Set up tab buttons
		WorkGroupTabButton.Tapped += WorkGroupTab_Tapped;
		WorkTabButton.Tapped += WorkTab_Tapped;
		SearchTabButton.Tapped += SearchTab_Tapped;

		UpdateTabStyles();

		logger.Trace("Created");
	}

	/// <summary>
	/// ホスト (ViewHost) がポップオーバー参照を渡す。検索確定 / 所定復帰時に自身を閉じるために使う。
	/// </summary>
	internal void SetPopover(IAnchorPopover popover) => _popover = popover;

	private void UpdateTabStyles()
	{
		logger.Trace("CurrentTab: {0}", CurrentTab);

		WorkGroupTabButton.IsSelected = CurrentTab == Tab.WorkGroup;
		WorkTabButton.IsSelected = CurrentTab == Tab.Work;
		SearchTabButton.IsSelected = CurrentTab == Tab.Search;

		// Update container visibility
		WorkGroupListContainer.IsVisible = CurrentTab == Tab.WorkGroup;
		WorkListContainer.IsVisible = CurrentTab == Tab.Work;
		SearchContainer.IsVisible = CurrentTab == Tab.Search;

		// Scroll to selected item
		if (CurrentTab == Tab.WorkGroup && WorkGroupListView.SelectedItem is not null)
		{
			WorkGroupListView.ScrollTo(item: WorkGroupListView.SelectedItem, position: Microsoft.Maui.Controls.ScrollToPosition.MakeVisible, animate: false);
		}
		else if (CurrentTab == Tab.Work && WorkListView.SelectedItem is not null)
		{
			WorkListView.ScrollTo(item: WorkListView.SelectedItem, position: Microsoft.Maui.Controls.ScrollToPosition.MakeVisible, animate: false);
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
		CurrentTab = Tab.WorkGroup;
	}

	private void WorkTab_Tapped(object? sender, EventArgs e)
	{
		logger.Info("Work tab tapped");
		CurrentTab = Tab.Work;
	}

	private void SearchTab_Tapped(object? sender, EventArgs e)
	{
		logger.Info("Search tab tapped");
		CurrentTab = Tab.Search;
	}

	private void WorkGroupListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		var selected = e.CurrentSelection?.FirstOrDefault();
		if (selected is WorkGroup selectedWorkGroup)
		{
			logger.Info("WorkGroup selected: {0}", selectedWorkGroup.Name);
			ViewModel.SelectedWorkGroup = selectedWorkGroup;

			// Update Work list with new WorkGroup's works
			WorkListView.ItemsSource = ViewModel.WorkList;
			UpdateWorkSelection();

			// Automatically switch to Work tab
			CurrentTab = Tab.Work;
		}
	}

	private void WorkListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		var selected = e.CurrentSelection?.FirstOrDefault();
		if (selected is Work selectedWork)
		{
			logger.Info("Work selected: {0}", selectedWork.Name);
			ViewModel.SelectedWork = selectedWork;
		}
	}

	// ================================================================
	// 列車検索 (Issue #197): 入力中にデバウンスしつつ自動検索する。
	// ================================================================

	private const int SearchDebounceMilliseconds = 400;

	private CancellationTokenSource? _searchDebounceCts;
	private TrainSearchMatchMode _matchMode = TrainSearchMatchMode.Prefix;

	private void MatchModeRadio_CheckedChanged(object? sender, CheckedChangedEventArgs e)
	{
		// RadioButton raises this for both the newly-checked button and the
		// previously-checked one going unchecked; only react to the checked one.
		if (!e.Value)
			return;

		_matchMode = ReferenceEquals(sender, MatchModeContainsRadio) ? TrainSearchMatchMode.Contains
			: ReferenceEquals(sender, MatchModeExactRadio) ? TrainSearchMatchMode.Exact
			: TrainSearchMatchMode.Prefix;

		// Re-run the current query under the newly selected match mode, if any.
		TriggerSearch(TrainNumberEntry.Text);
	}

	// Software numeric keypad: TrainNumberEntry is IsReadOnly (no OS keyboard), so digits
	// are appended/removed here. Assigning Entry.Text fires TextChanged the same as typing,
	// so this reuses the existing debounced-search path unchanged.
	private void KeypadDigit_Clicked(object? sender, EventArgs e)
	{
		if (sender is not Button button)
			return;
		TrainNumberEntry.Text = (TrainNumberEntry.Text ?? string.Empty) + button.Text;
	}

	private void KeypadBackspace_Clicked(object? sender, EventArgs e)
	{
		string current = TrainNumberEntry.Text ?? string.Empty;
		if (current.Length > 0)
			TrainNumberEntry.Text = current[..^1];
	}

	private void KeypadClear_Clicked(object? sender, EventArgs e)
	{
		TrainNumberEntry.Text = string.Empty;
	}

	private void TrainNumberEntry_TextChanged(object? sender, TextChangedEventArgs e)
		=> TriggerSearch(e.NewTextValue);

	private void TriggerSearch(string? text)
	{
		_searchDebounceCts?.Cancel();
		_searchDebounceCts?.Dispose();

		string number = text?.Trim() ?? string.Empty;
		if (string.IsNullOrEmpty(number))
		{
			_searchDebounceCts = null;
			SearchResultsView.ItemsSource = null;
			HideSearchStatus();
			SetSearchLoading(false);
			return;
		}

		var cts = new CancellationTokenSource();
		_searchDebounceCts = cts;
		_ = RunDebouncedSearchAsync(number, cts.Token);
	}

	private async Task RunDebouncedSearchAsync(string number, CancellationToken cancellationToken)
	{
		try
		{
			await Task.Delay(SearchDebounceMilliseconds, cancellationToken);
		}
		catch (TaskCanceledException)
		{
			return;
		}

		logger.Info("SearchTrain requested: {0}", number);
		SearchResultsView.ItemsSource = null;
		SetSearchStatus(AppResources.QuickSwitch_Search_Searching);
		SetSearchLoading(true);
		try
		{
			var results = await ViewModel.SearchTrainAsync(number, _matchMode, cancellationToken);
			if (cancellationToken.IsCancellationRequested)
				return;

			if (results.Count == 0)
			{
				SetSearchStatus(AppResources.QuickSwitch_Search_NoResults);
			}
			else
			{
				HideSearchStatus();
				SearchResultsView.ItemsSource = results;
			}
		}
		catch (OperationCanceledException)
		{
			// A newer keystroke superseded this search; stay silent.
		}
		catch (TimeoutException)
		{
			if (!cancellationToken.IsCancellationRequested)
			{
				logger.Warn("SearchTrain timed out");
				SetSearchStatus(AppResources.QuickSwitch_Search_Timeout);
			}
		}
		catch (Exception ex)
		{
			if (!cancellationToken.IsCancellationRequested)
			{
				logger.Error(ex, "SearchTrain failed");
				SetSearchStatus(AppResources.QuickSwitch_Search_Error);
			}
		}
		finally
		{
			if (!cancellationToken.IsCancellationRequested)
				SetSearchLoading(false);
		}
	}

	private async void SearchResultsView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		var selected = e.CurrentSelection?.FirstOrDefault() as TrainSearchResult;
		// Clear selection so the same row can be tapped again after cancelling.
		SearchResultsView.SelectedItem = null;
		if (selected is null)
			return;

		logger.Info("Search result selected: {0} ({1})", selected.TrainNumber, selected.TrainId);

		string body = string.Format(
			AppResources.QuickSwitch_Search_ConfirmBodyFormat,
			selected.TrainNumber, selected.WorkName,
			selected.StartStationName, selected.StartTime,
			selected.EndStationName, selected.EndTime);
		bool ok = await Util.DisplayAlertAsync(
			AppResources.QuickSwitch_Search_ConfirmTitle, body,
			AppResources.Common_OK, AppResources.Common_Cancel);
		if (!ok)
			return;

		try
		{
			var train = await ViewModel.FetchSearchedTrainTimetableAsync(selected);
			if (train is null)
			{
				await Util.DisplayAlertAsync(
					AppResources.QuickSwitch_Search_ErrorTitle,
					AppResources.QuickSwitch_Search_FetchError, AppResources.Common_OK);
				return;
			}
			// 行路 (WorkGroup/Work) ごと完全に切り替える。ヘッダの行路番号も切り替わる。
			ViewModel.SwitchToSearchedTrain(selected.WorkGroupId, selected.WorkId, selected.TrainId);
			await DismissAsync();
		}
		catch (TimeoutException)
		{
			logger.Warn("FetchSearchedTrainTimetable timed out");
			await Util.DisplayAlertAsync(
				AppResources.QuickSwitch_Search_ErrorTitle,
				AppResources.QuickSwitch_Search_Timeout, AppResources.Common_OK);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "FetchSearchedTrainTimetable failed");
			await Util.DisplayAlertAsync(
				AppResources.QuickSwitch_Search_ErrorTitle,
				AppResources.QuickSwitch_Search_FetchError, AppResources.Common_OK);
		}
	}

	private void SetSearchStatus(string text)
	{
		SearchStatusLabel.Text = text;
		SearchStatusLabel.IsVisible = true;
	}

	private void HideSearchStatus() => SearchStatusLabel.IsVisible = false;

	private void SetSearchLoading(bool loading)
	{
		SearchLoadingIndicator.IsRunning = loading;
		SearchLoadingIndicator.IsVisible = loading;
	}

	private async Task DismissAsync()
	{
		_searchDebounceCts?.Cancel();
		if (_popover is not null)
			await _popover.DismissAsync();
	}
}
