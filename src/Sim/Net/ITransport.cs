namespace PerformativeMail.Sim.Net;

public interface ITransport
{
    void Send(int channelId, byte[] payload);

    bool Poll(out int channelId, out byte[] payload);
}
