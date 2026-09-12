using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Vehicles;

namespace PerformativeMail.Client;

public readonly record struct VehicleView(
    EntityId Id,
    VehicleKind Kind,
    PlayerPose Pose);

public sealed class VehicleViewTable
{
    private readonly List<VehicleView> _visible = new();

    public IReadOnlyList<VehicleView> Visible => _visible;

    public void Clear() => _visible.Clear();

    public void Refresh(ClientRuntime client, VehicleTable? vehicles, TimeSpan serverTime)
    {
        if (client is null)
            throw new ArgumentNullException(nameof(client));

        _visible.Clear();
        if (vehicles is null)
        {
            TryAddPredicted(client, serverTime);
            return;
        }

        var all = vehicles.All;
        for (int i = 0; i < all.Count; i++)
        {
            var body = all[i];
            _visible.Add(new VehicleView(body.Id, body.Kind, PresentPose(client, body, serverTime)));
        }
    }

    private void TryAddPredicted(ClientRuntime client, TimeSpan serverTime)
    {
        if (client.Prediction.VehicleId.Value == 0)
            return;
        if (client.LocalPlayer is not EntityId local)
            return;
        if (!client.TryPresent(local, serverTime, out var pose))
            return;

        _visible.Add(new VehicleView(client.Prediction.VehicleId, client.Prediction.VehicleKind, pose));
    }

    private static PlayerPose PresentPose(ClientRuntime client, VehicleBody body, TimeSpan serverTime)
    {
        if (body.Driver.Value != 0 && client.TryPresent(body.Driver, serverTime, out var driven))
            return driven;
        if (client.Prediction.VehicleId == body.Id &&
            client.LocalPlayer is EntityId local &&
            client.TryPresent(local, serverTime, out var predicted))
            return predicted;
        return body.Pose;
    }
}
