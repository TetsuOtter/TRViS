using TRViS.Controls;
using TRViS.Services;

namespace TRViS.DTAC;

public class AfterRemarks
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	readonly Grid Parent;

	public AfterRemarks(Grid Parent)
	{
		logger.Trace("Creating... (Parent: {0})", Parent.GetType().Name);

		this.Parent = Parent;

		IsVisible = false;

		logger.Trace("Created");
	}

	readonly HtmlAutoDetectLabel Label = DTACElementStyles.AfterRemarksStyle<HtmlAutoDetectLabel>();

	public string Text
	{
		get => Label.Text ?? string.Empty;
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
		try
		{
			logger.Trace("Setting Row to {0}", row);
			Grid.SetRow(Label, row);
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "AfterRemarks.SetRow");
			Utils.ExitWithAlert(ex);
		}
	}
}

