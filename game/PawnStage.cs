using Godot;
using PerformativeMail.App;
using PerformativeMail.Client;
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

public partial class PawnStage : Node3D
{
    public const string CameraName = "Camera";
    public const string BodyName = "Body";
    public const string LabelName = "Label";

    private readonly Dictionary<uint, Node3D> _nodes = new();

    public void Sync(IReadOnlyList<PawnView> pawns, float localPitchRadians)
    {
        var seen = new HashSet<uint>();
        for (int i = 0; i < pawns.Count; i++)
        {
            var pawn = pawns[i];
            seen.Add(pawn.Id.Value);
            if (!_nodes.TryGetValue(pawn.Id.Value, out var node))
            {
                node = Spawn(pawn);
                AddChild(node);
                _nodes.Add(pawn.Id.Value, node);
            }

            var pose = pawn.Pose;
            node.Transform = PawnTransform.Of(in pose);
            bool local = pawn.Role == PawnRole.Local;
            if (node.GetNodeOrNull<MeshInstance3D>(BodyName) is MeshInstance3D body)
                body.Visible = !local;
            if (node.GetNodeOrNull<Label3D>(LabelName) is Label3D label)
            {
                label.Text = pawn.DisplayName;
                label.Visible = !local;
            }

            if (node.GetNodeOrNull<Camera3D>(CameraName) is Camera3D camera)
            {
                camera.Current = local;
                camera.Rotation = local
                    ? new Vector3(localPitchRadians, 0f, 0f)
                    : Vector3.Zero;
            }
        }

        if (_nodes.Count == seen.Count)
            return;

        var stale = new List<uint>();
        foreach (var id in _nodes.Keys)
        {
            if (!seen.Contains(id))
                stale.Add(id);
        }

        for (int i = 0; i < stale.Count; i++)
        {
            _nodes[stale[i]].QueueFree();
            _nodes.Remove(stale[i]);
        }
    }

    public void DespawnAll()
    {
        foreach (var node in _nodes.Values)
            node.QueueFree();
        _nodes.Clear();
    }

    private static Node3D Spawn(PawnView pawn)
    {
        var (r, g, b) = PawnPalette.Rgb(pawn.Palette);
        var color = new Color(r / 255f, g / 255f, b / 255f);
        var root = new Node3D { Name = $"Pawn_{pawn.Id.Value}" };
        bool local = pawn.Role == PawnRole.Local;

        var mesh = new MeshInstance3D
        {
            Name = BodyName,
            Mesh = new CapsuleMesh { Radius = 0.35f, Height = 1.6f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color },
            Position = new Vector3(0f, 0.8f, 0f),
            Visible = !local,
        };
        root.AddChild(mesh);

        var label = new Label3D
        {
            Name = LabelName,
            Text = pawn.DisplayName,
            Position = new Vector3(0f, 2.0f, 0f),
            FontSize = 48,
            OutlineSize = 8,
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
        return root;
    }
}
