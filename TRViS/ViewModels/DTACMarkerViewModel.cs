using CommunityToolkit.Mvvm.ComponentModel;

namespace TRViS.ViewModels;

public partial class DTACMarkerViewModel : ObservableObject
{
	public List<Color> ColorList { get; } = new()
	{
		// Red
		new(0xF0, 0x40, 0x20),

		// Lime
		new(0x40, 0xF0, 0x20),

		// Blue
		new(0x20, 0x40, 0xF0),

		// Yellow
		new(0xf0, 0xf0, 0x40),
	};

	public List<string> TextList { get; } = new()
	{
		string.Empty,
		"停車",
	};

	[ObservableProperty]
	private Color _SelectedColor;

	[ObservableProperty]
	private string _SelectedText;

	public DTACMarkerViewModel()
	{
		_SelectedColor = ColorList[0];
		_SelectedText = TextList[0];
	}
}
