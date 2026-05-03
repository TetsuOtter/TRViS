# Quick Start: Using VerticalPageState

## 30-Second Overview

The `VerticalPageState` model encapsulates all display logic for the D-TAC vertical page. Instead of scattering visibility logic throughout `VerticalStylePage`, you now:

1. **Create** state from train data: `VerticalPageStateFactory.CreateStateFromTrainData(...)`
2. **Update** state on events: `VerticalPageStateFactory.UpdateXxxState(...)`
3. **Bind** UI to state: `Label.IsVisible = state.Xxx.IsVisible`

## Common Tasks

### Task 1: Check if Something Should Be Visible

```csharp
// Get state for current train
VerticalPageState state = PageState;

// Check visibility
if (state.Destination.IsVisible)
{
    // Show destination label
}

if (state.NextDayIndicatorState.IsVisible)
{
    // Show next day badge
}
```

### Task 2: Update State When Train Changes

```csharp
partial void OnSelectedTrainDataChanged(TrainData? newValue)
{
    // Create fresh state
    PageState = VerticalPageStateFactory.CreateStateFromTrainData(
        trainData: newValue,
        affectDate: "2024年1月1日",
        isLocationServiceEnabled: true,
        pageHeight: Height,
        contentOtherThanTimetableHeight: 300,
        isPhoneIdiom: true
    );

    // Apply to UI
    ApplyState(PageState);
}
```

### Task 3: Handle User Interactions

```csharp
// User clicks button to expand before-departure area
void OnOpenBeforeDepartureClicked(object sender, EventArgs e)
{
    // Update state
    VerticalPageStateFactory.UpdateTrainInfoAreaOpenCloseState(
        PageState.TrainInfoAreaState,
        isToOpen: true
    );

    // UI responds to state change
    UpdateUIFromState();
}
```

### Task 4: Update GPS Location

```csharp
void OnGpsLocationReceived(GpsLocationEventArgs e)
{
    // Update state with new location
    VerticalPageStateFactory.UpdateGpsLocation(
        PageState.LocationServiceState,
        latitude: e.Latitude,
        longitude: e.Longitude,
        accuracy: e.Accuracy
    );

    // Use updated state
    DebugMap.SetCurrentLocation(
        PageState.LocationServiceState.CurrentLatitude ?? 0,
        PageState.LocationServiceState.CurrentLongitude ?? 0,
        PageState.LocationServiceState.CurrentAccuracy ?? 20
    );
}
```

## State Structure at a Glance

```
PageState
├── .Destination.IsVisible          → Show/hide destination label
├── .Destination.Text               → Destination text to display
├── .NextDayIndicatorState.IsVisible → Show/hide next day badge
├── .TrainInfoAreaState.IsOpen      → Is before-departure section open?
├── .DebugMapState.IsVisible        → Show debug map?
├── .TimetableActivityIndicatorState.IsVisible → Show loading?
├── .LocationServiceState.IsEnabled → GPS enabled?
├── .TrainDisplayInfo.MaxSpeed      → Train speed text
├── .TrainDisplayInfo.SpeedType     → Speed type text
└── ... and more
```

## Testing Locally

```bash
# Run all DTAC.Logic tests
dotnet test TRViS.DTAC.Logic.Tests

# Run specific test class
dotnet test TRViS.DTAC.Logic.Tests --filter VerticalPageStateFactoryTests

# Run specific test method
dotnet test TRViS.DTAC.Logic.Tests --filter "UpdateDestinationState_WithValidString"
```

## Key Methods

| Method                                       | Purpose                                |
| -------------------------------------------- | -------------------------------------- |
| `CreateStateFromTrainData()`                 | Initialize state from train data       |
| `UpdateDestinationState()`                   | Format destination and set visibility  |
| `UpdateNextDayIndicatorState()`              | Determine if next-day label is visible |
| `UpdateTrainInfoAreaOpenCloseState()`        | Mark animation as starting             |
| `CompleteTrainInfoAreaAnimation()`           | Mark animation as finished             |
| `UpdateDebugMapState()`                      | Toggle map based on orientation        |
| `UpdateTimetableActivityIndicatorState()`    | Toggle loading indicator               |
| `UpdateLocationServiceEnabledState()`        | Enable/disable location service        |
| `UpdateGpsLocation()`                        | Update GPS coordinates                 |
| `UpdateTimetableLocationServiceCapability()` | Set if location service can be used    |
| `UpdatePageHeaderRunState()`                 | Set if run is active                   |

## Common Patterns

### Pattern: Bind Multiple UI Elements from State

```csharp
private void ApplyDestinationState(VerticalPageState state)
{
    var dest = state.Destination;
    DestinationLabel.IsVisible = dest.IsVisible;
    DestinationLabel.Text = dest.Text;
}

private void ApplyTrainInfoState(VerticalPageState state)
{
    var info = state.TrainInfoAreaState;
    TrainInfoArea.IsVisible = info.IsVisible;
    TrainInfoArea.HeightRequest = info.CurrentHeight;
}

private void ApplyAllState(VerticalPageState state)
{
    ApplyDestinationState(state);
    ApplyTrainInfoState(state);
    // ... etc
}
```

### Pattern: State-Based Conditionals

```csharp
if (PageState.LocationServiceState.IsEnabled)
{
    DebugMap?.SetIsLocationServiceEnabled(true);
}

if (PageState.DebugMapState.IsVisible)
{
    MainGrid.Add(DebugMap, 1, 0);
}

if (PageState.NextDayIndicatorState.IsVisible)
{
    NextDayLabel.Show();
}
```

### Pattern: Updating During Animation

```csharp
void AnimateOpenClose(bool isOpening)
{
    // Tell state animation is starting
    VerticalPageStateFactory.UpdateTrainInfoAreaOpenCloseState(
        PageState.TrainInfoAreaState,
        isToOpen: isOpening
    );

    // Run animation...
    double target = isOpening ? 90 : 0;
    new Animation(v =>
    {
        TrainInfoArea.HeightRequest = v;
    }, 0, target)
    .Commit(this, "anim", finished: (_, canceled) =>
    {
        // Tell state animation is done
        VerticalPageStateFactory.CompleteTrainInfoAreaAnimation(
            PageState.TrainInfoAreaState,
            wasOpenAnimation: isOpening
        );
    });
}
```

## Debugging Tips

### Inspect State

```csharp
// Print state for debugging
var state = PageState;
Debug.WriteLine($"Destination visible: {state.Destination.IsVisible}");
Debug.WriteLine($"Destination text: {state.Destination.Text}");
Debug.WriteLine($"Next day visible: {state.NextDayIndicatorState.IsVisible}");
Debug.WriteLine($"Before-dep open: {state.TrainInfoAreaState.IsOpen}");
```

### Verify State Change

```csharp
var oldState = PageState;
VerticalPageStateFactory.UpdateDestinationState(
    PageState.Destination,
    "新目的地"
);
var newState = PageState;

Assert.NotEqual(oldState.Destination.Text, newState.Destination.Text);
```

## Documentation References

- **Full Architecture**: See `DTAC_VERTICAL_PAGE_LOGIC_SEPARATION.md`
- **Refactoring Guide**: See `VERTICALSTYLEPAGE_REFACTORING_GUIDE.md`
- **API Docs**: See code comments in `VerticalPageState.cs` and `VerticalPageStateFactory.cs`
- **Usage Examples**: See `VerticalPageStateUsageGuide.cs`

## Need Help?

1. Check the test file: `VerticalPageStateTests.cs` (39 examples)
2. Read the refactoring guide: `VERTICALSTYLEPAGE_REFACTORING_GUIDE.md`
3. Review before/after patterns in the guide
4. Look at usage examples in `VerticalPageStateUsageGuide.cs`

## Summary

State-based UI development:

- ✅ **One source of truth**: `VerticalPageState`
- ✅ **All logic testable**: Factory methods fully tested
- ✅ **Clear bindings**: UI reflects state, not model
- ✅ **Easy to extend**: Add state property, factory method, test, bind

That's it! You're ready to use `VerticalPageState`. 🚀
