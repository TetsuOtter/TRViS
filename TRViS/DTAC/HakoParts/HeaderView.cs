namespace TRViS.DTAC.HakoParts;

public class HeaderView : Grid
{
  private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

  readonly ColumnDefinition EdgeColumnDefinition = new(0);

  readonly BoxView backgroundBoxView = new();

  readonly Label leftEdgeLabel = DTACElementStyles.HeaderLabelStyle<Label>();
  readonly Label rightEdgeLabel = DTACElementStyles.HeaderLabelStyle<Label>();

  public HeaderView()
  {
    logger.Debug("Creating...");

    ColumnDefinitions.Add(EdgeColumnDefinition);
    ColumnDefinitions.Add(new(new(1, GridUnitType.Star)));
    ColumnDefinitions.Add(EdgeColumnDefinition);

    DTACElementStyles.HeaderBackgroundColor.Apply(backgroundBoxView, BoxView.ColorProperty);
    Grid.SetColumnSpan(backgroundBoxView, 3);
    backgroundBoxView.Margin = new(-100, 0);
    backgroundBoxView.Shadow = new()
    {
      Brush = Colors.Black,
      Offset = new(0, 1),
      Radius = 1,
      Opacity = 0.4f,
    };
    Children.Add(backgroundBoxView);

    Grid.SetColumn(leftEdgeLabel, 0);
    Children.Add(leftEdgeLabel);
    Grid.SetColumn(rightEdgeLabel, 2);
    Children.Add(rightEdgeLabel);

    logger.Debug("Created");
  }

  public double EdgeWidth
  {
    get => EdgeColumnDefinition.Width.Value;
    set
    {
      logger.Debug("value: {0} -> {0}", EdgeColumnDefinition.Width.Value, value);
      EdgeColumnDefinition.Width = new(value, GridUnitType.Absolute);
    }
  }

  public string? LeftEdgeText
  {
    get => leftEdgeLabel.Text;
    set
    {
      logger.Debug("value: {0} -> {0}", leftEdgeLabel.Text, value);
      leftEdgeLabel.Text = value;
    }
  }

  public string? RightEdgeText
  {
    get => rightEdgeLabel.Text;
    set
    {
      logger.Debug("value: {0} -> {0}", rightEdgeLabel.Text, value);
      rightEdgeLabel.Text = value;
    }
  }
}
