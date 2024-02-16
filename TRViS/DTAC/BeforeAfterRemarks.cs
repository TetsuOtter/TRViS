using TRViS.Controls;

namespace TRViS.DTAC;

public class BeforeAfterRemarks
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
	readonly Grid Parent;

	public BeforeAfterRemarks(Grid Parent)
	{
		logger.Trace("Creating... (Parent: {0})", Parent.GetType().Name);

		this.Parent = Parent;

		IsVisible = false;

		Label.Margin = new(32, 0);
		Label.HorizontalOptions = LayoutOptions.Start;

		logger.Trace("Created");
	}

	readonly HtmlAutoDetectLabel Label = DTACElementStyles.LabelStyle<HtmlAutoDetectLabel>();

	public string Text
	{
		get => Label.Text;
		set
		{
			if (Label.Text == value)
			{
				logger.Trace("Text is the same, skipping... ({0})", value);
				return;
			}

			IsVisible = !string.IsNullOrWhiteSpace(value);
			logger.Debug("Setting Text to {0}, IsVisible: {1}", value, IsVisible);

			Label.Text = value;
		}
	}

	public void AddToParent()
	{
		Grid.SetColumn(Label, 2);
		Grid.SetColumnSpan(Label, 6);
		Parent.Add(Label);
	}

	bool _IsVisible = false;
	public bool IsVisible
	{
		get => _IsVisible;
		set
		{
			_IsVisible = value;
			Label.IsVisible = value;
		}
	}

	public void SetRow(in int row)
	{
		logger.Trace("Setting Row to {0}", row);
		Grid.SetRow(Label, row);
	}
}

