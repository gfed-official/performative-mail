using Godot;
using PerformativeMail.App;
using PerformativeMail.Client;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;

namespace PerformativeMail.Game;

public static class PawnTransform
{
    public static Transform3D Of(in PlayerPose pose)
    {
        var view = ViewFrame.From(in pose);
        return new Transform3D(
            Basis.FromEuler(new Vector3(0f, view.YawRadians, 0f)),
            new Vector3(view.X, view.Y, view.Z));
    }
}

public static class VehicleTransform
{
    public static Transform3D Of(in PlayerPose pose)
    {
        var view = ViewFrame.From(in pose);
        var toward = new Vector3(-MathF.Sin(view.YawRadians), 0f, -MathF.Cos(view.YawRadians));
        var basis = toward.LengthSquared() < 1e-8f
            ? Basis.Identity
            : Basis.LookingAt(toward, Vector3.Up, useModelFront: true);
        return new Transform3D(basis, new Vector3(view.X, view.Y, view.Z));
    }
}

public partial class PawnStage : Node3D
{
    public const string CameraName = "Camera";
    public const string BodyName = "Body";
    public const string LabelName = "Label";
    public const string HeldMailName = "HeldMail";
    public const string VehiclePrefix = "Vehicle_";
    public const int LabelOutlineSize = 8;
    public const float LabelPixelSize = 0.01f;

    private readonly Dictionary<uint, PawnVisual> _nodes = new();
    private readonly Dictionary<uint, VehicleVisual> _vehicles = new();
    private readonly HashSet<uint> _seen = new();
    private readonly HashSet<uint> _vehicleSeen = new();
    private readonly List<uint> _stale = new();
    private readonly List<uint> _vehicleStale = new();

    public void Sync(
        IReadOnlyList<PawnView> pawns,
        float localPitchRadians,
        MailKindId? heldMail = null,
        byte heldDistrict = 0)
    {
        _seen.Clear();
        for (int i = 0; i < pawns.Count; i++)
        {
            var pawn = pawns[i];
            _seen.Add(pawn.Id.Value);
            if (!_nodes.TryGetValue(pawn.Id.Value, out var visual))
            {
                visual = Spawn(pawn);
                AddChild(visual.Root);
                _nodes.Add(pawn.Id.Value, visual);
            }

            var pose = pawn.Pose;
            if (!visual.Pose.Equals(pose))
            {
                visual.Root.Transform = PawnTransform.Of(in pose);
                visual.Pose = pose;
            }

            bool local = pawn.Role == PawnRole.Local;
            if (visual.Body.Visible == local)
                visual.Body.Visible = !local;
            if (visual.Label.Text != pawn.DisplayName)
                visual.Label.Text = pawn.DisplayName;
            if (visual.Label.Visible == local)
                visual.Label.Visible = !local;

            var camera = visual.Camera;
            camera.Current = local;
            var pitch = local ? new Vector3(localPitchRadians, 0f, 0f) : Vector3.Zero;
            if (camera.Rotation != pitch)
                camera.Rotation = pitch;
            SyncHeldMail(visual, local ? heldMail : null, local ? heldDistrict : (byte)0);
        }

        if (_nodes.Count == _seen.Count)
            return;

        _stale.Clear();
        foreach (var id in _nodes.Keys)
        {
            if (!_seen.Contains(id))
                _stale.Add(id);
        }

        for (int i = 0; i < _stale.Count; i++)
        {
            _nodes[_stale[i]].Root.QueueFree();
            _nodes.Remove(_stale[i]);
        }
    }

    public void SyncVehicles(IReadOnlyList<VehicleView> vehicles)
    {
        _vehicleSeen.Clear();
        for (int i = 0; i < vehicles.Count; i++)
        {
            var view = vehicles[i];
            _vehicleSeen.Add(view.Id.Value);
            if (!_vehicles.TryGetValue(view.Id.Value, out var visual))
            {
                visual = SpawnVehicle(view);
                AddChild(visual.Root);
                _vehicles.Add(view.Id.Value, visual);
            }

            var pose = view.Pose;
            if (visual.Pose.Equals(pose))
                continue;
            visual.Root.Transform = VehicleTransform.Of(in pose);
            visual.Pose = pose;
        }

        if (_vehicles.Count == _vehicleSeen.Count)
            return;

        _vehicleStale.Clear();
        foreach (var id in _vehicles.Keys)
        {
            if (!_vehicleSeen.Contains(id))
                _vehicleStale.Add(id);
        }

        for (int i = 0; i < _vehicleStale.Count; i++)
        {
            _vehicles[_vehicleStale[i]].Root.QueueFree();
            _vehicles.Remove(_vehicleStale[i]);
        }
    }

    public void DespawnAll()
    {
        if (_nodes.Count != 0)
        {
            foreach (var visual in _nodes.Values)
                visual.Root.QueueFree();
            _nodes.Clear();
        }

        if (_vehicles.Count == 0)
            return;
        foreach (var visual in _vehicles.Values)
            visual.Root.QueueFree();
        _vehicles.Clear();
    }

    private static PawnVisual Spawn(PawnView pawn)
    {
        var (r, g, b) = PawnPalette.Rgb(pawn.Palette);
        var color = new Color(r / 255f, g / 255f, b / 255f);
        var root = new Node3D { Name = $"Pawn_{pawn.Id.Value}" };
        bool local = pawn.Role == PawnRole.Local;

        var body = new Node3D
        {
            Name = BodyName,
            Visible = !local,
        };
        var mesh = ArtMesh.TryInstantiate(ArtMesh.PawnRemote);
        if (mesh is not null)
        {
            ArtMesh.ApplyPawnKitColor(mesh, color);
            body.AddChild(mesh);
        }
        else
        {
            body.AddChild(new MeshInstance3D
            {
                Mesh = new CapsuleMesh { Radius = 0.35f, Height = 1.6f },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = color },
                Position = new Vector3(0f, 0.8f, 0f),
            });
        }

        root.AddChild(body);

        var labelAt = PawnLabelPlacement.AbovePawn();
        var label = new Label3D
        {
            Name = LabelName,
            Text = pawn.DisplayName,
            Position = new Vector3(labelAt.X, labelAt.Y, labelAt.Z),
            FontSize = 48,
            OutlineSize = LabelOutlineSize,
            PixelSize = LabelPixelSize,
            Modulate = Colors.White,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Visible = !local,
        };
        root.AddChild(label);

        var camera = new Camera3D
        {
            Name = CameraName,
            Position = new Vector3(0f, FirstPersonLook.EyeHeightMeters, 0f),
            Current = local,
        };
        root.AddChild(camera);
        return new PawnVisual(root, body, label, camera, pawn.Pose);
    }

    private static void SyncHeldMail(PawnVisual visual, MailKindId? kind, byte district)
    {
        var held = visual.HeldMail;
        if (kind is null)
        {
            if (held is not null)
                held.Visible = false;
            return;
        }

        string path = ArtMesh.PathForMail(kind.Value);
        if (held is null)
        {
            held = new Node3D
            {
                Name = HeldMailName,
                Position = new Vector3(0.22f, -0.2f, -0.38f),
            };
            visual.Camera.AddChild(held);
            visual.HeldMail = held;
        }

        held.Visible = true;
        bool sameArt = held.HasMeta("art") && held.GetMeta("art").AsString() == path;
        bool sameDistrict = held.HasMeta("district") && (byte)held.GetMeta("district").AsInt32() == district;
        if (sameArt && sameDistrict)
            return;

        if (!sameArt)
        {
            foreach (var child in held.GetChildren())
                child.QueueFree();
            if (ArtMesh.TryInstantiate(path) is { } mesh)
                held.AddChild(mesh);
            held.SetMeta("art", path);
        }

        ArtMesh.ApplyDistrictColor(held, DistrictSwatch.Of(district));
        held.SetMeta("district", district);
    }

    private static VehicleVisual SpawnVehicle(VehicleView view)
    {
        var root = new Node3D { Name = VehiclePrefix + view.Id.Value };
        var mesh = ArtMesh.TryInstantiate(ArtMesh.PathForVehicle(view.Kind));
        if (mesh is not null)
            root.AddChild(mesh);
        else
        {
            root.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.45f, 1.05f, 1.7f) },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.18f, 0.23f, 0.55f), // #2F3A8C
                },
                Position = new Vector3(0f, 0.525f, 0f),
            });
        }

        root.Transform = VehicleTransform.Of(in view.Pose);
        return new VehicleVisual(root, view.Pose);
    }

    private sealed class PawnVisual
    {
        public PawnVisual(Node3D root, Node3D body, Label3D label, Camera3D camera, PlayerPose pose)
        {
            Root = root;
            Body = body;
            Label = label;
            Camera = camera;
            Pose = pose;
        }

        public Node3D Root { get; }
        public Node3D Body { get; }
        public Label3D Label { get; }
        public Camera3D Camera { get; }
        public PlayerPose Pose { get; set; }
        public Node3D? HeldMail { get; set; }
    }

    private sealed class VehicleVisual
    {
        public VehicleVisual(Node3D root, PlayerPose pose)
        {
            Root = root;
            Pose = pose;
        }

        public Node3D Root { get; }
        public PlayerPose Pose { get; set; }
    }
}
