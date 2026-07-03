using Microsoft.Maui.Layouts;

using TRViS.DTAC.Logic.Layout;
using TRViS.Services;

namespace TRViS.DTAC.HakoParts;

/// <summary>
/// Sticky (non-scrolling) header row for the diagram-style ("ハコ図") rendering of the Hako tab.
/// Renders one text slot per grid line, at the exact same fixed spacing/position as
/// <see cref="DiagramView"/>'s own grid lines, so station names stay aligned with their line
/// even while the (vertically scrollable) <see cref="DiagramView"/> content below is scrolled.
/// Only lines that correspond to an actual turn-back station get a name; the rest stay blank.
/// </summary>
public class DiagramHeaderView : Grid
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	public const double HeaderHeight = 100;

	// Per-character slot height, based directly on the label's actual font size rather than an
	// arbitrary quarter of HeaderHeight (which is taller than one line of text actually needs,
	// and than a runtime Label.Measure() call, which isn't reliable this early in the view's
	// lifecycle) — using FontSize keeps characters visually touching with no dead space between
	// them, matching a 4-character name's natural per-line height.
	const double CharSlotHeight = DTACElementStyles.HakoHeaderFontSize;

	readonly BoxView backgroundBoxView = new();
	readonly AbsoluteLayout _canvas = new();

	public DiagramHeaderView()
	{
		logger.Debug("Creating...");

		DTACElementStyles.HeaderBackgroundColor.Apply(backgroundBoxView, BoxView.ColorProperty);
		backgroundBoxView.Margin = new(-100, 0);
		backgroundBoxView.Shadow = new()
		{
			Brush = Colors.Black,
			Offset = new(0, 1),
			Radius = 1,
			Opacity = 0.4f,
		};
		Children.Add(backgroundBoxView);

		_canvas.HorizontalOptions = LayoutOptions.Center;
		_canvas.VerticalOptions = LayoutOptions.Start;
		// WidthRequest is set per-work in SetColumns: the grid width now varies with the screen
		// width (see DiagramView.GridWidth), so it can't be baked in from a const at construction.
		_canvas.HeightRequest = HeaderHeight;
		Children.Add(_canvas);

		HeightRequest = HeaderHeight;

		logger.Debug("Created");
	}

	/// <summary>
	/// Rebuilds the header's text slots from the given turn-back station columns (as computed by
	/// <see cref="HakoDiagramLayoutCalculator.BuildStationColumns"/> — same instances
	/// <see cref="DiagramView"/> uses for its own grid, via <see cref="DiagramView.StationColumns"/>).
	/// <paramref name="gridWidth"/> must match <see cref="DiagramView.GridWidth"/> so this header and
	/// the diagram below it center identically and stay column-aligned.
	/// </summary>
	public void SetColumns(IReadOnlyList<HakoDiagramLayoutCalculator.StationColumn> columns, double gridWidth)
	{
		_canvas.WidthRequest = gridWidth;
		_canvas.Children.Clear();

		foreach (HakoDiagramLayoutCalculator.StationColumn column in columns)
		{
			double x = column.LineIndex * DiagramView.LineSpacing;
			string name = column.StationName;

			foreach ((char character, double centerY) in VerticalCharacterLayout.ComputePositions(name, HeaderHeight, CharSlotHeight))
			{
				Label label = DTACElementStyles.HeaderLabelStyle<Label>();
				label.Text = character.ToString();
				label.Margin = 0;
				label.FontSize = DTACElementStyles.HakoHeaderFontSize;
				label.HorizontalTextAlignment = TextAlignment.Center;
				label.VerticalTextAlignment = TextAlignment.Center;

				AbsoluteLayout.SetLayoutBounds(label, new Rect(x - (DiagramView.LineSpacing / 2), centerY - (CharSlotHeight / 2), DiagramView.LineSpacing, CharSlotHeight));
				AbsoluteLayout.SetLayoutFlags(label, AbsoluteLayoutFlags.None);
				_canvas.Children.Add(label);
			}
		}
	}
}
