using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;

namespace PerformativeMail.Client.UI;

public readonly record struct OverlayCell(
    string CountLabel,
    string AddressLabel,
    bool Pending)
{
    public const float ConfirmedOpacity = 1f;
    public const float PendingOpacity = 0.6f;

    public float Opacity => Pending ? PendingOpacity : ConfirmedOpacity;

    public string Text
    {
        get
        {
            if (CountLabel.Length == 0)
                return "";
            if (AddressLabel.Length == 0)
                return CountLabel;
            return CountLabel + " " + AddressLabel;
        }
    }

    public static string MiniAddress(AddressId address) =>
        address.Unit == 0
            ? address.Number.ToString()
            : address.Number + "-" + address.Unit;
}

public readonly record struct OverlayGrid(
    string Name,
    byte Cols,
    byte Rows,
    IReadOnlyList<OverlayCell> Cells)
{
    public OverlayCell this[byte x, byte y] => Cells[y * Cols + x];
}

public readonly record struct OverlayFrame(
    OverlayGrid Hotbar,
    OverlayGrid Inventory,
    OverlayGrid? Backpack,
    OverlayGrid? External)
{
    public static OverlayFrame From(in OverlayReplica replica) =>
        new(
            Project("hotbar", replica.Hotbar, replica.Pending),
            Project("inventory", replica.Inventory, replica.Pending),
            replica.Backpack is { } pack ? Project("backpack", pack, replica.Pending) : null,
            replica.External is { } ext ? Project("external", ext, replica.Pending) : null);

    private static OverlayGrid Project(string name, GridContainer grid, IReadOnlySet<EntryId> pending)
    {
        byte cols = grid.Spec.Shape.Cols;
        byte rows = grid.Spec.Shape.Rows;
        var cells = new OverlayCell[cols * rows];
        int i = 0;
        for (byte y = 0; y < rows; y++)
        {
            for (byte x = 0; x < cols; x++)
            {
                var id = grid.EntryAt(new Cell(x, y));
                if (id.IsNone || !grid.TryGetEntry(id, out var entry))
                {
                    cells[i++] = new OverlayCell("", "", false);
                    continue;
                }

                string count = entry.Stack.Count.ToString();
                string address = entry.Stack is MailStack mail
                    ? OverlayCell.MiniAddress(mail.Address)
                    : "";
                cells[i++] = new OverlayCell(count, address, pending.Contains(entry.Id));
            }
        }

        return new OverlayGrid(name, cols, rows, cells);
    }
}
