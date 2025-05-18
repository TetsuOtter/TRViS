using DependencyPropertyGenerator;

using TRViS.Controls;
using TRViS.Services;

namespace TRViS.RootPages;

[DependencyProperty<ResourceManager.AssetName>("FileName", DefaultValue = ResourceManager.AssetName.UNKNOWN)]
public partial class ShowMarkdownPage : ContentPage
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
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

		NavigatedTo += (_, _) =>
		{
			logger.Info("NavigatedTo executing with FileName: '{0}'", FileName);
			markdownView.FileName = FileName;
		};

		logger.Trace("Created.");
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
	}
}
