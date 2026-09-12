using PerformativeMail.Sim.Movement;

namespace PerformativeMail.Sim.Vehicles;

public readonly record struct PlantedEnemy(int Xcm, int Ycm, int Hp)
{
    public const int StubHp = 60;

    public static PlantedEnemy AtMeters(double x, double y, int hp = StubHp) =>
        new(PlayerPose.QuantizeCm(x), PlayerPose.QuantizeCm(y), hp);
}
