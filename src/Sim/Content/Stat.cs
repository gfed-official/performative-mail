using System;

namespace PerformativeMail.Sim.Content;

public enum Stat
{
    PlayerSpeed,
    InventoryRows,
    MailboxInsertTime,
    WeightSpeedPenalty,
    BikeSpeed,
    BikeInventoryRows,
    QuotaMult,
    LetterValue,
    PostcardValue,
    MisdeliveryPenalty,
    ComplaintGain,
    BeltSpeed,
    ContainerGrid,
    ConstructHp,
    DeliverySeconds,
    CursedMailChance,
    MailSizeBias,
    RaidAlsoAtStart,
    OffRoadVehicleSpeed,
    ComplaintDecay,
    InspectorThreshold,
    MegaChance,
    QuotaPlayerOffset
}

public static class Stats
{
    public static bool TryParse(string? id, out Stat stat)
    {
        if (id is null || !Enum.TryParse(id, ignoreCase: false, out stat))
        {
            stat = default;
            return false;
        }

        return Enum.IsDefined(typeof(Stat), stat) && string.Equals(id, stat.ToString(), StringComparison.Ordinal);
    }
}
