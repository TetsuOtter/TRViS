using DependencyPropertyGenerator;

using Microsoft.Maui.Controls.Shapes;

using TRViS.Services;

namespace TRViS.DTAC;

[DependencyProperty<double>("FontSize_Large")]
public partial class TimetableHeader : Grid
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	private readonly Label runTimeLabel;
	private readonly Line runTimeSeparator;
	private readonly Label stationNameLabel;
	private readonly Line stationNameSeparator;
	private readonly Border arrivalBorder;
	private readonly Label arrivalLabel;
	private readonly Line arrivalSeparator;
	private readonly Label departureLabel;
	private readonly Line departureSeparator;
	private readonly Label trackLabel;
	private readonly Line trackSeparator;
	private readonly Label limitLabel;
	private readonly Line limitSeparator;
	private readonly Label remarksLabel;
	private readonly MarkerButton markerBtn;

	public TimetableHeader()
	{
		logger.Trace("Creating...");

		runTimeLabel = DTACElementStyles.Instance.HeaderLabelStyle<Label>();
		runTimeLabel.LineBreakMode = LineBreakMode.CharacterWrap;
		runTimeLabel.Text = "運転\n時分";
		Grid.SetColumn(runTimeLabel, 0);

		runTimeSeparator = DTACElementStyles.Instance.VerticalSeparatorLineStyle();
		Grid.SetColumn(runTimeSeparator, 0);

		stationNameLabel = DTACElementStyles.Instance.HeaderLabelStyle<Label>();
		stationNameLabel.Text = "停車場名";
		Grid.SetColumn(stationNameLabel, 1);

		stationNameSeparator = DTACElementStyles.Instance.VerticalSeparatorLineStyle();
		Grid.SetColumn(stationNameSeparator, 1);

		arrivalLabel = DTACElementStyles.Instance.HeaderLabelStyle<Label>();
		arrivalLabel.Margin = new Thickness(0);
		arrivalLabel.Padding = new Thickness(0);
		arrivalLabel.HorizontalOptions = LayoutOptions.Center;
		arrivalLabel.VerticalOptions = LayoutOptions.Center;
		arrivalLabel.FontAttributes = FontAttributes.Bold;
		arrivalLabel.TextColor = Colors.White;
		arrivalLabel.Text = "着";
		arrivalLabel.SetBinding(Label.FontSizeProperty, new Binding("FontSize_Large", source: this));

		arrivalBorder = new Border
		{
			Margin = new Thickness(16, 4),
			Padding = new Thickness(0),
			Stroke = Colors.Transparent,
			BackgroundColor = DTACElementStyles.Instance.DarkerGreen,
			StrokeShape = new RoundRectangle { CornerRadius = 8 },
			Content = arrivalLabel
		};
		Grid.SetColumn(arrivalBorder, 2);

		arrivalSeparator = DTACElementStyles.Instance.VerticalSeparatorLineStyle();
		Grid.SetColumn(arrivalSeparator, 2);

		departureLabel = DTACElementStyles.Instance.HeaderLabelStyle<Label>();
		departureLabel.Text = "発  (通)";
		Grid.SetColumn(departureLabel, 3);

		departureSeparator = DTACElementStyles.Instance.VerticalSeparatorLineStyle();
		Grid.SetColumn(departureSeparator, 3);

		trackLabel = DTACElementStyles.Instance.HeaderLabelStyle<Label>();
		trackLabel.LineBreakMode = LineBreakMode.CharacterWrap;
		trackLabel.Text = "着線\n発線";
		Grid.SetColumn(trackLabel, 4);

		trackSeparator = DTACElementStyles.Instance.VerticalSeparatorLineStyle();
		Grid.SetColumn(trackSeparator, 4);

		limitLabel = DTACElementStyles.Instance.HeaderLabelStyle<Label>();
		limitLabel.LineBreakMode = LineBreakMode.CharacterWrap;
		limitLabel.Text = "制限\n速度";
		Grid.SetColumn(limitLabel, 5);

		limitSeparator = DTACElementStyles.Instance.VerticalSeparatorLineStyle();
		Grid.SetColumn(limitSeparator, 5);

		remarksLabel = DTACElementStyles.Instance.HeaderLabelStyle<Label>();
		remarksLabel.Text = "記事";
		Grid.SetColumn(remarksLabel, 6);

		markerBtn = new MarkerButton();
		Grid.SetColumn(markerBtn, 7);

		Children.Add(runTimeLabel);
		Children.Add(runTimeSeparator);
		Children.Add(stationNameLabel);
		Children.Add(stationNameSeparator);
		Children.Add(arrivalBorder);
		Children.Add(arrivalSeparator);
		Children.Add(departureLabel);
		Children.Add(departureSeparator);
		Children.Add(trackLabel);
		Children.Add(trackSeparator);
		Children.Add(limitLabel);
		Children.Add(limitSeparator);
		Children.Add(remarksLabel);
		Children.Add(markerBtn);

		ColumnDefinitions = InstanceManager.DTACViewHostViewModel.ColumnDefinitionsProvider.TimetableRowColumnDefinitions;
		InstanceManager.DTACViewHostViewModel.ColumnDefinitionsProvider.ViewWidthModeChanged += (sender, e) =>
		{
			OnViewWidthModeChanged();
		};
		OnViewWidthModeChanged();

		logger.Trace("Created");
	}

	private void OnViewWidthModeChanged()
	{
		DTACColumnDefinitionsProvider provider = InstanceManager.DTACViewHostViewModel.ColumnDefinitionsProvider;
		runTimeLabel.IsVisible = provider.IsRunTimeColumnVisible;
		limitLabel.IsVisible = limitSeparator.IsVisible = provider.IsSpeedLimitColumnVisible;
		remarksLabel.IsVisible = provider.IsRemarksColumnVisible;
		markerBtn.IsVisible = provider.IsMarkerColumnVisible;
	}
}
