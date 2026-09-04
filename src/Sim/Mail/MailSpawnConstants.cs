using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.Mail;

public static class MailSpawnConstants
{
    // chapter 01 §2.2, chapter 08 §2.3, chapter 11 §6
    public const int BatchIntervalSeconds = 15;

    // chapter 08 §2.3, chapter 11 §6 (±3)
    public const int BatchJitterSeconds = 3;

    // chapter 03 §1.3, chapter 08 §2.3, chapter 11 §6
    public const double StreetStreakRatio = 0.30;

    // chapter 03 §1.3 / chapter 08 §2.3 shift 1 value shares
    public const double Shift1LetterShare = 0.60;
    public const double Shift1SmallShare = 0.30;
    public const double Shift1MediumShare = 0.10;

    // chapter 01 phase table, chapter 11 §1
    public const int Shift1DeliverySeconds = 240;

    // chapter 11 §3 solo spawn value
    public const int Shift1SpawnValueCents = 960;

    public const byte Shift1 = 1;

    public const double LateValueRatio = 0.5;

    public const int DeadLetterShifts = 2;

    public const int BatchIntervalTicks = BatchIntervalSeconds * TickClock.TickHz;

    public const int Shift1DeliveryTicks = Shift1DeliverySeconds * TickClock.TickHz;

    // chapter 01 §3.2: batchesPerShift is deliverySeconds / batchIntervalSeconds
    public const int BatchesPerShift = Shift1DeliverySeconds / BatchIntervalSeconds;
}
