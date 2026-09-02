using PerformativeMail.Sim.Net;

namespace PerformativeMail.Net.Tests.Soak;

public sealed class SoakRoster
{
    public const int SeatCount = 8;
    public const int RealCount = 2;
    public const int BotCount = 6;

    private readonly SoakSeat[] _seats;

    private SoakRoster(SoakSeat[] seats) => _seats = seats;

    public IReadOnlyList<SoakSeat> Seats => _seats;

    public static SoakRoster Create(SoakSeat[] seats)
    {
        if (seats is null)
            throw new ArgumentNullException(nameof(seats));
        if (seats.Length != SeatCount)
            throw new ArgumentException($"SoakRoster needs {SeatCount} seats.", nameof(seats));

        int real = 0;
        int bot = 0;
        var copy = new SoakSeat[SeatCount];
        for (int i = 0; i < seats.Length; i++)
        {
            var seat = seats[i] ?? throw new ArgumentException("SoakRoster seat is null.", nameof(seats));
            switch (seat.Kind)
            {
                case SeatKind.Real:
                    real++;
                    break;
                case SeatKind.Bot:
                    bot++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(seats), seat.Kind, null);
            }

            copy[i] = seat;
        }

        if (real != RealCount || bot != BotCount)
            throw new ArgumentException($"SoakRoster needs {RealCount} Real and {BotCount} Bot seats.", nameof(seats));

        return new SoakRoster(copy);
    }
}
