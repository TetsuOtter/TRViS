namespace TRViS.DemoServer.Services;

public class TimeSimulationService
{
    public enum TimeSpeed
    {
        Normal = 1,
        Fast30 = 30,
        Fast60 = 60
    }

    private readonly object _lock = new();
    private TimeSpeed _currentSpeed = TimeSpeed.Normal;
    private DateTime _baseTime = DateTime.Now;
    private DateTime _lastUpdate = DateTime.Now;
    private TimeSpan _simulatedOffset = TimeSpan.Zero;
    private bool _isRunning = false;

    public event EventHandler<TimeSimulationUpdate>? TimeUpdated;

    public TimeSpeed CurrentSpeed
    {
        get { lock (_lock) return _currentSpeed; }
        set
        {
            lock (_lock)
            {
                if (_currentSpeed != value)
                {
                    // Adjust simulated time when speed changes
                    UpdateSimulatedTime();
                    _currentSpeed = value;
                    _lastUpdate = DateTime.Now;
                }
            }
        }
    }

    public bool IsRunning
    {
        get { lock (_lock) return _isRunning; }
        set
        {
            lock (_lock)
            {
                if (_isRunning != value)
                {
                    if (value)
                    {
                        _lastUpdate = DateTime.Now;
                    }
                    else
                    {
                        UpdateSimulatedTime();
                    }
                    _isRunning = value;
                }
            }
        }
    }

    public DateTime CurrentSimulatedTime
    {
        get
        {
            lock (_lock)
            {
                if (_isRunning)
                {
                    UpdateSimulatedTime();
                }
                return _baseTime.Add(_simulatedOffset);
            }
        }
    }

    public long CurrentTimeMilliseconds
    {
        get
        {
            var time = CurrentSimulatedTime.TimeOfDay;
            return (long)time.TotalMilliseconds;
        }
    }

    private void UpdateSimulatedTime()
    {
        if (_isRunning)
        {
            var now = DateTime.Now;
            var elapsed = now - _lastUpdate;
            var scaledElapsed = TimeSpan.FromTicks(elapsed.Ticks * (int)_currentSpeed);
            _simulatedOffset += scaledElapsed;
            _lastUpdate = now;
        }
    }

    public void SetSimulatedTime(DateTime time)
    {
        lock (_lock)
        {
            _baseTime = time;
            _simulatedOffset = TimeSpan.Zero;
            _lastUpdate = DateTime.Now;
            NotifyTimeUpdate();
        }
    }

    public void Start()
    {
        IsRunning = true;
        NotifyTimeUpdate();
    }

    public void Stop()
    {
        IsRunning = false;
    }

    public void Reset()
    {
        lock (_lock)
        {
            _baseTime = DateTime.Now;
            _simulatedOffset = TimeSpan.Zero;
            _lastUpdate = DateTime.Now;
            NotifyTimeUpdate();
        }
    }

    private void NotifyTimeUpdate()
    {
        TimeUpdated?.Invoke(this, new TimeSimulationUpdate
        {
            CurrentTime = CurrentSimulatedTime,
            TimeMilliseconds = CurrentTimeMilliseconds,
            Speed = CurrentSpeed,
            IsRunning = IsRunning
        });
    }

    public void Tick()
    {
        lock (_lock)
        {
            if (_isRunning)
            {
                UpdateSimulatedTime();
                NotifyTimeUpdate();
            }
        }
    }
}

public class TimeSimulationUpdate
{
    public DateTime CurrentTime { get; set; }
    public long TimeMilliseconds { get; set; }
    public TimeSimulationService.TimeSpeed Speed { get; set; }
    public bool IsRunning { get; set; }
}
