using System;
using System.IO;

namespace PerformativeMail.Sim.World;

public readonly struct ContentReload
{
    private ContentReload(string? error)
    {
        Error = error;
    }

    public bool Succeeded => Error is null;

    public string? Error { get; }

    public static ContentReload Ok() => new(null);

    public static ContentReload Fail(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
            throw new ArgumentException("Error is required.", nameof(error));
        return new ContentReload(error);
    }
}

public sealed class ContentSession
{
    private readonly object _gate = new();
    private readonly string _root;
    private ContentBundle _bundle;

    private ContentSession(string root, ContentBundle bundle)
    {
        _root = root;
        _bundle = bundle;
    }

    public string Root => _root;

    public ContentBundle Bundle
    {
        get
        {
            lock (_gate)
                return _bundle;
        }
    }

    public static ContentSession Open(string contentRoot)
    {
        var bundle = ContentFiles.Load(contentRoot);
        return new ContentSession(Path.GetFullPath(contentRoot), bundle);
    }

    public ContentReload Reload()
    {
        lock (_gate)
        {
            try
            {
                _bundle = ContentFiles.Load(_root);
                return ContentReload.Ok();
            }
            catch (Exception ex)
            {
                return ContentReload.Fail(ex.Message);
            }
        }
    }

    public ContentWatcher Watch() => new ContentWatcher(this);
}
