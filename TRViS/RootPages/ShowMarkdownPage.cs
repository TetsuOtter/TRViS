using DependencyPropertyGenerator;

using TRViS.Controls;

namespace TRViS.RootPages;

[DependencyProperty<string>("FileName")]
public partial class ShowMarkdownPage : ContentPage
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
	readonly SimpleMarkdownView markdownView;

	public ShowMarkdownPage()
	{
		logger.Trace("Creating...");
		ScrollView scrollView = new()
		{
			Padding = new Thickness(10),
		};
		markdownView = new();
		scrollView.Content = markdownView;
		Content = scrollView;

		logger.Trace("Created.");
	}

	partial void OnFileNameChanged(string? oldValue, string? newValue)
	{
		markdownView.FileName = newValue;
	}
}
