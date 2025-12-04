using CommunityToolkit.Mvvm.ComponentModel;

using TRViS.MyAppCustomizables;
using TRViS.Services;

namespace TRViS.ViewModels;

public record MarkerInfo(string Name, Color Color);

public partial class DTACMarkerViewModel : ObservableObject
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	public static readonly IReadOnlyList<MarkerInfo> ColorListDefaultValue = new List<MarkerInfo>()
	{
		// Red
		new("赤", new(0xF0, 0x40, 0x20)),

		// Lime
		new("緑", new(0x40, 0xF0, 0x20)),

		// Blue
		new("青", new(0x20, 0x40, 0xF0)),

		// Yellow
		new("黄", new(0xf0, 0xf0, 0x40)),

		// Brown
		new("茶", new(0x80, 0x40, 0x20)),
	};
	public List<MarkerInfo> ColorList { get; } = [.. ColorListDefaultValue];

	public static readonly IReadOnlyList<string> TextListDefaultValue = new List<string>()
	{
		string.Empty,
		"停車",
		"徐行",
		"両数",
		"規制",
		"時変",
		"合図",
	};
	public List<string> TextList { get; } = [.. TextListDefaultValue];

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

	public void UpdateList(SettingFileStructure settingFile)
	{
		logger.Trace("Executing... Colors Count {0} -> {1} / Texts Count {2} -> {3}",
			ColorList.Count,
			settingFile.MarkerColors.Count,
			TextList.Count,
			settingFile.MarkerTexts.Length
		);

		ColorList.Clear();
		ColorList.AddRange(settingFile.MarkerColors.Select(static v => new MarkerInfo(v.Key, v.Value.ToColor())));
		if (SelectedMarkerInfo is not null && !ColorList.Contains(SelectedMarkerInfo))
		{
			logger.Debug("Current SelectedMarkerInfo is not in ColorList");
			int newIndex = ColorList.FindIndex(v => v.Name == SelectedMarkerInfo.Name || v.Color == SelectedMarkerInfo.Color);
			if (0 <= newIndex)
			{
				SelectedMarkerInfo = ColorList[newIndex];
				logger.Debug("Current ColorName or ColorValue Found in ColorList (newIndex: {0}, Obj:{1})", newIndex, SelectedMarkerInfo);
			}
			else
			{
				logger.Debug("Current ColorName or ColorValue Not Found in ColorList");
				SelectedMarkerInfo = ColorList.FirstOrDefault();
				logger.Debug("Selected First Item in ColorList, or null (Obj:{0})", SelectedMarkerInfo);
			}
		}

		TextList.Clear();
		TextList.AddRange(settingFile.MarkerTexts);
		if (SelectedText is not null && !TextList.Contains(SelectedText))
		{
			logger.Debug("Current SelectedText is not in TextList");
			int newIndex = TextList.FindIndex(v => v == SelectedText);
			if (0 <= newIndex)
			{
				SelectedText = TextList[newIndex];
				logger.Debug("Current Text Found in TextList (newIndex: {0}, Obj:{1})", newIndex, SelectedText);
			}
			else
			{
				SelectedText = string.Empty;
				logger.Debug("Current Text Not Found in TextList -> set to string.Empty");
			}
		}

		logger.Trace("Completed");
	}

	public void SetToSettings(SettingFileStructure settingFile)
	{
		logger.Trace("Executing...");

		settingFile.MarkerColors = ColorList.ToDictionary(static v => v.Name, static v => new ColorSetting(v.Color));
		settingFile.MarkerTexts = TextList.ToArray();

		logger.Trace("Completed");
	}
}
