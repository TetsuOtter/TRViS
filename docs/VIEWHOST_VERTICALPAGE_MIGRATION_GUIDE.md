# ViewHost および VerticalStylePage のロジック分離ガイド

## 概要

ViewHost と VerticalStylePage のロジックが TRViS.DTAC.Logic プロジェクトに移行されました。以下のクラスが新規に追加されています：

### 新規追加クラス

1. **ViewHostState** (`TRViS.DTAC.Logic`)

   - ViewHost の状態を管理するモデル
   - WorkGroupInfo, WorkInfo, TrainInfo のサブモデルを含む

2. **ViewHostStateFactory** (`TRViS.DTAC.Logic`)

   - ViewHostState の作成と更新を行うファクトリクラス
   - PropertyChanged イベントで使用するロジックを提供

3. **VerticalPageState の拡張**

   - `ViewHostDisplayState` プロパティを追加
   - ViewHost の表示状態（IsVisible, IsVerticalViewMode など）を管理

4. **VerticalPageStateFactory の拡張**
   - `UpdateViewHostDisplayState()`: ViewHost の表示状態を更新
   - `UpdateAffectDate()`: AffectDate を更新
   - `ShouldApplyTrainData()`: 訓練データを適用すべきか判定

## コメントアウトされたロジックの移行

### ViewHost.xaml.cs

現在、以下のメソッドがコメントアウトされています：

```csharp
// private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
// private void OnSelectedWorkGroupChanged(IO.Models.DB.WorkGroup? newValue)
// private void OnSelectedWorkChanged(IO.Models.DB.Work? newValue)
// private void OnSelectedTrainChanged(TrainData? newValue)
```

これらのメソッドは ViewHostStateFactory を使用して以下のように移行できます：

```csharp
private ViewHostState viewHostState = ViewHostStateFactory.CreateEmptyState();

private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
{
    if (sender is not AppViewModel vm)
        return;

    try
    {
        switch (e.PropertyName)
        {
            case nameof(AppViewModel.SelectedWorkGroup):
                OnSelectedWorkGroupChanged(vm.SelectedWorkGroup);
                break;

            case nameof(AppViewModel.SelectedWork):
                OnSelectedWorkChanged(vm.SelectedWork);
                break;

            case nameof(AppViewModel.SelectedTrainData):
                OnSelectedTrainChanged(vm.SelectedTrainData);
                break;
        }
    }
    catch (Exception ex)
    {
        logger.Fatal(ex, "Unknown Exception");
        InstanceManager.CrashlyticsWrapper.Log(ex, "ViewHost.Vm_PropertyChanged");
        Utils.ExitWithAlert(ex);
    }
}

void OnSelectedWorkGroupChanged(IO.Models.DB.WorkGroup? newValue)
{
    string title = newValue?.Name ?? string.Empty;
    logger.Info("SelectedWorkGroup is changed to {0}", title);

    // State を更新
    ViewHostStateFactory.UpdateSelectedWorkGroup(viewHostState, title);

    // UI を更新
    HakoView.WorkSpaceName = title;
}

void OnSelectedWorkChanged(IO.Models.DB.Work? newValue)
{
    string title = newValue?.Name ?? string.Empty;
    logger.Info("SelectedWork is changed to {0}", title);

    // State を更新
    ViewHostStateFactory.UpdateSelectedWork(viewHostState, title);

    // UI を更新
    TitleLabel.Text = title;
    Title = title;
    HakoView.WorkName = title;
}

void OnSelectedTrainChanged(TrainData? newValue)
{
    int dayCount = newValue?.DayCount ?? 0;
    string affectDate = ViewHostStateFactory.FormatAffectDate(
        newValue?.AffectDate,
        dayCount
    );

    logger.Debug(
        "date: {0}, dayCount: {1}, AffectDate: {2}",
        newValue?.AffectDate,
        dayCount,
        affectDate
    );

    // State を更新
    ViewHostStateFactory.UpdateSelectedTrain(viewHostState, affectDate, dayCount);

    // UI を更新
    VerticalStylePageView.AffectDate = affectDate;
    HakoView.AffectDate = affectDate;
}
```

### VerticalStylePage.xaml.cs

現在、以下のメソッドがコメントアウトされています：

```csharp
// private void OnIsLocationServiceEnabledChanged(object? sender, ValueChangedEventArgs<bool> e)
// partial void OnSelectedTrainDataChanged(TrainData? newValue)
// partial void OnAffectDateChanged(string? newValue)
```

これらは VerticalPageStateFactory を使用して以下のように移行できます：

```csharp
// OnIsLocationServiceEnabledChanged メソッド
private void OnIsLocationServiceEnabledChanged(object? sender, ValueChangedEventArgs<bool> e)
{
    logger.Info("IsLocationServiceEnabledChanged: {0}", e.NewValue);

    // State を更新
    VerticalPageStateFactory.UpdateLocationServiceEnabledState(
        PageState,
        isEnabled: e.NewValue
    );

    // UI を更新
    PageHeaderArea.IsLocationServiceEnabled = PageState.PageHeaderState.IsLocationServiceEnabled;
    DebugMap?.SetIsLocationServiceEnabled(PageState.LocationServiceState.IsEnabled);
}

// OnSelectedTrainDataChanged メソッド
partial void OnSelectedTrainDataChanged(TrainData? newValue)
{
    if (CurrentShowingTrainData == newValue)
    {
        logger.Debug("CurrentShowingTrainData == newValue -> do nothing");
        return;
    }

    // ViewHost の表示状態を確認して、遅延ロードするかどうかを判定
    if (!VerticalPageStateFactory.ShouldApplyTrainData(
        newValue,
        DTACViewHostViewModel.IsViewHostVisible,
        DTACViewHostViewModel.IsVerticalViewMode))
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

        // State を作成
        PageState = VerticalPageStateFactory.CreateStateFromTrainData(
            trainData: newValue,
            affectDate: AffectDate,
            isLocationServiceEnabled: PageHeaderArea.IsLocationServiceEnabled,
            pageHeight: this.Height,
            contentOtherThanTimetableHeight: CONTENT_OTHER_THAN_TIMETABLE_HEIGHT,
            isPhoneIdiom: DeviceInfo.Current.Idiom == DeviceIdiom.Phone || DeviceInfo.Current.Idiom == DeviceIdiom.Unknown
        );

        // State を UI に適用
        ApplyPageState(PageState);

        // TimetableView を初期化
        TimetableView.InitializeWithTrainData(newValue);

        // State 以外の操作
        BindingContext = newValue;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            DebugMap?.SetTimetableRowList(newValue?.Rows);
        });
        PageHeaderArea.IsRunning = false;
        InstanceManager.DTACMarkerViewModel.IsToggled = false;
    }
    catch (Exception ex)
    {
        logger.Fatal(ex, "Unknown Exception");
        InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalStylePage.OnSelectedTrainDataChanged");
        Utils.ExitWithAlert(ex);
    }
}

// OnAffectDateChanged メソッド
partial void OnAffectDateChanged(string? newValue)
{
    // State を更新
    VerticalPageStateFactory.UpdateAffectDate(
        PageState.PageHeaderState,
        newValue
    );

    // UI を更新
    PageHeaderArea.AffectDateLabelText = newValue ?? "";
}
```

## DTACViewHostViewModel PropertyChanged の移行

DTACViewHostViewModel の PropertyChanged イベントでも、State を更新する必要があります：

```csharp
DTACViewHostViewModel.PropertyChanged += (_, e) =>
{
    try
    {
        switch (e.PropertyName)
        {
            case nameof(DTACViewHostViewModel.IsViewHostVisible):
            case nameof(DTACViewHostViewModel.IsVerticalViewMode):
                // State を更新
                VerticalPageStateFactory.UpdateViewHostDisplayState(
                    PageState,
                    DTACViewHostViewModel.IsViewHostVisible,
                    DTACViewHostViewModel.IsVerticalViewMode,
                    DTACViewHostViewModel.IsHakoMode,
                    DTACViewHostViewModel.IsWorkAffixMode
                );

                // UI を更新
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
```

## 実装のメリット

1. **テスト可能性**: ロジックが UI から分離されたため、ユニットテストを追加しやすい
2. **保守性**: 状態管理のロジックが集中化され、変更に強い
3. **再利用性**: ViewHostState と ViewHostStateFactory は他の場所でも使用できる
4. **トレーサビリティ**: すべての状態変更が State オブジェクトを通じて行われるため、追跡が容易

## 次のステップ

1. コメントアウトされたメソッドを実装に復活させる
2. 新しいロジッククラスを使用してテストを追加する
3. LocationService の状態更新ロジックも State で管理する（必要に応じて）
