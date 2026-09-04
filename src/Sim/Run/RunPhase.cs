namespace PerformativeMail.Sim.Run;

public enum RunPhase : byte
{
    Lobby = 0,
    Generating = 1,
    Prep = 2,
    Delivery = 3,
    Raid = 4,
    Payday = 5,
    Draft = 6,
    Results = 7,
    RunOver = 8,
    Victory = 9,
}
