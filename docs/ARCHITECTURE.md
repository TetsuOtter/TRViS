# TRViS Architecture

## Project Structure

The TRViS solution has been reorganized to separate logic from the UI layer, improving maintainability and testability.

### Core Projects

#### TRViS.Core
System-wide utilities and common logic used across the application.

**Classes:**
- `StringUtils`: Character and string manipulation utilities
  - `ToWide()`: Converts ASCII characters to full-width characters
  - `InsertBetweenChars()`: Inserts characters/strings between each character
  - `InsertCharBetweenCharAndMakeWide()`: Combines insertion and wide conversion

- `Base64Utils`: URL-safe Base64 encoding/decoding
  - `UrlSafeBase64Encode()`: Encodes data to URL-safe Base64
  - `UrlSafeBase64Decode()`: Decodes URL-safe Base64 data

**Test Coverage:** TRViS.Core.Tests (12 unit tests)

#### TRViS.DTAC.Logic
D-TAC (Driver's Train Administration and Control) page-specific business logic.

**Classes:**
- `DestinationFormatter`: Formats destination strings for display
  - Adds proper spacing for single and double character destinations
  - Wraps destination in Japanese brackets with "行" suffix

- `TimetableDisplayLogic`: Display calculations and logic
  - `ShouldShowNextDayIndicator()`: Determines if next day indicator should be shown
  - `CalculateNonTimetableContentHeight()`: Calculates fixed content height
  - `CalculateScrollViewHeight()`: Calculates total scroll view height

**Test Coverage:** TRViS.DTAC.Logic.Tests (15 unit tests)

#### TRViS.TimetableService
Manages timetable data with support for dynamic insertions and deletions while maintaining unique IDs.

**Classes:**
- `ITimetableService`: Service interface
- `TimetableService`: Thread-safe implementation
- `TrainDataItem`: Train data with ID-tracked rows
- `TimetableRowItem`: Individual timetable row with unique ID
- `LocationInfoItem`: Location information (lat/lon/radius)
- `TimeDataItem`: Time data (hour/minutes/seconds)

**Features:**
- Thread-safe operations with locking
- ID-based tracking for all data
- Support for insert, update, and delete operations
- Maintains row order for timetable display

**Test Coverage:** TRViS.TimetableService.Tests (10 unit tests)

### Application Projects

#### TRViS (Main App)
.NET MAUI application with UI components.
- Current References: TRViS.IO, TRViS.LocationService, TRViS.NetworkSyncService
- **Note:** The main project should be updated to reference TRViS.Core, TRViS.DTAC.Logic, and TRViS.TimetableService in future iterations to fully complete the logic separation. Currently, some utility code remains in the TRViS.Utils namespace.

#### TRViS.IO
Input/Output operations and data models.

#### TRViS.LocationService
Location-based services and GPS handling.

#### TRViS.NetworkSyncService
Network synchronization functionality.

## Design Principles

1. **Separation of Concerns**: Logic is separated from UI components
2. **Testability**: All logic projects have comprehensive unit tests
3. **Maintainability**: Clear boundaries between system-wide, domain-specific, and UI logic
4. **ID-Based Tracking**: All timetable data uses unique IDs to support insertions and deletions

## Testing

Run all tests:
```bash
dotnet test
```

Run specific project tests:
```bash
dotnet test TRViS.Core.Tests/TRViS.Core.Tests.csproj
dotnet test TRViS.DTAC.Logic.Tests/TRViS.DTAC.Logic.Tests.csproj
dotnet test TRViS.TimetableService.Tests/TRViS.TimetableService.Tests.csproj
```

## Building

Build entire solution:
```bash
dotnet build TRViS.sln
```

Build individual projects:
```bash
dotnet build TRViS.Core/TRViS.Core.csproj
dotnet build TRViS.DTAC.Logic/TRViS.DTAC.Logic.csproj
dotnet build TRViS.TimetableService/TRViS.TimetableService.csproj
```
