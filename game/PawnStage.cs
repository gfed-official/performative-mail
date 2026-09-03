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
    private readonly Dictionary<uint, Node3D> _nodes = new();

    public void Sync(IReadOnlyList<PawnView> pawns)
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
            if (node.GetNodeOrNull<Label3D>("Label") is Label3D label)
                label.Text = pawn.DisplayName;
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

    public bool TryLocalOrigin(IReadOnlyList<PawnView> pawns, out Vector3 origin)
    {
        for (int i = 0; i < pawns.Count; i++)
        {
            if (pawns[i].Role != PawnRole.Local)
                continue;
            var pose = pawns[i].Pose;
            origin = PawnTransform.Of(in pose).Origin;
            return true;
        }

        origin = default;
        return false;
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

        var mesh = new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.35f, Height = 1.6f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color },
        };
        mesh.Position = new Vector3(0f, 0.8f, 0f);
        root.AddChild(mesh);

        var label = new Label3D
        {
            Name = "Label",
            Text = pawn.DisplayName,
            Position = new Vector3(0f, 2.0f, 0f),
            FontSize = 48,
            OutlineSize = 8,
            Modulate = Colors.White,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        };
        root.AddChild(label);
        return root;
    }
}
