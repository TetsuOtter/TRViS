using TRViS.IO;
using TRViS.Services;

namespace TRViS.Utils;

/// <summary>
/// Handles automatic loading of timetable files from TimetableFileDirectory
/// </summary>
public class DefaultTimetableFileLoader
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	/// <summary>
	/// Attempts to load a timetable file from the TimetableFileDirectory.
	/// Priority: default.json > first available JSON file
	/// </summary>
	/// <param name="cancellationToken">Cancellation token for async operations</param>
	/// <returns>Tuple containing (loader, selectedFilePath, requiresFileSelection)</returns>
	public static async Task<(ILoader? loader, string? selectedFilePath, bool requiresFileSelection)> TryLoadDefaultTimetableAsync(
		CancellationToken cancellationToken = default)
	{
		try
		{
			if (!DirectoryPathProvider.TimetableFileDirectory.Exists)
			{
				logger.Info("TimetableFileDirectory does not exist, creating: {0}",
					DirectoryPathProvider.TimetableFileDirectory.FullName);
				Directory.CreateDirectory(DirectoryPathProvider.TimetableFileDirectory.FullName);
			}

			// Look for default.json first
			string defaultJsonPath = Path.Combine(
				DirectoryPathProvider.TimetableFileDirectory.FullName,
				"default.json"
			);

			if (File.Exists(defaultJsonPath))
			{
				logger.Info("Found default.json at: {0}", defaultJsonPath);
				try
				{
					ILoader loader = await LoaderJson.InitFromFileAsync(defaultJsonPath, cancellationToken);
					logger.Trace("Successfully loaded default.json");
					return (loader, defaultJsonPath, false);
				}
				catch (Exception ex)
				{
					logger.Error(ex, "Failed to load default.json");
					return (null, defaultJsonPath, false);
				}
			}

			// If no default.json, look for other JSON files
			var jsonFiles = DirectoryPathProvider.TimetableFileDirectory
				.GetFiles("*.json")
				.OrderBy(f => f.Name)
				.ToArray();

			if (jsonFiles.Length == 0)
			{
				logger.Info("No JSON files found in TimetableFileDirectory");
				return (null, null, false);
			}

			if (jsonFiles.Length == 1)
			{
				// Only one JSON file, load it automatically
				string filePath = jsonFiles[0].FullName;
				logger.Info("Found single JSON file: {0}", filePath);
				try
				{
					ILoader loader = await LoaderJson.InitFromFileAsync(filePath, cancellationToken);
					logger.Trace("Successfully loaded JSON file: {0}", jsonFiles[0].Name);
					return (loader, filePath, false);
				}
				catch (Exception ex)
				{
					logger.Error(ex, "Failed to load JSON file: {0}", jsonFiles[0].Name);
					return (null, filePath, false);
				}
			}

			// Multiple JSON files found, require user selection
			logger.Info("Found {0} JSON files in TimetableFileDirectory", jsonFiles.Length);
			return (null, null, true);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Error in TryLoadDefaultTimetableAsync");
			return (null, null, false);
		}
	}

	/// <summary>
	/// Gets the list of JSON files in TimetableFileDirectory
	/// </summary>
	/// <returns>Array of FileInfo for JSON files, ordered by name (excluding default.json)</returns>
	public static FileInfo[] GetAvailableJsonFiles()
	{
		try
		{
			if (!DirectoryPathProvider.TimetableFileDirectory.Exists)
			{
				logger.Info("TimetableFileDirectory does not exist, creating: {0}",
					DirectoryPathProvider.TimetableFileDirectory.FullName);
				Directory.CreateDirectory(DirectoryPathProvider.TimetableFileDirectory.FullName);
			}

			return DirectoryPathProvider.TimetableFileDirectory
				.GetFiles("*.json")
				.Where(f => f.Name != "default.json") // Exclude default.json as it's already handled
				.OrderBy(f => f.Name)
				.ToArray();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Error in GetAvailableJsonFiles");
			return [];
		}
	}

	/// <summary>
	/// Loads a specific JSON file from TimetableFileDirectory
	/// </summary>
	/// <param name="filePath">Full path to the file</param>
	/// <param name="cancellationToken">Cancellation token for async operations</param>
	/// <returns>Loaded ILoader or null if loading failed</returns>
	public static async Task<ILoader?> LoadTimetableFileAsync(
		string filePath,
		CancellationToken cancellationToken = default)
	{
		try
		{
			if (!File.Exists(filePath))
			{
				logger.Warn("File does not exist: {0}", filePath);
				return null;
			}

			logger.Info("Loading timetable file: {0}", filePath);
			ILoader loader = await LoaderJson.InitFromFileAsync(filePath, cancellationToken);
			logger.Trace("Successfully loaded timetable file");
			return loader;
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Failed to load timetable file: {0}", filePath);
			return null;
		}
	}
}
