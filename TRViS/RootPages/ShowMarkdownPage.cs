using DependencyPropertyGenerator;

using TRViS.Controls;

namespace TRViS.RootPages;

[DependencyProperty<string>("FileName")]
public partial class ShowMarkdownPage : ContentPage
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
	readonly SimpleMarkdownLabel markdownLabel;

	public ShowMarkdownPage()
	{
		logger.Trace("Creating...");
		ScrollView scrollView = new()
		{
			Padding = new Thickness(10),
		};
		markdownLabel = new();
		scrollView.Content = markdownLabel;
		Content = scrollView;

		logger.Trace("Created.");
	}

	partial void OnFileNameChanged(string? oldValue, string? newValue)
	{
		logger.Debug("OnFileNameChanged: '{0}' -> '{1}'", oldValue, newValue);
		markdownLabel.MarkdownFileContent = string.Empty;
		if (string.IsNullOrEmpty(newValue))
		{
			logger.Debug("newValue is null or empty.");
			return;
		}

		Task.Run(async () => {
			bool isExist = await FileSystem.AppPackageFileExistsAsync(newValue);
			if (!isExist)
			{
				logger.Warn("File not found: '{0}'", newValue);
				return;
			}

			try
			{
				using Stream stream = await FileSystem.OpenAppPackageFileAsync(newValue);
				using StreamReader reader = new(stream);
				string fileContent = await reader.ReadToEndAsync();
				markdownLabel.MarkdownFileContent = fileContent;
			}
			catch (Exception ex)
			{
				logger.Error(ex, "Open file failed: '{0}'", newValue);
			}
		});
	}
}
