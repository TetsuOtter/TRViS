using TRViS.DTAC.Adapters;
using TRViS.DTAC.HakoParts;
using TRViS.DTAC.Logic.Presenter;
using TRViS.Services;
using TRViS.Utils;

namespace TRViS.DTAC;

public partial class Hako : Grid
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	readonly HeaderView headerView = [];

	readonly Label AffectDateLabel;
	readonly Label WorkInfoLabel;

	private readonly HakoPresenter _presenter;

	static Label GenAffectDateLabel()
	{
		Label v = DTACElementStyles.AffectDateLabelStyle<Label>();

		SetRow(v, 0);

		return v;
	}
	static Label GenWorkInfoLabel()
	{
		Label v = DTACElementStyles.HakoTabWorkInfoLabelStyle<Label>();

		SetRow(v, 0);

		return v;
	}

	public Hako()
	{
		logger.Trace("Creating...");

		_presenter = PresenterFactory.BuildHakoPresenter();
		_presenter.StateChanged += OnPresenterStateChanged;

		InitializeComponent();

		Grid.SetRow(headerView, 1);
		headerView.EdgeWidth = SimpleView.STA_NAME_TIME_COLUMN_WIDTH;
		headerView.LeftEdgeText = TRViS.Core.StringUtils.InsertBetweenChars("乗務開始".AsSpan(), '\n');
		headerView.RightEdgeText = TRViS.Core.StringUtils.InsertBetweenChars("乗務終了".AsSpan(), '\n');
		Children.Add(headerView);

		AffectDateLabel = GenAffectDateLabel();
		Children.Add(AffectDateLabel);

		WorkInfoLabel = GenWorkInfoLabel();
		Children.Add(WorkInfoLabel);

		// Apply initial state computed by presenter before StateChanged was subscribed.
		AffectDateLabel.Text = _presenter.CurrentState.AffectDateText;
		WorkInfoLabel.Text = _presenter.CurrentState.WorkInfoText;

		SimpleView.SetBinding(
			WidthRequestProperty,
			BindingBase.Create(static (ScrollView x) => x.Width, BindingMode.OneWay, source: headerView)
		);

		SimpleView.IsBusyChanged += (s, _) =>
		{
			if (s is not SimpleView v)
				return;

			logger.Info("IsBusyChanged: {0}", v.IsBusy);

			MainThread.BeginInvokeOnMainThread(() =>
			{
				try
				{
					if (v.IsBusy)
					{
						SimpleViewActivityIndicatorBorder.IsVisible = true;
						SimpleViewActivityIndicatorBorder.FadeToAsync(VerticalStylePage.TimetableViewActivityIndicatorBorderMaxOpacity);
					}
					else
					{
						SimpleViewActivityIndicatorBorder.FadeToAsync(0).ContinueWith((_) =>
						{
							logger.Debug("SimpleViewActivityIndicatorBorder.FadeTo(0) completed");
							SimpleViewActivityIndicatorBorder.IsVisible = false;
						});
					}
				}
				catch (Exception ex)
				{
					logger.Fatal(ex, "Unknown Exception");
					InstanceManager.CrashlyticsWrapper.Log(ex, "Hako.SimpleView.IsBusyChanged");
					Util.ExitWithAlertAsync(ex);
				}
			});
		};

		logger.Trace("Created");
	}

	private void OnPresenterStateChanged(object? sender, HakoStateChangedEventArgs e)
	{
		if (e.Changed.HasFlag(HakoStateSection.AffectDate))
		{
			AffectDateLabel.Text = _presenter.CurrentState.AffectDateText;
		}
		if (e.Changed.HasFlag(HakoStateSection.WorkInfo))
		{
			WorkInfoLabel.Text = _presenter.CurrentState.WorkInfoText;
		}
		// IsSimpleViewBusy is handled directly by the IsBusyChanged handler (animation is View-only).
	}
}
