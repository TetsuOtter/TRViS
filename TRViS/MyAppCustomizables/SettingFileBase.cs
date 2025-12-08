using System.Text.Json;
using System.Text.Json.Serialization;

using TRViS.Services;
using TRViS.Utils;
using TRViS.ViewModels;

namespace TRViS.MyAppCustomizables;

/// <summary>
/// カスタマイズ設定ファイルの構造が定義されたクラスです。
/// </summary>
public partial class SettingFileStructure
{
	[JsonSourceGenerationOptions(WriteIndented = true)]
	[JsonSerializable(typeof(SettingFileStructure))]
	internal partial class SourceGenerationContext : JsonSerializerContext { }
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	/// <summary>
	/// タイトルバーの背景色
	/// </summary>
	public ColorSetting TitleColor { get; set; } = new();

	/// <summary>
	/// マーカーの色一覧
	/// </summary>
	public Dictionary<string, ColorSetting> MarkerColors { get; set; } = [];

	/// <summary>
	/// マーカーのテキスト一覧
	/// </summary>
	public string[] MarkerTexts { get; set; } = [];

	/// <summary>
	/// 位置情報サービスの位置情報サービスの位置測位間隔 (秒)
	/// </summary>
	/// <remarks>
	/// この値は、0.1 (秒) 以上である必要があります。
	/// </remarks>
	public double LocationServiceInterval_Seconds { get; set; } = 1;
	public static readonly double MinimumLocationServiceIntervalValue = 0.1;

	/// <summary>
	/// アプリのテーマ初期値 (null/Unspecifiedの場合は、システムの設定に従う)
	/// </summary>
	public AppTheme? InitialTheme { get; set; } = AppTheme.Unspecified;

	/// <summary>
	/// 端末を横向きにした際に地図を表示するかどうか (位置情報デバッグ用)
	/// </summary>
	public bool ShowMapWhenLandscape { get; set; } = false;

	/// <summary>
	/// 運行中に画面OFFを抑制するかどうか (スマートフォン・タブレット用)
	/// </summary>
	public bool KeepScreenOnWhenRunning { get; set; } = false;

	/// <summary>
	/// 時間の進み方の倍率
	/// </summary>
	public TimeProgressionRate TimeProgressionRate { get; set; } = TimeProgressionRate.Normal;

	public override string ToString()
	{
		return
			$"TitleColor: {TitleColor},"
			+ $"MarkerColors: {MarkerColors},"
			+ $"MarkerTexts: {MarkerTexts},"
			+ $"LocationServiceInterval: {LocationServiceInterval_Seconds}[s],"
			+ $"InitialTheme: {InitialTheme},"
			+ $"ShowMapWhenLandscape: {ShowMapWhenLandscape},"
			+ $"KeepScreenOnWhenRunning: {KeepScreenOnWhenRunning},"
			+ $"TimeProgressionRate: {TimeProgressionRate}"
		;
	}

	public bool Equals(SettingFileStructure? v)
	{
		if (v is null)
			return false;

		return
			TitleColor.Equals(v.TitleColor)
			&& MarkerColors.Equals(v.MarkerColors)
			&& MarkerTexts.Equals(v.MarkerTexts)
			&& LocationServiceInterval_Seconds.Equals(v.LocationServiceInterval_Seconds)
			&& InitialTheme.Equals(v.InitialTheme)
			&& ShowMapWhenLandscape.Equals(v.ShowMapWhenLandscape)
			&& KeepScreenOnWhenRunning.Equals(v.KeepScreenOnWhenRunning)
			&& TimeProgressionRate.Equals(v.TimeProgressionRate)
		;
	}

	public override bool Equals(object? obj)
		=> Equals(obj as SettingFileStructure);

	public override int GetHashCode()
		=> HashCode.Combine(TitleColor, MarkerColors, MarkerTexts, LocationServiceInterval_Seconds, InitialTheme, ShowMapWhenLandscape, KeepScreenOnWhenRunning, TimeProgressionRate);

	#region Loaders

	public static readonly string SettingFileName = "MyAppCustomizables.json";
	static readonly DirectoryInfo settingFileDirectoryInfo = DirectoryPathProvider.InternalFilesDirectory;
	static readonly FileInfo settingFileInfo = new(Path.Combine(DirectoryPathProvider.InternalFilesDirectory.FullName, SettingFileName));
	public static readonly string settingFileCreatedMsg = "created";
	public static async Task<(SettingFileStructure, string?)> LoadFromJsonFileOrCreateAsync()
	{
		if (!settingFileDirectoryInfo.Exists)
		{
			logger.Info("Creating InternalFiles directory... (path: {0})", settingFileDirectoryInfo.FullName);
			settingFileDirectoryInfo.Create();
		}

		if (!settingFileInfo.Exists)
		{
			logger.Info("Creating setting file... (path: {0})", settingFileInfo.FullName);
			SettingFileStructure setting = new()
			{
				MarkerTexts = [.. DTACMarkerViewModel.TextListDefaultValue],
				MarkerColors = DTACMarkerViewModel.ColorListDefaultValue.ToDictionary(static v => v.Name, static v => new ColorSetting(v.Color)),
			};
			using FileStream jsonStream = File.Create(settingFileInfo.FullName);
			await setting.SaveToJsonFileAsync(jsonStream);
			return (setting, settingFileCreatedMsg);
		}

		try
		{
			logger.Info("Loading setting file... (path: {0})", settingFileInfo.FullName);
			using FileStream jsonStream = File.OpenRead(settingFileInfo.FullName);
			return await LoadFromJsonAsync(jsonStream);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Failed to load setting file.");
			return (new(), "設定ファイルの読み込みに失敗しました。" + ex.Message);
		}
	}

	public static async Task<(SettingFileStructure, string?)> LoadFromJsonAsync(Stream jsonStream)
	{
		SettingFileStructure? settingFileStructure;

		try
		{
			logger.Info("Loading setting file...");
			settingFileStructure = await JsonSerializer.DeserializeAsync(jsonStream, SourceGenerationContext.Default.SettingFileStructure);
			logger.Trace("Loaded setting file.");
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Failed to load setting file.");
			return (new(), "設定ファイルの読み込みに失敗しました。" + ex.Message);
		}

		if (settingFileStructure is null)
		{
			logger.Warn("Failed to load setting file. (result is null)");
			return (new(), "設定ファイルの読み込みに失敗しました。(結果がnullです)");
		}

		return (settingFileStructure, null);
	}

	public static (SettingFileStructure, string?) LoadFromJson(string jsonString)
	{
		SettingFileStructure? settingFileStructure;

		if (string.IsNullOrWhiteSpace(jsonString))
		{
			logger.Warn("Empty JSON string.");
			return (new(), "JSONファイル文字列が空です");
		}

		try
		{
			logger.Info("Loading setting file...");
			settingFileStructure = JsonSerializer.Deserialize(jsonString, SourceGenerationContext.Default.SettingFileStructure);
			logger.Trace("Loaded setting file.");
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Failed to load setting file.");
			return (new(), "設定ファイルの読み込みに失敗しました。" + ex.Message);
		}

		if (settingFileStructure is null)
		{
			logger.Warn("Failed to load setting file. (result is null)");
			return (new(), "設定ファイルの読み込みに失敗しました。(結果がnullです)");
		}

		return (settingFileStructure, null);
	}

	public string ToJsonString()
		=> JsonSerializer.Serialize(this, SourceGenerationContext.Default.SettingFileStructure);
	public Task SaveToJsonFileAsync(Stream dst)
		=> JsonSerializer.SerializeAsync(dst, this, SourceGenerationContext.Default.SettingFileStructure);
	public async Task SaveToJsonFileAsync()
	{
		FileStream? jsonStream = null;

		try
		{
			settingFileInfo.Refresh();
			if (!settingFileInfo.Exists)
			{
				logger.Info("Creating setting file... (path: {0})", settingFileInfo.FullName);
				jsonStream = settingFileInfo.Create();
			}
			else
			{
				jsonStream = File.Open(settingFileInfo.FullName, FileMode.Truncate);
			}

			await SaveToJsonFileAsync(jsonStream);
		}
		finally
		{
			jsonStream?.Dispose();
		}
	}

	#endregion Loaders
}
