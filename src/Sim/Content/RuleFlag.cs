namespace PerformativeMail.Sim.Content;

public enum RuleFlag
{
    InsuredRebuild,
    RaidAtDeliveryStart,
    CursedMailMiniraid
}

public static class RuleFlags
{
    public static bool TryParse(string? id, out RuleFlag flag)
    {
        switch (id)
        {
            case "insured_rebuild":
                flag = RuleFlag.InsuredRebuild;
                return true;
            case "raid_at_delivery_start":
                flag = RuleFlag.RaidAtDeliveryStart;
                return true;
            case "cursed_mail_miniraid":
                flag = RuleFlag.CursedMailMiniraid;
                return true;
            default:
                flag = default;
                return false;
        }
    }
}
