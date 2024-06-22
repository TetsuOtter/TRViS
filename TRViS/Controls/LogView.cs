using System.Collections.Concurrent;
using System.Text;
using DependencyPropertyGenerator;
using Microsoft.AppCenter.Crashes;

namespace TRViS.Controls;

[DependencyProperty<Priority>(
	"PriorityFilter",
	DefaultValue = Priority.Error | Priority.Warn
)]
public partial class LogView : ScrollView
{
	[Flags]
	public enum Priority
	{
		Debug = 1 << 0,
		Info = 1 << 1,
		Warn = 1 << 2,
		Error = 1 << 3,
	}

	public class Log
	{
		public DateTime UTCTime { get; }
		public Priority Priority { get; }
		public string Text { get; } = string.Empty;

		public Log(Priority priority, string text)
		{
			this.UTCTime = DateTime.UtcNow;
			this.Priority = priority;
			this.Text = text;
		}
		public Log(string text) : this(Priority.Info, text) { }

		public override bool Equals(object? obj)
			=> obj is Log v
				&& this.UTCTime == v.UTCTime
				&& this.Priority == v.Priority
				&& this.Text == v.Text
			;

		public override int GetHashCode()
			=> this.UTCTime.GetHashCode()
				^ this.Priority.GetHashCode()
				^ this.Text.GetHashCode()
			;

		public override string ToString()
			=> $"{this.UTCTime:O}\t[{this.Priority}]: {this.Text}";
	}

	static public EventHandler<Log>? LogAdded;
	static ConcurrentBag<Log> _Logs { get; } = new();
	static public IReadOnlyCollection<Log> Logs => _Logs;
	static public void Add(Log log)
	{
		try
		{
			_Logs.Add(log);
			LogAdded?.Invoke(_Logs, log);
		}
		catch (Exception ex)
		{
			Crashes.TrackError(ex);
		}
	}
	static public void Add(Priority priority, string text)
		=> Add(new Log(priority, text));
	static public void Add(string text)
		=> Add(new Log(text));

	readonly Label label = new();
	StringBuilder builder = new();
	readonly object buildberLock = new();

	public LogView()
	{
		this.Content = label;
		LogAdded += OnLogAdded;
	}

	void OnLogAdded(object? sender, Log log)
	{
		if (!PriorityFilter.HasFlag(log.Priority))
			return;

		lock (buildberLock)
		{
			builder.AppendLine(log.ToString());
		}

		updateLabelText();
	}

	partial void OnPriorityFilterChanged()
		=> Reload();

	public void Reload()
	{
		try
		{
			lock (buildberLock)
			{
				builder.Clear();
				label.Text = string.Empty;

				Priority priority = this.PriorityFilter;
				if (priority != 0)
					builder.AppendJoin('\n', _Logs.Where(v => priority.HasFlag(v.Priority)));
			}

			updateLabelText();
		}
		catch (Exception ex)
		{
			Crashes.TrackError(ex);
			Utils.ExitWithAlert(ex);
		}
	}

	void updateLabelText()
		=> MainThread.BeginInvokeOnMainThread(() => label.Text = builder.ToString());
}
