using TRViS.DTAC.Adapters;
using TRViS.DTAC.HakoParts;
using TRViS.DTAC.Logic.Layout;
using TRViS.DTAC.Logic.Presenter;
using TRViS.IO.Models;
using TRViS.Services;
using TRViS.Utils;

namespace TRViS.DTAC;

public partial class Hako : Grid
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	readonly HeaderView headerView = [];
	readonly DiagramHeaderView diagramHeaderView = new();

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
		headerView.LeftEdgeText = "乗務開始";
		headerView.RightEdgeText = "乗務終了";
		Children.Add(headerView);

		Grid.SetRow(diagramHeaderView, 1);
		// Never IsVisible=false (see UpdateLayoutMode) — start hidden via Opacity instead so it
		// still participates in the initial layout pass.
		diagramHeaderView.Opacity = 0;
		diagramHeaderView.InputTransparent = true;
		Children.Add(diagramHeaderView);

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

		DiagramView.DataChanged += (_, _) => UpdateLayoutMode();
		DiagramView.DataChanged += (_, _) => diagramHeaderView.SetColumns(DiagramView.StationColumns, DiagramView.GridWidth);
		SizeChanged += (_, _) => UpdateLayoutMode();
		// Lets the grid's dashed lines fill all the way down to the bottom of the visible
		// screen (not just to the last train row) even when there are too few trains to
		// naturally fill the viewport — see DiagramView.ViewportHeight.
		DiagramViewScrollView.SizeChanged += (_, _) => DiagramView.ViewportHeight = DiagramViewScrollView.Height;
		UpdateLayoutMode();
		diagramHeaderView.SetColumns(DiagramView.StationColumns, DiagramView.GridWidth);

#if UI_TEST
		// XCUITest cannot address DiagramView's internal ToggleButtons: iOS flattens the
		// entire diagram canvas subtree into a single unlabeled accessibility Image, so
		// even DiagramView's own Grid root shows up with zero children. Rather than
		// blocking on that (separate) accessibility bug, expose one hidden proxy button
		// per train at the Hako level, outside the ScrollView that gets flattened. Reuses
		// the same AutomationId the (invisible) production ToggleButton already carries,
		// so DTACViewHostPageObject.selectHakoTrain needs no changes.
		DiagramView.DataChanged += (_, _) => RebuildTrainSelectSeams();
		RebuildTrainSelectSeams();
#endif

		logger.Trace("Created");
	}

#if UI_TEST
	readonly List<Button> _trainSelectSeams = [];

	void RebuildTrainSelectSeams()
	{
		foreach (Button seam in _trainSelectSeams)
			Children.Remove(seam);
		_trainSelectSeams.Clear();

		IReadOnlyList<TrainData> trains = InstanceManager.AppViewModel.OrderedTrainDataList ?? [];
		for (int i = 0; i < trains.Count; i++)
		{
			TrainData train = trains[i];
			if (train.TrainNumber is null)
				continue;

			Button seam = new()
			{
				AutomationId = $"DTAC.HakoDiagram.{train.TrainNumber}",
				HorizontalOptions = LayoutOptions.Start,
				VerticalOptions = LayoutOptions.Start,
				WidthRequest = 24,
				HeightRequest = 24,
				BackgroundColor = Colors.Transparent,
				BorderColor = Colors.Transparent,
				Padding = 0,
				Margin = new Thickness(0, i * 24, 0, 0),
			};
			seam.Clicked += (_, _) => InstanceManager.AppViewModel.SelectedTrainData = train;
			Grid.SetRow(seam, 2);
			Children.Add(seam);
			_trainSelectSeams.Add(seam);
		}
	}
#endif

	void UpdateLayoutMode()
	{
		try
		{
			// The grid grows/shrinks with the available width (more lines on wider screens, fewer on
			// narrow ones), so push the width-derived line count into the view before deciding whether
			// the diagram can hold this work's turn-back stations at all.
			int gridLineCount = HakoDiagramLayoutCalculator.CalculateGridLineCount(Width);
			DiagramView.GridLineCount = gridLineCount;

			bool useDiagram = HakoDiagramLayoutCalculator.ShouldUseDiagramLayout(Width, DiagramView.TurnBackStationCount);

			logger.Debug("UpdateLayoutMode: Width={0}, GridLineCount={1}, TurnBackStationCount={2}, useDiagram={3}", Width, gridLineCount, DiagramView.TurnBackStationCount, useDiagram);

			SimpleViewScrollView.IsVisible = !useDiagram;
			DiagramViewScrollView.IsVisible = useDiagram;

			// headerView/diagramHeaderView are deliberately never IsVisible=false: a view that
			// starts (or stays) collapsed is skipped by MAUI's layout pass entirely, so the first
			// time it's flipped to visible its background can render with a stale/zero-ish frame
			// (same class of bug as the iOS 26 MAUI layout issue #34273/#34369 the
			// OrientationService.InvalidateMAUILayout workaround addresses elsewhere in this repo —
			// an InvalidateMeasure() call here was tried first and did not fix it, since the header
			// was still going through an IsVisible=false state at some point). Toggling Opacity
			// instead keeps both headers permanently participating in layout — only one is ever
			// visually shown/hittable — so the "laid out while hidden" state can never occur.
			headerView.Opacity = useDiagram ? 0 : 1;
			headerView.InputTransparent = useDiagram;
			diagramHeaderView.Opacity = useDiagram ? 1 : 0;
			diagramHeaderView.InputTransparent = !useDiagram;
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "Hako.UpdateLayoutMode");
			Util.ExitWithAlertAsync(ex);
		}
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
