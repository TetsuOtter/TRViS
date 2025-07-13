using CommunityToolkit.Maui.Views;

using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS.DTAC;

public partial class SelectMarkerPopup : Popup
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	public SelectMarkerPopup() : this(InstanceManager.DTACMarkerViewModel) { }

	private readonly Grid rootGrid;
	private readonly Button closeButton;
	private readonly HorizontalStackLayout stackLayout;
	private readonly Border colorListBorder;
	private readonly CollectionView colorListView;
	private readonly Border textListBorder;
	private readonly CollectionView textListView;

	public SelectMarkerPopup(DTACMarkerViewModel viewModel)
	{
		logger.Trace("Creating...");

		BindingContext = viewModel;

		VerticalOptions = Microsoft.Maui.Primitives.LayoutAlignment.Center;
		HorizontalOptions = Microsoft.Maui.Primitives.LayoutAlignment.End;
		Size = new Size(240, 360);

		rootGrid = new Grid
		{
			RowDefinitions = new RowDefinitionCollection
			{
				new RowDefinition(GridLength.Auto),
				new RowDefinition(GridLength.Star)
			}
		};

		closeButton = new Button
		{
			Margin = new Thickness(0, 8, 8, 0),
			Text = "Close",
			HorizontalOptions = LayoutOptions.End
		};
		closeButton.Clicked += OnCloseButtonClicked;
		Grid.SetRow(closeButton, 0);

		colorListView = new CollectionView
		{
			SelectionMode = SelectionMode.Single,
			ItemTemplate = new DataTemplate(() =>
			{
				var border = new Border
				{
					HeightRequest = 32,
					WidthRequest = 32,
					VerticalOptions = LayoutOptions.Center,
					HorizontalOptions = LayoutOptions.Center
				};
				border.SetBinding(Border.BackgroundColorProperty, new Binding("Color"));

				var label = new Label
				{
					TextColor = Colors.Black,
					Background = Color.FromArgb("#AAFFFFFF"),
					HorizontalOptions = LayoutOptions.Center,
					VerticalOptions = LayoutOptions.Center
				};
				label.SetBinding(Label.TextProperty, new Binding("Name"));

				border.Content = label;
				return border;
			})
		};
		colorListView.SetBinding(CollectionView.ItemsSourceProperty, new Binding("ColorList", mode: BindingMode.OneTime));
		colorListView.SetBinding(CollectionView.SelectedItemProperty, new Binding("SelectedMarkerInfo", mode: BindingMode.TwoWay));

		colorListBorder = new Border
		{
			Margin = new Thickness(4),
			Padding = new Thickness(16),
			WidthRequest = 80,
			Content = colorListView
		};
		DTACElementStyles.Instance.TabAreaBGColor.Apply(colorListBorder, Border.BackgroundColorProperty);

		textListView = new CollectionView
		{
			SelectionMode = SelectionMode.Single
		};
		textListView.SetBinding(CollectionView.ItemsSourceProperty, new Binding("TextList", mode: BindingMode.OneTime));
		textListView.SetBinding(CollectionView.SelectedItemProperty, new Binding("SelectedText", mode: BindingMode.TwoWay));

		textListBorder = new Border
		{
			Margin = new Thickness(4),
			Padding = new Thickness(16),
			WidthRequest = 128,
			Content = textListView
		};
		DTACElementStyles.Instance.TabAreaBGColor.Apply(textListBorder, Border.BackgroundColorProperty);

		stackLayout = new HorizontalStackLayout
		{
			Padding = new Thickness(8),
			HorizontalOptions = LayoutOptions.Center
		};
		stackLayout.Children.Add(colorListBorder);
		stackLayout.Children.Add(textListBorder);
		Grid.SetRow(stackLayout, 1);

		rootGrid.Children.Add(closeButton);
		rootGrid.Children.Add(stackLayout);

		Content = rootGrid;

		DTACElementStyles.Instance.DefaultBGColor.Apply(this, ColorProperty);

		logger.Trace("Created");
	}

	async void OnCloseButtonClicked(object? sender, EventArgs e)
	{
		logger.Trace("Closing...");

		await CloseAsync();

		logger.Trace("Closed");
	}
}
