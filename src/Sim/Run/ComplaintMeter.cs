using System;
using PerformativeMail.Sim.Mail;

namespace PerformativeMail.Sim.Run;

public sealed class ComplaintMeter
{
    public const int MinPoints = 0;
    public const int MaxPoints = 100;
    public const int LateDelivery = 2;
    public const int BacklogTick = 1;
    public const int DefaultInspectorThreshold = 75;

    private double _decayCredit;

    public ComplaintMeter(int points = 0)
    {
        if (points < MinPoints || points > MaxPoints)
            throw new ArgumentOutOfRangeException(nameof(points), points, null);
        Points = points;
    }

    public int Points { get; private set; }

    public bool InspectorDue(int threshold = DefaultInspectorThreshold)
        => Points >= threshold;

    public double RaidMultiplier => Math.Min(2.0, 1.0 + Points / 100.0);

    public void Add(int delta)
    {
        Points = Clamp(checked(Points + delta));
    }

    public void AddMisdelivery(MailKindId kind)
        => Add(MailKinds.ComplaintOnMisdelivery(kind));

    public void AddLateDelivery() => Add(LateDelivery);

    public void AddBacklogTick() => Add(BacklogTick);

    public void Decay(double seconds, double perSecond)
    {
        if (seconds < 0)
            throw new ArgumentOutOfRangeException(nameof(seconds), seconds, null);
        if (double.IsNaN(perSecond) || double.IsInfinity(perSecond) || perSecond < 0)
            throw new ArgumentOutOfRangeException(nameof(perSecond), perSecond, null);

        _decayCredit += seconds * perSecond;
        int whole = (int)Math.Floor(_decayCredit);
        if (whole <= 0)
            return;
        _decayCredit -= whole;
        Points = Clamp(Points - whole);
    }

    private static int Clamp(int points)
    {
        if (points < MinPoints) return MinPoints;
        if (points > MaxPoints) return MaxPoints;
        return points;
    }
}
