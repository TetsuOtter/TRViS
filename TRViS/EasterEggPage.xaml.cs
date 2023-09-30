using TRViS.ViewModels;

namespace TRViS;

public partial class EasterEggPage : ContentPage
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

	public EasterEggPage(EasterEggPageViewModel vm)
	{
		logger.Trace("EasterEggPage Creating (EasterEggPageViewModel: {0})", vm);

		InitializeComponent();

		BindingContext = vm;

		LogFilePathLabel.Text = DirectoryPathProvider.NormalLogFileDirectory.FullName;

		logger.Trace("EasterEggPage Created");
	}
}
