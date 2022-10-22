using CommunityToolkit.Mvvm.ComponentModel;

namespace TRViS.ViewModels;

public record MarkerInfo(string Name, Color Color);

public partial class DTACMarkerViewModel : ObservableObject
{
	public List<MarkerInfo> ColorList { get; } = new()
	{
		// Red
		new("赤", new(0xF0, 0x40, 0x20)),

		// Lime
		new("緑", new(0x40, 0xF0, 0x20)),

		// Blue
		new("青", new(0x20, 0x40, 0xF0)),

		// Yellow
		new("黄", new(0xf0, 0xf0, 0x40)),
	};

	public List<string> TextList { get; } = new()
	{
		string.Empty,
		"停車",
	};

	[ObservableProperty]
	private bool _IsToggled;

	[ObservableProperty]
	private MarkerInfo? _SelectedMarkerInfo;

	[ObservableProperty]
	private Color? _SelectedColor;

	[ObservableProperty]
	private string _SelectedText;

	public DTACMarkerViewModel()
	{
		_SelectedMarkerInfo = ColorList[0];
		_SelectedColor = _SelectedMarkerInfo.Color;
		_SelectedText = TextList[0];
	}

	partial void OnSelectedMarkerInfoChanged(MarkerInfo? value)
	{
		SelectedColor = value?.Color;
	}

	partial void OnSelectedColorChanged(Color? value)
	{
		SelectedMarkerInfo = ColorList.FirstOrDefault(v => v.Color == value);
	}
}
