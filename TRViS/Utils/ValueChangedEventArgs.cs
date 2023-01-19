using System;

namespace TRViS;

public class ValueChangedEventArgs<T> : EventArgs
{
	public T OldValue { get; }
	public T NewValue { get; }

	public ValueChangedEventArgs(in T oldValue, in T newValue)
	{
		OldValue = oldValue;
		NewValue = newValue;
	}
}
