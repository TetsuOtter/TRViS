using DependencyPropertyGenerator;

namespace TRViS.Controls;

[DependencyProperty<string>("FileName")]
public partial class SimpleMarkdownView : ContentView
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
	readonly SimpleMarkdownLabel markdownLabel;

	string currentFileName = string.Empty;

	public SimpleMarkdownView()
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

	Task LoadMarkdownFile()
		=> LoadMarkdownFile(FileName ?? string.Empty);
	async Task LoadMarkdownFile(string fileName)
	{
		bool isExist = await FileSystem.AppPackageFileExistsAsync(fileName);
		if (!isExist)
		{
			logger.Warn("File not found: '{0}'", fileName);
			return;
		}

		try
		{
			using Stream stream = await FileSystem.OpenAppPackageFileAsync(fileName);
			using StreamReader reader = new(stream);
			string fileContent = await reader.ReadToEndAsync();
			markdownLabel.MarkdownFileContent = fileContent;
			currentFileName = fileName;
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Open file failed: '{0}'", fileName);
		}
	}

	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);

		if (currentFileName == FileName || string.IsNullOrEmpty(FileName))
			return;

		Task.Run(LoadMarkdownFile);
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

		Task.Run(LoadMarkdownFile);
	}
}
