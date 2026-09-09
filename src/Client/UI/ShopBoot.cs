using System;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Client.UI;

public static class ShopBoot
{
    public static ShopFrame Inspect() =>
        new(
            ShopFrame.FormatCents(1000),
            ShopFrame.PhaseLabelOf(RunPhase.Prep),
            new[]
            {
                new ShopRowFrame("axe", "Axe", ShopFrame.FormatCents(80), "", true),
                new ShopRowFrame("bandage_x3", "Bandages ×3", ShopFrame.FormatCents(80), "", true),
                new ShopRowFrame("bike", "Bike", ShopFrame.FormatCents(120), "", true),
                new ShopRowFrame(
                    "bp_pipes",
                    "Blueprint: Pneumatics",
                    ShopFrame.FormatCents(700),
                    ShopFrame.UnlocksPrefix + "3",
                    false),
            });

    public static ShopFrame Closed() =>
        new(ShopFrame.FormatCents(0), ShopFrame.ClosedText, Array.Empty<ShopRowFrame>());
}
