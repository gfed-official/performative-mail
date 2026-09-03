using PerformativeMail.App;
using PerformativeMail.Sim.Net;

namespace PerformativeMail.Game.Net;

public sealed class GodotEnetStack : INetworkStack
{
    public ListenResult Listen(ushort port, int maxPlayers)
    {
        if (maxPlayers < 1)
            throw new ArgumentOutOfRangeException(nameof(maxPlayers), maxPlayers, null);

        var pair = new LoopbackTransport();
        var local = LoopbackLink.OverPipes(pair.A);
        int remotes = Math.Max(1, maxPlayers - 1);
        var remote = EnetServerLink.Bind(port, remotes, firstId: 1);
        return new ListenResult(new CombinedServerLink(local, remote), pair.B);
    }

    public IClientLink Connect(JoinTarget target) => EnetClientLink.Dial(target);
}
