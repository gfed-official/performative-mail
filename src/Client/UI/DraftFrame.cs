using PerformativeMail.Sim.Run;

namespace PerformativeMail.Client.UI;

public readonly record struct DraftFrame(string Card1Label, string Card2Label, string Card3Label)
{
    public static DraftFrame From(in DraftOffer offer) =>
        new(offer.First, offer.Second, offer.Third);
}
