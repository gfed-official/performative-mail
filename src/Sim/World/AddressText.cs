using System;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.World;

public static class AddressText
{
    public static string Format(AddressId address, StreetRecord[] streets)
    {
        if (streets is null) throw new ArgumentNullException(nameof(streets));
        string name = NameOf(address, streets);
        if (name.Length == 0)
        {
            return address.Unit == 0
                ? address.Number.ToString()
                : address.Number + "-" + address.Unit;
        }

        return address.Unit == 0
            ? address.Number + " " + name
            : address.Number + " " + name + " Unit " + address.Unit;
    }

    public static string NameOf(AddressId address, StreetRecord[] streets)
    {
        if (streets is null) throw new ArgumentNullException(nameof(streets));
        for (int i = 0; i < streets.Length; i++)
        {
            var street = streets[i];
            if (street.Id == address.Street && street.District == address.District)
                return street.Name ?? "";
        }

        return "";
    }
}
