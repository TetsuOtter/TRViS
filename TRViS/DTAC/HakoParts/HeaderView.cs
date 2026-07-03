using Microsoft.Maui.Layouts;

using TRViS.Services;

namespace TRViS.DTAC.HakoParts;

public class HeaderView : Grid
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	const double CharSlotHeight = DTACElementStyles.HakoHeaderFontSize;

	readonly ColumnDefinition EdgeColumnDefinition = new(0);

	readonly BoxView backgroundBoxView = new();

	// Each edge's text (e.g. "乗務開始") is rendered as one Label per character via
	// VerticalCharacterLayout — the same tightly-packed layout DiagramHeaderView uses for
	// station names — rather than a single Label with '\n' between characters, so the two
	// headers (which share the same-height row) look consistent.
	readonly AbsoluteLayout leftEdgeCanvas = new();
	readonly AbsoluteLayout rightEdgeCanvas = new();

	string? _leftEdgeText;
	string? _rightEdgeText;

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

		Grid.SetColumn(leftEdgeCanvas, 0);
		Children.Add(leftEdgeCanvas);
		Grid.SetColumn(rightEdgeCanvas, 2);
		Children.Add(rightEdgeCanvas);

		logger.Debug("Created");
	}

	public double EdgeWidth
	{
		get => EdgeColumnDefinition.Width.Value;
		set
		{
			logger.Debug("value: {0} -> {0}", EdgeColumnDefinition.Width.Value, value);
			EdgeColumnDefinition.Width = new(value, GridUnitType.Absolute);
			RebuildEdgeCanvas(leftEdgeCanvas, _leftEdgeText);
			RebuildEdgeCanvas(rightEdgeCanvas, _rightEdgeText);
		}
	}

	public string? LeftEdgeText
	{
		get => _leftEdgeText;
		set
		{
			logger.Debug("value: {0} -> {0}", _leftEdgeText, value);
			_leftEdgeText = value;
			RebuildEdgeCanvas(leftEdgeCanvas, value);
		}
	}

	public string? RightEdgeText
	{
		get => _rightEdgeText;
		set
		{
			logger.Debug("value: {0} -> {0}", _rightEdgeText, value);
			_rightEdgeText = value;
			RebuildEdgeCanvas(rightEdgeCanvas, value);
		}
	}

	void RebuildEdgeCanvas(AbsoluteLayout canvas, string? text)
	{
		canvas.Children.Clear();
		if (string.IsNullOrEmpty(text))
			return;

		double width = EdgeColumnDefinition.Width.Value;

		foreach ((char character, double centerY) in VerticalCharacterLayout.ComputePositions(text, DiagramHeaderView.HeaderHeight, CharSlotHeight))
		{
			Label label = DTACElementStyles.HeaderLabelStyle<Label>();
			label.Text = character.ToString();
			label.Margin = 0;
			label.FontSize = DTACElementStyles.HakoHeaderFontSize;
			label.HorizontalTextAlignment = TextAlignment.Center;
			label.VerticalTextAlignment = TextAlignment.Center;

			AbsoluteLayout.SetLayoutBounds(label, new Rect(0, centerY - (CharSlotHeight / 2), width, CharSlotHeight));
			AbsoluteLayout.SetLayoutFlags(label, AbsoluteLayoutFlags.None);
			canvas.Children.Add(label);
		}
	}
}
