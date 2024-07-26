using System;

namespace TRViS.Services;

public class LocationStateChangedEventArgs(
  int newStationIndex,
  bool isRunningToNextStation
) : EventArgs, IEquatable<LocationStateChangedEventArgs>
{
  public int NewStationIndex { get; } = newStationIndex;
  public bool IsRunningToNextStation { get; } = isRunningToNextStation;

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
