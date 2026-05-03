using TRViS.DTAC.Logic.Abstractions;
using TRViS.DTAC.Logic.Presenter;

namespace TRViS.DTAC.Logic.Tests;

public class HakoPresenterTests
{
	#region Fake / Stub

	private class FakeCrashLogger : IDtacCrashLogger
	{
		public List<(Exception ex, string? context)> Calls { get; } = [];

		public void Log(Exception ex, string? context = null)
			=> Calls.Add((ex, context));
	}

	private static HakoPresenter CreatePresenter(out FakeCrashLogger logger)
	{
		logger = new FakeCrashLogger();
		return new HakoPresenter(logger);
	}

	#endregion

	// --- constructor / initial state ---

	[Fact]
	public void InitialState_AffectDateText_IsEmpty()
	{
		var presenter = CreatePresenter(out _);
		Assert.Equal(string.Empty, presenter.CurrentState.AffectDateText);
	}

	[Fact]
	public void InitialState_WorkInfoText_IsEmpty()
	{
		var presenter = CreatePresenter(out _);
		Assert.Equal(string.Empty, presenter.CurrentState.WorkInfoText);
	}

	[Fact]
	public void InitialState_IsSimpleViewBusy_IsFalse()
	{
		var presenter = CreatePresenter(out _);
		Assert.False(presenter.CurrentState.IsSimpleViewBusy);
	}

	// --- OnAffectDateChanged ---

	[Fact]
	public void OnAffectDateChanged_SetsFormattedText()
	{
		var presenter = CreatePresenter(out _);
		presenter.OnAffectDateChanged("2025-05-03");

		Assert.Equal(HakoPresenter.AffectDateLabelTextPrefix + "2025-05-03", presenter.CurrentState.AffectDateText);
	}

	[Fact]
	public void OnAffectDateChanged_WithNull_ContainsPrefixOnly()
	{
		var presenter = CreatePresenter(out _);
		presenter.OnAffectDateChanged(null);

		Assert.Equal(HakoPresenter.AffectDateLabelTextPrefix, presenter.CurrentState.AffectDateText);
	}

	[Fact]
	public void OnAffectDateChanged_WithEmpty_ContainsPrefixOnly()
	{
		var presenter = CreatePresenter(out _);
		presenter.OnAffectDateChanged("");

		Assert.Equal(HakoPresenter.AffectDateLabelTextPrefix, presenter.CurrentState.AffectDateText);
	}

	[Fact]
	public void OnAffectDateChanged_RaisesStateChanged()
	{
		var presenter = CreatePresenter(out _);
		HakoStateChangedEventArgs? args = null;
		presenter.StateChanged += (_, e) => args = e;

		presenter.OnAffectDateChanged("2025-05-03");

		Assert.NotNull(args);
		Assert.True(args.Changed.HasFlag(HakoStateSection.AffectDate));
	}

	// --- prefix constant ---

	[Fact]
	public void AffectDateLabelTextPrefix_HasExpectedValue()
	{
		// The prefix includes the newline so that the date appears on the second line.
		Assert.Equal("行路施行日\n", HakoPresenter.AffectDateLabelTextPrefix);
	}

	// --- OnWorkNameChanged / OnWorkSpaceNameChanged ---

	[Fact]
	public void OnWorkNameChanged_SetsWorkInfoText()
	{
		var presenter = CreatePresenter(out _);
		presenter.OnWorkNameChanged("列車A");

		Assert.Equal("列車A\n", presenter.CurrentState.WorkInfoText);
	}

	[Fact]
	public void OnWorkSpaceNameChanged_SetsWorkInfoText()
	{
		var presenter = CreatePresenter(out _);
		presenter.OnWorkSpaceNameChanged("東京区");

		Assert.Equal("\n東京区", presenter.CurrentState.WorkInfoText);
	}

	[Fact]
	public void WorkInfo_BothNameAndSpace_CombinedWithNewline()
	{
		var presenter = CreatePresenter(out _);
		presenter.OnWorkNameChanged("列車A");
		presenter.OnWorkSpaceNameChanged("東京区");

		Assert.Equal("列車A\n東京区", presenter.CurrentState.WorkInfoText);
	}

	[Fact]
	public void OnWorkNameChanged_WithNull_NullAppearsInText()
	{
		var presenter = CreatePresenter(out _);
		presenter.OnWorkSpaceNameChanged("東京区");
		presenter.OnWorkNameChanged(null);

		Assert.Equal("\n東京区", presenter.CurrentState.WorkInfoText);
	}

	[Fact]
	public void OnWorkNameChanged_RaisesStateChanged()
	{
		var presenter = CreatePresenter(out _);
		HakoStateChangedEventArgs? args = null;
		presenter.StateChanged += (_, e) => args = e;

		presenter.OnWorkNameChanged("列車A");

		Assert.NotNull(args);
		Assert.True(args.Changed.HasFlag(HakoStateSection.WorkInfo));
	}

	// --- OnSimpleViewBusyChanged ---

	[Fact]
	public void OnSimpleViewBusyChanged_True_SetsStateTrue()
	{
		var presenter = CreatePresenter(out _);
		presenter.OnSimpleViewBusyChanged(true);

		Assert.True(presenter.CurrentState.IsSimpleViewBusy);
	}

	[Fact]
	public void OnSimpleViewBusyChanged_False_SetsStateFalse()
	{
		var presenter = CreatePresenter(out _);
		presenter.OnSimpleViewBusyChanged(true);
		presenter.OnSimpleViewBusyChanged(false);

		Assert.False(presenter.CurrentState.IsSimpleViewBusy);
	}

	[Fact]
	public void OnSimpleViewBusyChanged_RaisesStateChanged()
	{
		var presenter = CreatePresenter(out _);
		HakoStateChangedEventArgs? args = null;
		presenter.StateChanged += (_, e) => args = e;

		presenter.OnSimpleViewBusyChanged(true);

		Assert.NotNull(args);
		Assert.True(args.Changed.HasFlag(HakoStateSection.IsSimpleViewBusy));
	}

	// --- LogException ---

	[Fact]
	public void LogException_DelegatesToCrashLogger()
	{
		var presenter = CreatePresenter(out var logger);
		var ex = new InvalidOperationException("test");

		presenter.LogException(ex, "Hako.Test");

		Assert.Single(logger.Calls);
		Assert.Same(ex, logger.Calls[0].ex);
		Assert.Equal("Hako.Test", logger.Calls[0].context);
	}

	[Fact]
	public void LogException_WithNullContext_DelegatesToCrashLogger()
	{
		var presenter = CreatePresenter(out var logger);
		var ex = new Exception("oops");

		presenter.LogException(ex);

		Assert.Single(logger.Calls);
		Assert.Null(logger.Calls[0].context);
	}
}
