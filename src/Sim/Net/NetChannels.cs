using System;

namespace PerformativeMail.Sim.Net;

public static class NetChannels
{
    public const int Unreliable = 0;

    public const int Reliable = 1;

    public const int Handshake = 2;

    public const int Count = 3;

    public static bool IsReliable(int channelId)
    {
        switch (channelId)
        {
            case Unreliable:
                return false;
            case Reliable:
            case Handshake:
                return true;
            default:
                throw new ArgumentOutOfRangeException(nameof(channelId), channelId, null);
        }
    }
}
