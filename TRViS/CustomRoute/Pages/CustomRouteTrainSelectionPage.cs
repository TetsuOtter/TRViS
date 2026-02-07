using Microsoft.Maui.Controls;
using TRViS.CustomRoute.Controls;
using TRViS.IO.Models;
using TRViS.ViewModels;

namespace TRViS.CustomRoute.Pages;

using TRViS;

/// <summary>
/// CustomRoute列車選択ページ
/// 列車一覧を表示し、時刻表を表示する列車を選択するページ
/// C#コードビハインド実装（XAML禁止）
/// </summary>
public class CustomRouteTrainSelectionPage : ContentPage
{
	public static readonly string NameOfThisClass = nameof(CustomRouteTrainSelectionPage);

	private AppViewModel _appViewModel = null!;
	private TrainSelector _trainSelector = null!;

	public CustomRouteTrainSelectionPage()
	{
		Title = "Custom Route - Select Train";
		InitializeViewModel();
		InitializeLayout();
		Shell.SetNavBarIsVisible(this, true);
		Appearing += OnPageAppearing;
	}

	private void InitializeViewModel()
	{
		_appViewModel = InstanceManager.AppViewModel;
		BindingContext = _appViewModel;
	}

	private void InitializeLayout()
	{
		// メインレイアウト（縦方向）
		var mainLayout = new VerticalStackLayout
		{
			Padding = 0,
			Spacing = 0,
		};

		// 列車選択コントロール（全画面を使用）
		_trainSelector = new TrainSelector();
		_trainSelector.SetTrainList(_appViewModel.OrderedTrainDataList);
		_trainSelector.TrainSelected += OnTrainSelected;

		mainLayout.Add(_trainSelector);

		// メインコンテンツ
		Content = mainLayout;
	}

	private void OnPageAppearing(object? sender, EventArgs e)
	{
		// ページが表示されるたびにデータを読み込む
		LoadTrainData();
	}

	private void LoadTrainData()
	{
		System.Diagnostics.Debug.WriteLine($"[LoadTrainData] SelectedWork: {_appViewModel.SelectedWork?.Name ?? "null"}, Loader: {(_appViewModel.Loader != null ? "not null" : "null")}");

		// 現在選択されているWorkから列車データを取得
		if (_appViewModel.SelectedWork is not null && _appViewModel.Loader is not null)
		{
			var trainDataList = _appViewModel.Loader.GetTrainDataList(_appViewModel.SelectedWork.Id);
			System.Diagnostics.Debug.WriteLine($"[LoadTrainData] trainDataList from Loader: {trainDataList?.Count ?? 0} items");
			if (trainDataList is not null && trainDataList.Count > 0)
			{
				System.Diagnostics.Debug.WriteLine($"[LoadTrainData] Updating OrderedTrainDataList from Loader");
				_appViewModel.OrderedTrainDataList = trainDataList;
				_trainSelector.SetTrainList(trainDataList);
				return;
			}
		}

		// OrderedTrainDataListが利用可能な場合はそれを使用
		if (_appViewModel.OrderedTrainDataList is not null && _appViewModel.OrderedTrainDataList.Count > 0)
		{
			System.Diagnostics.Debug.WriteLine($"[LoadTrainData] Using OrderedTrainDataList: {_appViewModel.OrderedTrainDataList.Count} items");
		}
		else
		{
			System.Diagnostics.Debug.WriteLine($"[LoadTrainData] No trains available");
		}
	}

	private async void OnTrainSelected(object? sender, TrainData trainData)
	{
		// 選択された列車をAppViewModelに設定
		_appViewModel.SelectedTrainData = trainData;

		// 時刻表ページへナビゲート
		try
		{
			await Shell.Current.GoToAsync($"///{NameOfThisClass}/timetable");
		}
		catch (Exception ex)
		{
			MainThread.BeginInvokeOnMainThread(async () =>
			{
				await DisplayAlert("Navigation Error", $"Failed to navigate: {ex.Message}", "OK");
			});
		}
	}
}
