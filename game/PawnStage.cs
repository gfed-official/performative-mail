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

public partial class PawnStage : Node3D
{
    public const string CameraName = "Camera";
    public const string BodyName = "Body";
    public const string LabelName = "Label";
    public const string HeldMailName = "HeldMail";
    public const int LabelOutlineSize = 8;
    public const float LabelPixelSize = 0.01f;

    private readonly Dictionary<uint, Node3D> _nodes = new();

    public void Sync(IReadOnlyList<PawnView> pawns, float localPitchRadians, MailKindId? heldMail = null)
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
            if (node.GetNodeOrNull<Node3D>(BodyName) is Node3D body)
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
                SyncHeldMail(camera, local ? heldMail : null);
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
        return root;
    }

    private static void SyncHeldMail(Camera3D camera, MailKindId? kind)
    {
        var held = camera.GetNodeOrNull<Node3D>(HeldMailName);
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
            camera.AddChild(held);
        }

        held.Visible = true;
        if (held.HasMeta("art") && held.GetMeta("art").AsString() == path)
            return;

        foreach (var child in held.GetChildren())
            child.QueueFree();
        if (ArtMesh.TryInstantiate(path) is { } mesh)
            held.AddChild(mesh);
        held.SetMeta("art", path);
    }
}
