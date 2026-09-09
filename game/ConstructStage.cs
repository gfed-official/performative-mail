using System.Text;
using Godot;
using PerformativeMail.App;
using PerformativeMail.Sim.Content;

namespace PerformativeMail.Game;

public partial class ConstructStage : Node3D
{
    public const string Prefix = "Construct_";
    public const string LanePrefix = "LaneItem_";
    public const int LabelOutlineSize = 8;
    public const float LabelPixelSize = 0.01f;

    // Locked construct palette (style-guide 0–1).
    private static readonly Color BeltGold = new(0.77f, 0.66f, 0.29f); // #C4A84A
    private static readonly Color ChestWood = new(0.55f, 0.35f, 0.17f); // #8B5A2B
    private static readonly Color WallWood = new(0.65f, 0.49f, 0.32f); // #A67C52
    private static readonly Color SorterBlue = new(0.24f, 0.49f, 1f); // #3D7EFF
    private static readonly Color SplitterCoral = new(0.91f, 0.36f, 0.23f); // #E85D3A
    private static readonly Color MergerGreen = new(0.18f, 0.8f, 0.44f); // #2ECC71
    private static readonly Color InserterOrange = new(0.9f, 0.49f, 0.13f); // #E67E22
    private static readonly Color PipeTeal = new(0.1f, 0.74f, 0.61f); // #1ABC9C
    private static readonly Color DefaultSteel = new(0.54f, 0.56f, 0.60f); // #8A8E9A
    private static readonly Color MailPaper = new(0.97f, 0.95f, 0.87f); // #F7F1DE

    private static readonly StandardMaterial3D BeltGoldMat = Solid(BeltGold);
    private static readonly StandardMaterial3D ChestWoodMat = Solid(ChestWood);
    private static readonly StandardMaterial3D WallWoodMat = Solid(WallWood);
    private static readonly StandardMaterial3D SorterBlueMat = Solid(SorterBlue);
    private static readonly StandardMaterial3D SplitterCoralMat = Solid(SplitterCoral);
    private static readonly StandardMaterial3D MergerGreenMat = Solid(MergerGreen);
    private static readonly StandardMaterial3D InserterOrangeMat = Solid(InserterOrange);
    private static readonly StandardMaterial3D PipeTealMat = Solid(PipeTeal);
    private static readonly StandardMaterial3D DefaultSteelMat = Solid(DefaultSteel);
    private static readonly StandardMaterial3D MailPaperMat = Solid(MailPaper);

    private readonly Dictionary<uint, ConstructVisual> _nodes = new();
    private readonly HashSet<uint> _seen = new();
    private readonly List<uint> _stale = new();
    private readonly List<Node3D> _laneItems = new();

    public void Sync(in ConstructFrame frame)
    {
        _seen.Clear();
        float tileM = frame.TileCm / 100f;
        for (int i = 0; i < frame.Placed.Count; i++)
        {
            var view = frame.Placed[i];
            _seen.Add(view.Id);
            if (!_nodes.TryGetValue(view.Id, out var visual))
            {
                visual = Spawn(in view, tileM);
                AddChild(visual.Root);
                _nodes.Add(view.Id, visual);
            }
            else if (!SamePose(visual.View, in view))
            {
                Place(visual.Root, in view, tileM);
                visual.View = view;
            }
        }

        if (_nodes.Count != _seen.Count)
        {
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

        SyncLaneItems(in frame);
    }

    public void Clear()
    {
        if (_nodes.Count == 0 && _laneItems.Count == 0)
            return;
        foreach (var visual in _nodes.Values)
            visual.Root.QueueFree();
        _nodes.Clear();
        for (int i = 0; i < _laneItems.Count; i++)
            _laneItems[i].QueueFree();
        _laneItems.Clear();
    }

    public string Dump()
    {
        var dump = new StringBuilder();
        dump.AppendLine("CONSTRUCT_DUMP");
        foreach (var child in GetChildren())
        {
            if (child.GetNodeOrNull<Label3D>("Label") is not { } label)
                continue;
            dump.Append(child.Name);
            dump.Append(" Label=");
            dump.Append(label.Text);
            if (child.HasMeta("def"))
            {
                dump.Append(" def=");
                dump.Append(child.GetMeta("def").AsString());
            }

            dump.AppendLine();
        }

        dump.Append("CONSTRUCT_DUMP_END");
        return dump.ToString();
    }

    private void SyncLaneItems(in ConstructFrame frame)
    {
        int need = frame.LaneItems.Count;
        while (_laneItems.Count > need)
        {
            int last = _laneItems.Count - 1;
            _laneItems[last].QueueFree();
            _laneItems.RemoveAt(last);
        }

        for (int i = 0; i < need; i++)
        {
            var item = frame.LaneItems[i];
            var at = ConstructPlacement.LaneItem(item.Tiles, item.Facing, item.PositionCm, item.Lane, frame.TileCm);
            if (i == _laneItems.Count)
            {
                var node = new Node3D
                {
                    Name = LanePrefix + item.Segment + "_" + item.Lane + "_" + i,
                    Position = new Vector3(at.X, at.Y, at.Z),
                };
                node.AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh
                    {
                        Size = new Vector3(0.22f, ConstructPlacement.LaneItemHeightMeters, 0.28f),
                    },
                    MaterialOverride = MailPaperMat,
                    Position = new Vector3(0f, ConstructPlacement.LaneItemHeightMeters * 0.5f, 0f),
                });
                node.AddChild(new Label3D
                {
                    Name = "Label",
                    Text = "Mail",
                    Position = new Vector3(0f, 0.35f, 0f),
                    FontSize = 28,
                    OutlineSize = LabelOutlineSize,
                    PixelSize = LabelPixelSize,
                    Modulate = Colors.White,
                    Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                });
                AddChild(node);
                _laneItems.Add(node);
            }
            else
            {
                _laneItems[i].Position = new Vector3(at.X, at.Y, at.Z);
            }
        }
    }

    private static ConstructVisual Spawn(in ConstructView view, float tileM)
    {
        var root = new Node3D { Name = Prefix + view.Id };
        root.SetMeta("def", view.DefId);
        var size = ConstructPlacement.BoxSize(view.Behaviour, view.FootprintW, view.FootprintH, view.Rotation, tileM);
        root.AddChild(new MeshInstance3D
        {
            Name = "Body",
            Mesh = new BoxMesh { Size = new Vector3(size.X, size.Y, size.Z) },
            MaterialOverride = MaterialFor(view.Behaviour),
            Position = new Vector3(0f, size.Y * 0.5f, 0f),
        });
        root.AddChild(new Label3D
        {
            Name = "Label",
            Text = view.Name,
            Position = new Vector3(0f, size.Y + 0.35f, 0f),
            FontSize = 36,
            OutlineSize = LabelOutlineSize,
            PixelSize = LabelPixelSize,
            Modulate = Colors.White,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        });
        Place(root, in view, tileM);
        return new ConstructVisual(root, view);
    }

    private static void Place(Node3D root, in ConstructView view, float tileM)
    {
        var origin = ConstructPlacement.Origin(view.Tile, view.FootprintW, view.FootprintH, view.Rotation, tileM);
        var toward = ConstructPlacement.Toward(view.Rotation);
        root.Position = new Vector3(origin.X, origin.Y, origin.Z);
        var dir = new Vector3(toward.X, 0f, toward.Z);
        root.Basis = dir.LengthSquared() < 1e-8f
            ? Basis.Identity
            : Basis.LookingAt(dir, Vector3.Up, useModelFront: true);
    }

    private static bool SamePose(ConstructView a, in ConstructView b) =>
        a.Tile == b.Tile && a.Rotation == b.Rotation && a.DefId == b.DefId;

    private static StandardMaterial3D MaterialFor(BuildingBehaviour behaviour)
    {
        switch (behaviour)
        {
            case BuildingBehaviour.Belt:
                return BeltGoldMat;
            case BuildingBehaviour.Container:
                return ChestWoodMat;
            case BuildingBehaviour.Wall:
            case BuildingBehaviour.Gate:
                return WallWoodMat;
            case BuildingBehaviour.Sorter:
                return SorterBlueMat;
            case BuildingBehaviour.Splitter:
                return SplitterCoralMat;
            case BuildingBehaviour.Merger:
                return MergerGreenMat;
            case BuildingBehaviour.Inserter:
                return InserterOrangeMat;
            case BuildingBehaviour.Pipe:
                return PipeTealMat;
            case BuildingBehaviour.Spike:
            case BuildingBehaviour.Turret:
            case BuildingBehaviour.Alarm:
            case BuildingBehaviour.VehicleDepot:
            case BuildingBehaviour.Port:
            case BuildingBehaviour.Pump:
            case BuildingBehaviour.Pier:
                return DefaultSteelMat;
            default:
                throw new ArgumentOutOfRangeException(nameof(behaviour), behaviour, null);
        }
    }

    private static StandardMaterial3D Solid(Color color) => new() { AlbedoColor = color };

    private sealed class ConstructVisual
    {
        public ConstructVisual(Node3D root, ConstructView view)
        {
            Root = root;
            View = view;
        }

        public Node3D Root { get; }

        public ConstructView View { get; set; }
    }
}
