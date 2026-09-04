using System;
using System.IO;

namespace PerformativeMail.Sim.World;

public sealed class ContentWatcher : IDisposable
{
    private readonly ContentSession _session;
    private readonly FileSystemWatcher _watcher;
    private bool _disposed;

    internal ContentWatcher(ContentSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _watcher = new FileSystemWatcher(session.Root, "*.json")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
        };
        _watcher.Changed += OnDiskChange;
        _watcher.Created += OnDiskChange;
        _watcher.Deleted += OnDiskChange;
        _watcher.Renamed += OnDiskChange;
        _watcher.EnableRaisingEvents = true;
    }

    public event Action? Reloaded;

    public event Action<string>? Failed;

    private void OnDiskChange(object sender, FileSystemEventArgs e)
    {
        if (_disposed)
            return;

        var result = _session.Reload();
        if (result.Succeeded)
            Reloaded?.Invoke();
        else if (result.Error is string error)
            Failed?.Invoke(error);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _watcher.EnableRaisingEvents = false;
        _watcher.Changed -= OnDiskChange;
        _watcher.Created -= OnDiskChange;
        _watcher.Deleted -= OnDiskChange;
        _watcher.Renamed -= OnDiskChange;
        _watcher.Dispose();
    }
}
