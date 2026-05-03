using System.ComponentModel;

using TRViS.DTAC.Logic.Abstractions;

namespace TRViS.Web.Adapters;

public sealed class SimpleEasterEgg : IEasterEggSettings
{
	private bool _keepScreenOn = true;
	private bool _showMapWhenLandscape;

	public bool KeepScreenOnWhenRunning
	{
		get => _keepScreenOn;
		set
		{
			if (_keepScreenOn == value) return;
			_keepScreenOn = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(KeepScreenOnWhenRunning)));
		}
	}

	public bool ShowMapWhenLandscape
	{
		get => _showMapWhenLandscape;
		set
		{
			if (_showMapWhenLandscape == value) return;
			_showMapWhenLandscape = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowMapWhenLandscape)));
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;
}
