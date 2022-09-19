using TRViS.ViewModels;

namespace TRViS.DTAC;

public partial class VerticalStylePage : ContentPage
{
	static public ColumnDefinitionCollection TimetableColumnWidthCollection => new(
		new(new(60)),
		new(new(136)),
		new(new(132)),
		new(new(132)),
		new(new(60)),
		new(new(60)),
		new(new(1, GridUnitType.Star)),
		new(new(64))
		);

	public VerticalStylePage(AppViewModel viewModel)
	{
		InitializeComponent();

		BindingContext = viewModel;

		if (DeviceInfo.Current.Idiom == DeviceIdiom.Phone || DeviceInfo.Current.Idiom == DeviceIdiom.Unknown)
			Content = new ScrollView()
			{
				Content = this.Content
			};
	}
}
