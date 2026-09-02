using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.Mail;

public static class StreetStreakPicker
{
    public static AddressId[] Pick(
        IReadOnlyList<AddressId> pool,
        int count,
        double streakRatio,
        Random rng)
    {
        if (pool is null) throw new ArgumentNullException(nameof(pool));
        if (pool.Count == 0) throw new ArgumentException("Address pool is empty.", nameof(pool));
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (rng is null) throw new ArgumentNullException(nameof(rng));

        var batch = new AddressId[count];
        for (int i = 0; i < count; i++)
            batch[i] = pool[rng.Next(pool.Count)];

        EnforceStreak(batch, pool, streakRatio, rng);
        return batch;
    }

    public static int SharedCount(IReadOnlyList<AddressId> batch)
    {
        if (batch is null) throw new ArgumentNullException(nameof(batch));
        int shared = 0;
        for (int i = 0; i < batch.Count; i++)
        {
            for (int j = 0; j < batch.Count; j++)
            {
                if (i == j) continue;
                if (batch[i].Street == batch[j].Street)
                {
                    shared++;
                    break;
                }
            }
        }

        return shared;
    }

    public static int MinimumShared(int count, double streakRatio)
    {
        if (count < 2) return 0;
        int need = (int)Math.Ceiling(streakRatio * count);
        return need < 2 ? 2 : need;
    }

    private static void EnforceStreak(
        AddressId[] batch,
        IReadOnlyList<AddressId> pool,
        double streakRatio,
        Random rng)
    {
        int need = MinimumShared(batch.Length, streakRatio);
        while (SharedCount(batch) < need)
        {
            int unique = IndexOfUniqueStreet(batch);
            if (unique < 0) return;

            byte street = StreetToJoin(batch, unique);
            batch[unique] = AddressOnStreet(pool, street, rng);
        }
    }

    private static int IndexOfUniqueStreet(AddressId[] batch)
    {
        for (int i = 0; i < batch.Length; i++)
        {
            if (CountOnStreet(batch, batch[i].Street) == 1)
                return i;
        }

        return -1;
    }

    private static int CountOnStreet(AddressId[] batch, byte street)
    {
        int n = 0;
        for (int i = 0; i < batch.Length; i++)
        {
            if (batch[i].Street == street) n++;
        }

        return n;
    }

    private static byte StreetToJoin(AddressId[] batch, int uniqueIndex)
    {
        int bestCount = 0;
        byte best = batch[uniqueIndex == 0 ? 1 : 0].Street;
        for (int i = 0; i < batch.Length; i++)
        {
            if (i == uniqueIndex) continue;
            int n = CountOnStreet(batch, batch[i].Street);
            if (n > bestCount)
            {
                bestCount = n;
                best = batch[i].Street;
            }
        }

        return best;
    }

    private static AddressId AddressOnStreet(IReadOnlyList<AddressId> pool, byte street, Random rng)
    {
        int matches = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].Street == street) matches++;
        }

        if (matches == 0)
            return pool[rng.Next(pool.Count)];

        int pick = rng.Next(matches);
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].Street != street) continue;
            if (pick == 0) return pool[i];
            pick--;
        }

        return pool[0];
    }
}
