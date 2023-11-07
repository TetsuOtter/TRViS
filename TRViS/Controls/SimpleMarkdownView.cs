using DependencyPropertyGenerator;

namespace TRViS.Controls;

[DependencyProperty<ResourceManager.AssetName>("FileName", DefaultValue = ResourceManager.AssetName.UNKNOWN)]
public partial class SimpleMarkdownView : ContentView
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
	readonly SimpleMarkdownLabel markdownLabel;

	ResourceManager.AssetName currentFileName = ResourceManager.AssetName.UNKNOWN;

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
		=> LoadMarkdownFile(FileName);
	async Task LoadMarkdownFile(ResourceManager.AssetName fileName)
	{
		try
		{
			markdownLabel.MarkdownFileContent = await ResourceManager.Current.LoadAssetAsync(fileName);
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

		if (currentFileName == FileName)
			return;

		Task.Run(LoadMarkdownFile);
	}

	partial void OnFileNameChanged(ResourceManager.AssetName oldValue, ResourceManager.AssetName newValue)
	{
		logger.Debug("OnFileNameChanged: '{0}' -> '{1}'", oldValue, newValue);
		markdownLabel.MarkdownFileContent = string.Empty;

		Task.Run(LoadMarkdownFile);
	}
}
