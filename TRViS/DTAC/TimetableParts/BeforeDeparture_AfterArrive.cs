using TRViS.Controls;
using TRViS.Services;

namespace TRViS.DTAC;

public class BeforeDeparture_AfterArrive
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	public readonly RowDefinition RowDefinition = new(DTACElementStyles.BeforeDeparture_AfterArrive_Height);

	readonly BoxView HeaderBoxView = new()
	{
		IsVisible = false,
		Margin = new(0),
	};
	readonly Label HeaderLabel = DTACElementStyles.HeaderLabelStyle<Label>();

	readonly HtmlAutoDetectLabel Label = DTACElementStyles.HtmlAutoDetectLabelStyle<HtmlAutoDetectLabel>();

	Grid Parent { get; }
	bool AlwaysShow { get; }

	public BeforeDeparture_AfterArrive(Grid Parent, string HeaderLabelText)
		: this(Parent, HeaderLabelText, false) { }

	public BeforeDeparture_AfterArrive(Grid Parent, string HeaderLabelText, bool AlwaysShow)
	{
		logger.Trace("Creating... (Parent: {0}, HeaderLabelText: {1}, AlwaysShow: {2})",
			Parent.GetType().Name,
			HeaderLabelText,
			AlwaysShow
		);

		this.Parent = Parent;
		this.AlwaysShow = AlwaysShow;

		Label.HorizontalOptions = LayoutOptions.Start;
		Label.VerticalOptions = LayoutOptions.Start;
		Label.Margin = new(Label.Margin.Left);

		DTACElementStyles.HeaderTextColor.Apply(HeaderLabel, Microsoft.Maui.Controls.Label.TextColorProperty);
		DTACElementStyles.HeaderBackgroundColor.Apply(HeaderBoxView, BoxView.ColorProperty);

		HeaderLabel.Text = HeaderLabelText;

		IsVisible = AlwaysShow;

		logger.Trace("Created");
	}

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

			if (!AlwaysShow)
				IsVisible = !string.IsNullOrWhiteSpace(value);

			logger.Debug("Setting Text to {0}, AlwaysShow: {1}, IsVisible: {2}", value, AlwaysShow, IsVisible);
			Label.Text = value;
		}
	}

	public void AddToParent()
	{
		Parent.Add(HeaderBoxView);
		Parent.Add(HeaderLabel);
		Grid.SetColumn(Label, 1);
		Grid.SetColumnSpan(Label, 7);
		Parent.Add(Label);
	}

	bool _IsVisible = false;
	public bool IsVisible
	{
		get => _IsVisible;
		set
		{
			if (_IsVisible == value)
			{
				logger.Trace("IsVisible is the same({0}), skipping...", value);
				return;
			}

			logger.Debug("Setting IsVisible to {0}", value);
			_IsVisible = value;

			HeaderBoxView.IsVisible
				= HeaderLabel.IsVisible
				= Label.IsVisible
				= value;
		}
	}

	public void SetRow(in int row)
	{
		try
		{
			Grid.SetRow(HeaderBoxView, row);
			Grid.SetRow(HeaderLabel, row);
			Grid.SetRow(Label, row);
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "BeforeDeparture_AfterArrive.SetRow");
			Utils.ExitWithAlert(ex);
		}
	}
}

