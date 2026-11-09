namespace Poseidon.Api.Tests;

// Single-shot logical timers for provider unit tests only. Production uses the
// ordinary System TimeProvider; protocol assertions do not measure host load.
internal sealed class CertificateExtractionDeadlineClock:TimeProvider
{
    private readonly object sync=new();
    private readonly List<DeadlineTimer> timers=[];
    private long ticks;
    public override long TimestampFrequency=>TimeSpan.TicksPerSecond;
    public override long GetTimestamp()=>Volatile.Read(ref ticks);
    public override DateTimeOffset GetUtcNow()=>DateTimeOffset.UnixEpoch.AddTicks(Volatile.Read(ref ticks));
    internal int ActiveTimers {get{lock(sync)return timers.Count(t=>!t.Disposed && t.Due!=long.MaxValue);}}
    public override ITimer CreateTimer(TimerCallback callback,object? state,TimeSpan dueTime,TimeSpan period)
    {
        if(period!=Timeout.InfiniteTimeSpan)throw new NotSupportedException("single_shot_test_timer_only");
        lock(sync){var timer=new DeadlineTimer(this,callback,state);timer.Change(dueTime,period);timers.Add(timer);return timer;}
    }
    internal void Advance(TimeSpan elapsed)
    {
        if(elapsed<TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(elapsed));
        DeadlineTimer[] ready;
        lock(sync){ticks=checked(ticks+elapsed.Ticks);ready=timers.Where(t=>!t.Disposed && t.Due<=ticks).ToArray();foreach(var timer in ready)timer.Due=long.MaxValue;}
        foreach(var timer in ready)timer.Fire();
    }
    private sealed class DeadlineTimer(CertificateExtractionDeadlineClock owner,TimerCallback callback,object? state):ITimer
    {
        internal long Due=long.MaxValue;internal bool Disposed;
        internal void Fire()=>callback(state);
        public bool Change(TimeSpan dueTime,TimeSpan period)
        {
            if(period!=Timeout.InfiniteTimeSpan || dueTime<TimeSpan.Zero && dueTime!=Timeout.InfiniteTimeSpan)throw new ArgumentOutOfRangeException(nameof(dueTime));
            lock(owner.sync){if(Disposed)return false;Due=dueTime==Timeout.InfiniteTimeSpan?long.MaxValue:checked(owner.ticks+dueTime.Ticks);return true;}
        }
        public void Dispose(){lock(owner.sync)Disposed=true;}
        public ValueTask DisposeAsync(){Dispose();return ValueTask.CompletedTask;}
    }
}
