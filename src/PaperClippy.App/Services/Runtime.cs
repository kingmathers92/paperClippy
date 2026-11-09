using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using PaperClippy.App.Interop;
using PaperClippy.Core.Mascot.Behavior;

namespace PaperClippy.App.Services;

/// <summary>Real time for the brain: a monotonic clock for animation and the wall clock for greetings and quiet hours.</summary>
internal sealed class SystemClock : IClock
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public TimeSpan Now => _stopwatch.Elapsed;

    public DateTime LocalNow => DateTime.Now;
}

/// <summary>One-shot timers on the UI thread. Each schedule is a DispatcherTimer that is stopped when disposed.</summary>
internal sealed class DispatcherScheduler : IScheduler
{
    public IDisposable ScheduleOnce(TimeSpan delay, Action callback)
    {
        var handle = new Handle(delay < TimeSpan.FromMilliseconds(1) ? TimeSpan.FromMilliseconds(1) : delay, callback);
        handle.Start();
        return handle;
    }

    private sealed class Handle : IDisposable
    {
        private readonly DispatcherTimer _timer;
        private Action? _callback;

        public Handle(TimeSpan delay, Action callback)
        {
            _callback = callback;
            _timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = delay };
            _timer.Tick += OnTick;
        }

        public void Start() => _timer.Start();

        private void OnTick(object? sender, EventArgs e)
        {
            _timer.Stop();
            var callback = _callback;
            _callback = null; // fire at most once
            callback?.Invoke();
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Tick -= OnTick;
            _callback = null;
        }
    }
}

/// <summary>How long since the user last touched the keyboard or mouse anywhere in Windows.</summary>
internal sealed class WindowsPresenceSource : IPresenceSource
{
    public TimeSpan GetIdleTime()
    {
        var info = new NativeMethods.LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<NativeMethods.LASTINPUTINFO>() };
        if (!NativeMethods.GetLastInputInfo(ref info))
            return TimeSpan.Zero;

        // Both values are 32-bit millisecond tick counts that wrap; unchecked subtraction handles the wrap.
        var idleMilliseconds = unchecked((uint)Environment.TickCount64 - info.dwTime);
        return TimeSpan.FromMilliseconds(idleMilliseconds);
    }
}
