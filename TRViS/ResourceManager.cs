using TRViS.Services;

namespace TRViS;

public class ResourceManager
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	public static ResourceManager Current { get; } = new();

	public enum AssetName
	{
		UNKNOWN,
		PrivacyPolicy_md,
	}

	static string ToFileName(AssetName assetName)
		=> assetName switch
		{
			AssetName.PrivacyPolicy_md => "PrivacyPolicy.md",
			_ => throw new NotImplementedException(),
		};

	readonly Dictionary<AssetName, string> Resources = new();

	public async Task<string> LoadAssetAsync(AssetName assetName)
	{
		if (Resources.TryGetValue(assetName, out string? value))
			return value;

		string fileName = ToFileName(assetName);
		bool isExist = await FileSystem.AppPackageFileExistsAsync(fileName);
		if (!isExist)
		{
			logger.Warn("File not found: '{0}'", fileName);
			return string.Empty;
		}

		try
		{
			using Stream stream = await FileSystem.OpenAppPackageFileAsync(fileName);
			using StreamReader reader = new(stream);
			string fileContent = await reader.ReadToEndAsync();
			Resources[assetName] = fileContent;
			return fileContent;
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Open file failed: '{0}'", fileName);
			return string.Empty;
		}
	}
}
