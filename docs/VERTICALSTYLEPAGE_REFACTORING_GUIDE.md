# VerticalStylePage Refactoring Guide

## Quick Reference: How to Use VerticalPageState in VerticalStylePage

This guide shows practical before/after code patterns for integrating `VerticalPageState` into the `VerticalStylePage` component.

## Pattern 1: Destination Display

### Before (Logic in UI)

```csharp
private string? _DestinationString = null;
void SetDestinationString(string? value)
{
    if (_DestinationString == value)
        return;

    _DestinationString = value;

    var formatted = TRViS.DTAC.Logic.DestinationFormatter.FormatDestination(value);
    if (formatted is null)
    {
        DestinationLabel.IsVisible = false;
        DestinationLabel.Text = null;
        return;
    }

    DestinationLabel.Text = formatted;
    DestinationLabel.IsVisible = true;
}

partial void OnSelectedTrainDataChanged(TrainData? newValue)
{
    // ...
    SetDestinationString(newValue?.Destination);
}
```

### After (Logic in State)

```csharp
// In state initialization
VerticalPageStateFactory.UpdateDestinationState(
    PageState.Destination,
    newValue?.Destination
);

// Binding (no code needed if using proper data binding)
DestinationLabel.IsVisible = PageState.Destination.IsVisible;
DestinationLabel.Text = PageState.Destination.Text;

// Or simpler with helper method
private void ApplyDestinationState(VerticalPageState state)
{
    DestinationLabel.IsVisible = state.Destination.IsVisible;
    DestinationLabel.Text = state.Destination.Text;
}
```

## Pattern 2: Next Day Indicator

### Before (Logic Scattered)

```csharp
partial void OnSelectedTrainDataChanged(TrainData? newValue)
{
    // ... other code ...

    int dayCount = newValue?.DayCount ?? 0;
    this.IsNextDayLabel.IsVisible = dayCount > 0;
}
```

### After (Centralized Logic)

```csharp
// All logic in one place
VerticalPageStateFactory.UpdateNextDayIndicatorState(
    PageState.NextDayIndicatorState,
    dayCount: newValue?.DayCount ?? 0
);

// Binding
IsNextDayLabel.IsVisible = PageState.NextDayIndicatorState.IsVisible;
```

## Pattern 3: Train Info Before-Departure Animation

### Before (Complex State Management)

```csharp
const string DateAndStartButton_AnimationName = nameof(DateAndStartButton_AnimationName);
RowDefinition TrainInfo_BeforeDepature_RowDefinition { get; } = new(0);

void BeforeRemarks_TrainInfo_OpenCloseChanged(object sender, ValueChangedEventArgs<bool> e)
{
    bool isToOpen = e.NewValue;
    (double start, double end) = isToOpen
        ? (TrainInfo_BeforeDepature_RowDefinition.Height.Value, TRAIN_INFO_BEFORE_DEPARTURE_ROW_HEIGHT)
        : (TrainInfo_BeforeDepature_RowDefinition.Height.Value, 0d);

    if (this.AnimationIsRunning(DateAndStartButton_AnimationName))
    {
        this.AbortAnimation(DateAndStartButton_AnimationName);
    }

    new Animation(
        v =>
        {
            if (!TrainInfo_BeforeDepartureArea.IsVisible)
            {
                TrainInfo_BeforeDepartureArea.IsVisible = true;
            }
            TrainInfo_BeforeDepature_RowDefinition.Height = v;
            TrainInfo_BeforeDepartureArea.HeightRequest = v;
        },
        start,
        end,
        Easing.SinInOut
    )
        .Commit(
            this,
            DateAndStartButton_AnimationName,
            finished: (_, canceled) =>
            {
                if (!isToOpen && !canceled)
                {
                    TrainInfo_BeforeDepartureArea.IsVisible = false;
                }
            }
        );
}
```

### After (State Handles Logic)

```csharp
void BeforeRemarks_TrainInfo_OpenCloseChanged(object sender, ValueChangedEventArgs<bool> e)
{
    // Update state
    VerticalPageStateFactory.UpdateTrainInfoAreaOpenCloseState(
        PageState.TrainInfoAreaState,
        isToOpen: e.NewValue
    );

    bool isToOpen = e.NewValue;
    (double start, double end) = isToOpen
        ? (TrainInfo_BeforeDepature_RowDefinition.Height.Value, TRAIN_INFO_BEFORE_DEPARTURE_ROW_HEIGHT)
        : (TrainInfo_BeforeDepature_RowDefinition.Height.Value, 0d);

    // Same animation code as before...
    // But now it's coordinated with state tracking

    // When animation completes:
    VerticalPageStateFactory.CompleteTrainInfoAreaAnimation(
        PageState.TrainInfoAreaState,
        wasOpenAnimation: isToOpen
    );
}
```

## Pattern 4: Location Service Integration

### Before (State Scattered)

```csharp
TimetableView.IsLocationServiceEnabledChanged += (_, e) =>
{
    logger.Info("IsLocationServiceEnabledChanged: {0}", e.NewValue);
    PageHeaderArea.IsLocationServiceEnabled = e.NewValue;
};

InstanceManager.LocationService.OnGpsLocationUpdated += (_, e) =>
{
    if (DebugMap is null || e is null)
        return;

    logger.Debug("OnGpsLocationUpdated: {0}", e);
    DebugMap.SetCurrentLocation(e.Latitude, e.Longitude, e.Accuracy ?? 20);
};
```

### After (Centralized State)

```csharp
TimetableView.IsLocationServiceEnabledChanged += (_, e) =>
{
    logger.Info("IsLocationServiceEnabledChanged: {0}", e.NewValue);

    // Update state
    VerticalPageStateFactory.UpdateLocationServiceEnabledState(
        PageState,
        isEnabled: e.NewValue
    );

    // Apply to UI
    ApplyLocationServiceState(PageState);
};

InstanceManager.LocationService.OnGpsLocationUpdated += (_, e) =>
{
    if (DebugMap is null || e is null)
        return;

    logger.Debug("OnGpsLocationUpdated: {0}", e);

    // Update state
    VerticalPageStateFactory.UpdateGpsLocation(
        PageState.LocationServiceState,
        latitude: e.Latitude,
        longitude: e.Longitude,
        accuracy: e.Accuracy
    );

    // Apply to UI using state
    DebugMap.SetCurrentLocation(
        PageState.LocationServiceState.CurrentLatitude ?? 0,
        PageState.LocationServiceState.CurrentLongitude ?? 0,
        PageState.LocationServiceState.CurrentAccuracy ?? 20
    );
};

private void ApplyLocationServiceState(VerticalPageState state)
{
    PageHeaderArea.IsLocationServiceEnabled = state.PageHeaderState.IsLocationServiceEnabled;
    TimetableView.IsLocationServiceEnabled = state.TimetableViewState.IsLocationServiceEnabled;
    DebugMap?.SetIsLocationServiceEnabled(state.LocationServiceState.IsEnabled);
}
```

## Pattern 5: State Initialization

### Before (Multiple Assignments)

```csharp
partial void OnSelectedTrainDataChanged(TrainData? newValue)
{
    if (CurrentShowingTrainData == newValue)
        return;
    if (!DTACViewHostViewModel.IsViewHostVisible || !DTACViewHostViewModel.IsVerticalViewMode)
        return;

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
```

### After (Single State Creation)

```csharp
partial void OnSelectedTrainDataChanged(TrainData? newValue)
{
    if (CurrentShowingTrainData == newValue)
        return;
    if (!DTACViewHostViewModel.IsViewHostVisible || !DTACViewHostViewModel.IsVerticalViewMode)
        return;

    try
    {
        VerticalTimetableView_ScrollRequested(this, new(0));
        CurrentShowingTrainData = newValue;
        logger.Info("SelectedTrainDataChanged: {0}", newValue);

        // Initialize entire state from train data
        PageState = VerticalPageStateFactory.CreateStateFromTrainData(
            trainData: newValue,
            affectDate: AffectDate,
            isLocationServiceEnabled: PageHeaderArea.IsLocationServiceEnabled,
            pageHeight: this.Height,
            contentOtherThanTimetableHeight: CONTENT_OTHER_THAN_TIMETABLE_HEIGHT,
            isPhoneIdiom: DeviceInfo.Current.Idiom == DeviceIdiom.Phone
        );

        // Apply all state to UI in one method
        ApplyState(PageState);

        // Non-state operations
        BindingContext = newValue;
        TimetableView.SelectedTrainData = newValue;
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

private void ApplyState(VerticalPageState state)
{
    // Destination
    DestinationLabel.IsVisible = state.Destination.IsVisible;
    DestinationLabel.Text = state.Destination.Text;

    // Train display info
    MaxSpeedLabel.Text = ToWideConverter.Convert(state.TrainDisplayInfo.MaxSpeed);
    SpeedTypeLabel.Text = ToWideConverter.Convert(state.TrainDisplayInfo.SpeedType);
    NominalTractiveCapacityLabel.Text = ToWideConverter.Convert(state.TrainDisplayInfo.NominalTractiveCapacity);
    TrainInfo_BeforeDepartureArea.TrainInfoText = state.TrainInfoAreaState.TrainInfoText;
    TrainInfo_BeforeDepartureArea.BeforeDepartureText = state.TrainInfoAreaState.BeforeDepartureText;
    BeginRemarksLabel.Text = state.TrainDisplayInfo.BeginRemarks;

    // Next day indicator
    IsNextDayLabel.IsVisible = state.NextDayIndicatorState.IsVisible;

    // Location service
    PageHeaderArea.CanUseLocationService = state.PageHeaderState.CanUseLocationService;
}
```

## Key Benefits of Refactoring

1. **Single Source of Truth**: All display logic in state model
2. **Easier Testing**: State logic tested without UI framework
3. **Clearer Code**: UI code focuses on binding, not logic
4. **Maintainability**: Easy to find where logic for specific UI is
5. **Extensibility**: Add new features by extending state and factory
6. **Debugging**: Easy to inspect state and understand what's visible and why

## Integration Checklist

- [ ] Create `VerticalPageState PageState` property
- [ ] Create `ApplyState(VerticalPageState state)` helper method
- [ ] Update `OnSelectedTrainDataChanged` to use factory
- [ ] Update all event handlers to update state first
- [ ] Update all UI bindings to use state instead of model
- [ ] Remove old logic methods and fields
- [ ] Test all UI interactions work correctly
- [ ] Verify all tests still pass
