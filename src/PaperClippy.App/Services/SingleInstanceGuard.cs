using System;
using System.Threading;

namespace PaperClippy.App.Services;

/// <summary>
/// Makes PaperClippy a single-instance app. A second launch doesn't start another mascot; it
/// pokes the running instance (which brings its mascot back if hidden) and exits.
/// </summary>
internal sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activateSignal;
    private readonly ManualResetEvent _stopSignal = new(false);
    private Thread? _listener;
    private bool _disposed;

    public SingleInstanceGuard(string name)
    {
        // "Local\" scopes both objects to the current Windows session (fast user switching safe).
        _mutex = new Mutex(initiallyOwned: true, $@"Local\{name}.Instance", out var createdNew);
        IsFirstInstance = createdNew;
        _activateSignal = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{name}.Activate");
    }

    public bool IsFirstInstance { get; }

    /// <summary>Raised on a background thread when another launch asked us to show ourselves.</summary>
    public event Action? ActivationRequested;

    public void StartListening()
    {
        if (!IsFirstInstance || _listener is not null)
            return;

        _listener = new Thread(() =>
        {
            var handles = new WaitHandle[] { _activateSignal, _stopSignal };
            while (WaitHandle.WaitAny(handles) == 0)
                ActivationRequested?.Invoke();
        })
        {
            IsBackground = true,
            Name = "PaperClippy.SingleInstance",
        };
        _listener.Start();
    }

    public void SignalFirstInstance() => _activateSignal.Set();

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _stopSignal.Set();
        _listener?.Join(TimeSpan.FromMilliseconds(500));

        if (IsFirstInstance)
        {
            try { _mutex.ReleaseMutex(); }
            catch (ApplicationException) { /* released from a different thread: process exit frees it anyway */ }
        }

        _mutex.Dispose();
        _activateSignal.Dispose();
        _stopSignal.Dispose();
    }
}
