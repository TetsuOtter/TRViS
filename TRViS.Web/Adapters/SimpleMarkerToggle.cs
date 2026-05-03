using System.ComponentModel;

using TRViS.DTAC.Logic.Abstractions;

namespace TRViS.Web.Adapters;

public sealed class SimpleMarkerToggle : IMarkerToggleController
{
	private bool _isToggled;

	public bool IsToggled
	{
		get => _isToggled;
		set
		{
			if (_isToggled == value) return;
			_isToggled = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsToggled)));
		}
	}

	public void ResetToggle()
	{
		IsToggled = false;
	}

	public event PropertyChangedEventHandler? PropertyChanged;
}
