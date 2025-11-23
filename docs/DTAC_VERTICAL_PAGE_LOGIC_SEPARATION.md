# D-TAC Vertical Page Logic Separation Enhancement

## Overview

This document describes the enhanced logic separation implementation for the D-TAC vertical page. The improvement strengthens the separation of concerns by introducing a comprehensive state model (`VerticalPageState`) that encapsulates all display-related logic and flags.

## What Changed

### 1. New `TRViS.DTAC.Logic` Components

#### VerticalPageState.cs

A comprehensive state model containing all display-related information:

- **DestinationInfo**: Handles destination formatting and visibility
- **TrainInfoAreaState**: Controls before-departure section open/close animation
- **NextDayIndicatorState**: Manages next-day label visibility
- **DebugMapState**: Manages debug map visibility (easter egg feature)
- **TimetableActivityIndicatorState**: Controls loading indicator visibility
- **TimetableViewState**: Manages timetable rendering state
- **ScrollViewState**: Handles scroll positioning and sizing
- **LocationServiceState**: Manages GPS location state
- **PageHeaderState**: Controls header UI elements
- **TrainDisplayInfo**: Holds train display information (speed, remarks, etc.)

#### VerticalPageStateFactory.cs

Static factory class with methods to create and update state:

**Key Methods:**

- `CreateStateFromTrainData()`: Initializes state from train data
- `UpdateDestinationState()`: Formats and sets destination visibility
- `UpdateNextDayIndicatorState()`: Determines next day label visibility
- `UpdateTrainInfoAreaOpenCloseState()`: Animates before-departure section
- `UpdateDebugMapState()`: Toggles map based on orientation and settings
- `UpdateTimetableActivityIndicatorState()`: Manages loading indicator
- `UpdateLocationServiceEnabledState()`: Synchronizes location service state
- `UpdateGpsLocation()`: Updates GPS coordinates
- And many more...

#### VerticalPageStateUsageGuide.cs

Documentation explaining how to use the new state system in UI components.

### 2. Comprehensive Test Coverage

**VerticalPageStateTests.cs** (39 new tests)

All state factory methods are thoroughly tested:

- Destination formatting logic
- Next day indicator visibility
- Animation state management
- Debug map visibility conditions
- Activity indicator state
- Location service updates
- GPS coordinate updates
- And more...

**Test Results**: All 39 tests passing ✅

## Architecture Benefits

### Before

```
VerticalStylePage.xaml.cs
├── Contains logic for destination formatting
├── Contains logic for next day indicator visibility
├── Contains animation state management
├── Contains debug map show/hide logic
├── Directly manages UI element visibility
└── Mixed business logic with UI code
```

### After

```
VerticalPageState (TRViS.DTAC.Logic)
├── Pure data model representing all display state
└── No business logic

VerticalPageStateFactory (TRViS.DTAC.Logic)
├── All business logic for state determination
├── Static methods for state mutations
├── Fully testable without UI framework
└── Clear separation from rendering

VerticalStylePage.xaml.cs (TRViS)
├── Receives VerticalPageState
├── Binds UI to state properties
├── Handles user events
└── Delegates to factory for state updates
```

## How to Use in UI Components

### 1. Create Initial State

```csharp
var state = VerticalPageStateFactory.CreateStateFromTrainData(
    trainData: selectedTrainData,
    affectDate: "2024年1月1日",
    isLocationServiceEnabled: locationService.IsEnabled,
    pageHeight: this.Height,
    contentOtherThanTimetableHeight: CONTENT_HEIGHT,
    isPhoneIdiom: DeviceInfo.Current.Idiom == DeviceIdiom.Phone
);
```

### 2. Bind UI to State (Not Model Directly)

```csharp
// BAD: UI logic in code-behind
DestinationLabel.IsVisible = !string.IsNullOrEmpty(trainData?.Destination);

// GOOD: Follow the state
DestinationLabel.IsVisible = state.Destination.IsVisible;
DestinationLabel.Text = state.Destination.Text;
```

### 3. Update State on User Actions

```csharp
// User clicks to open before-departure section
VerticalPageStateFactory.UpdateTrainInfoAreaOpenCloseState(
    state.TrainInfoAreaState,
    isToOpen: true
);

// Animation completes
VerticalPageStateFactory.CompleteTrainInfoAreaAnimation(
    state.TrainInfoAreaState,
    wasOpenAnimation: true
);
```

### 4. Update State from External Events

```csharp
// GPS location updated
VerticalPageStateFactory.UpdateGpsLocation(
    state.LocationServiceState,
    latitude: gpsEvent.Latitude,
    longitude: gpsEvent.Longitude,
    accuracy: gpsEvent.Accuracy
);
```

## Testing Benefits

All logic is tested without UI framework dependencies:

```csharp
[Fact]
public void UpdateDestinationState_WithValidString_FormatsAndSetsVisible()
{
    // Arrange
    var state = new DestinationInfo();
    var destination = "東京";

    // Act
    VerticalPageStateFactory.UpdateDestinationState(state, destination);

    // Assert
    Assert.True(state.IsVisible);
    Assert.NotNull(state.Text);
    Assert.Contains("行", state.Text);
    Assert.Equal(destination, state.OriginalValue);
}
```

## Next Steps for Full Integration

To complete the refactoring of `VerticalStylePage`:

1. **Create a state property in VerticalStylePage**

   ```csharp
   VerticalPageState PageState { get; set; }
   ```

2. **Initialize state on train data change**

   ```csharp
   partial void OnSelectedTrainDataChanged(TrainData? newValue)
   {
       PageState = VerticalPageStateFactory.CreateStateFromTrainData(
           trainData: newValue,
           affectDate: AffectDate,
           isLocationServiceEnabled: /* ... */,
           pageHeight: Height,
           contentOtherThanTimetableHeight: CONTENT_OTHER_THAN_TIMETABLE_HEIGHT,
           isPhoneIdiom: DeviceInfo.Current.Idiom == DeviceIdiom.Phone
       );

       // Apply state to UI
       ApplyState(PageState);
   }
   ```

3. **Create ApplyState method**

   ```csharp
   private void ApplyState(VerticalPageState state)
   {
       DestinationLabel.IsVisible = state.Destination.IsVisible;
       DestinationLabel.Text = state.Destination.Text;
       IsNextDayLabel.IsVisible = state.NextDayIndicatorState.IsVisible;
       // ... bind all other UI elements
   }
   ```

4. **Delegate state updates to factory**
   ```csharp
   void BeforeRemarks_TrainInfo_OpenCloseChanged(object sender, ValueChangedEventArgs<bool> e)
   {
       VerticalPageStateFactory.UpdateTrainInfoAreaOpenCloseState(
           PageState.TrainInfoAreaState,
           isToOpen: e.NewValue
       );
       // Animate and then update ApplyState
   }
   ```

## Files Added

### Logic Files (TRViS.DTAC.Logic)

- `VerticalPageState.cs` - Complete state model
- `VerticalPageStateFactory.cs` - State factory and mutation methods
- `VerticalPageStateUsageGuide.cs` - Usage documentation

### Test Files (TRViS.DTAC.Logic.Tests)

- `VerticalPageStateTests.cs` - 39 comprehensive unit tests

## Compatibility

- No breaking changes to existing code
- Fully backward compatible
- Can be integrated incrementally

## Benefits Summary

✅ **Better Testability**: All logic testable without UI framework
✅ **Clear Separation**: Logic separated from rendering
✅ **Maintainability**: Easy to understand what controls what
✅ **Extensibility**: Easy to add new display features
✅ **Documentation**: Self-documenting state model
✅ **Debuggability**: Easy to inspect and debug state
✅ **Type Safety**: Strongly-typed state instead of scattered flags

## References

See `VerticalPageStateUsageGuide.cs` for detailed usage examples and patterns.
