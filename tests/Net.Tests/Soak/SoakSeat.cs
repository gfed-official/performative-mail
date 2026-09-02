using PerformativeMail.BotClient;
using PerformativeMail.Client;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Net;

namespace PerformativeMail.Net.Tests.Soak;

public sealed class SoakSeat
{
    public SoakSeat(
        ConnectionId id,
        SeatKind kind,
        ClientRuntime client,
        ITransport clientEnd,
        BotState? brain = null)
    {
        if (client is null)
            throw new ArgumentNullException(nameof(client));
        if (clientEnd is null)
            throw new ArgumentNullException(nameof(clientEnd));

        switch (kind)
        {
            case SeatKind.Real:
            case SeatKind.Bot:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }

        Id = id;
        Kind = kind;
        Client = client;
        ClientEnd = clientEnd;
        Brain = brain;
    }

    public ConnectionId Id { get; }

    public SeatKind Kind { get; }

    public EntityId Player { get; set; }

    public ClientRuntime Client { get; }

    public ITransport ClientEnd { get; }

    public BotState? Brain { get; }
}
