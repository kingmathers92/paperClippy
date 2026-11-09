using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using PaperClippy.App.Mascot;
using PaperClippy.App.Services;
using PaperClippy.Core.Mascot;
using PaperClippy.Core.Settings;
using PaperClippy.Infrastructure;
using PaperClippy.Infrastructure.Diagnostics;
using PaperClippy.Infrastructure.Persistence;

namespace PaperClippy.App;

public partial class PaperClippyApp : Application
{
    private SingleInstanceGuard? _guard;
    private ServiceProvider? _services;
    private FileLog? _log;
    private MascotHost? _mascot;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _guard = new SingleInstanceGuard("PaperClippy");
        if (!_guard.IsFirstInstance)
        {
            _guard.SignalFirstInstance(); // wake the running copy, then quietly leave
            Shutdown();
            return;
        }

        _guard.ActivationRequested += () => Dispatcher.InvokeAsync(() => _mascot?.Show());
        _guard.StartListening();

        try
        {
            _services = BuildServices();
            _log = _services.GetRequiredService<FileLog>();
            _log.Info("PaperClippy starting.");
            HookGlobalErrorHandlers(_log);

            await _services.GetRequiredService<SqliteDatabase>().InitializeAsync();

            _mascot = _services.GetRequiredService<MascotHost>();
            await _mascot.StartAsync();

            _ = _services.GetRequiredService<TrayIconService>(); // created last so the tray menu can drive the mascot
        }
        catch (DatabaseTooNewException ex)
        {
            _log?.Error("Database is newer than this build.", ex);
            MessageBox.Show(ex.Message, "PaperClippy", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown(1);
        }
        catch (Exception ex)
        {
            _log?.Error("Startup failed.", ex);
            MessageBox.Show(
                $"PaperClippy couldn't start.\n\n{ex.Message}\n\nDetails were written to:\n{_log?.Path}",
                "PaperClippy",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _log?.Info("PaperClippy exiting.");

        // The container disposes singletons in reverse creation order: tray first, then the mascot host.
        _services?.Dispose();

        // Close pooled SQLite connections so the write-ahead log is checkpointed into the main file.
        SqliteConnection.ClearAllPools();

        _guard?.Dispose();

        base.OnExit(e);
    }

    private static ServiceProvider BuildServices()
    {
        var paths = AppPaths.ForCurrentUser();
        var services = new ServiceCollection();

        services.AddSingleton(paths);
        services.AddSingleton(new FileLog(paths.LogFile));
        services.AddSingleton(sp => new SqliteDatabase(paths.DatabaseFile, log: sp.GetRequiredService<FileLog>()));
        services.AddSingleton<ISettingsStore>(sp => new SqliteSettingsStore(
            sp.GetRequiredService<SqliteDatabase>(),
            sp.GetRequiredService<FileLog>()));
        services.AddSingleton<MascotPlacementRepository>();
        services.AddSingleton<MascotSettingsRepository>();
        services.AddSingleton<MonitorService>();
        services.AddSingleton<MascotHost>();
        services.AddSingleton<TrayIconService>();

        return services.BuildServiceProvider();
    }

    /// <summary>A desktop companion should survive a stray exception: log it and carry on.</summary>
    private void HookGlobalErrorHandlers(FileLog log)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            log.Error("Unhandled UI exception.", args.Exception);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            log.Error("Unhandled exception (process is terminating).", args.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            log.Warn("Unobserved task exception.", args.Exception);
            args.SetObserved();
        };
    }
}
