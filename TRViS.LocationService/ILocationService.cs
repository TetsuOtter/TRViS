namespace TRViS.Services;

public class LocationStateChangedEventArgs : EventArgs, IEquatable<LocationStateChangedEventArgs>
{
	public int NewStationIndex { get; }
	public bool IsRunningToNextStation { get; }

	public LocationStateChangedEventArgs(int newStationIndex, bool isRunningToNextStation)
	{
		NewStationIndex = newStationIndex;
		IsRunningToNextStation = isRunningToNextStation;
	}

	public bool Equals(LocationStateChangedEventArgs? other)
	{
		if (other is null)
			return false;

		if (ReferenceEquals(this, other))
			return true;

		return
			NewStationIndex == other.NewStationIndex
			&&
			IsRunningToNextStation == other.IsRunningToNextStation
		;
	}

	public override bool Equals(object obj)
		=> Equals(obj as LocationStateChangedEventArgs);

	public override int GetHashCode()
		=> HashCode.Combine(NewStationIndex, IsRunningToNextStation);

	public override string ToString()
	{
		return $"{nameof(LocationStateChangedEventArgs)} {{ {nameof(NewStationIndex)}: {NewStationIndex}, {nameof(IsRunningToNextStation)}: {IsRunningToNextStation} }}";
	}
}

public interface ILocationService
{
	event EventHandler<LocationStateChangedEventArgs>? LocationStateChanged;

	StaLocationInfo[]? StaLocationInfo { get; set; }

	int CurrentStationIndex { get; }

	bool IsRunningToNextStation { get; }

	void ResetLocationInfo();
}
