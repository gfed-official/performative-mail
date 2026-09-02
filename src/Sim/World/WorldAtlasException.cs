using System;

namespace PerformativeMail.Sim.World;

public sealed class WorldAtlasException : Exception
{
    public WorldAtlasException(string message) : base(message)
    {
    }

    public WorldAtlasException(string message, Exception inner) : base(message, inner)
    {
    }
}
