using System.Text;
using Godot;
using PerformativeMail.App;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.World;

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

    private const string FallbackMeshKey = "box";

    private readonly Dictionary<uint, ConstructVisual> _nodes = new();
    private readonly HashSet<uint> _seen = new();
    private readonly List<uint> _stale = new();
    private readonly List<ConstructView> _belts = new();
    private readonly Dictionary<string, List<int>> _beltGroups = new();
    private readonly Dictionary<string, MultiMeshInstance3D> _beltBatches = new();
    private readonly List<Vector3> _laneWorld = new();
    private readonly List<Transform3D> _laneVisible = new();
    private readonly List<string> _dropKeys = new();
    private Node3D? _beltLabel;
    private MultiMeshInstance3D? _laneBatch;
    private Mesh? _laneMesh;
    private float _laneYLift;
    private bool _lanePaper;
    private int _beltStamp;
    private int _beltInstanceTotal;

    public int BeltInstanceCount => _beltInstanceTotal;

    public void Sync(in ConstructFrame frame)
    {
        _seen.Clear();
        _belts.Clear();
        float tileM = frame.TileCm / 100f;
        for (int i = 0; i < frame.Placed.Count; i++)
        {
            var view = frame.Placed[i];
            if (IsBatchedBelt(view.Behaviour))
            {
                _belts.Add(view);
                continue;
            }

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

        SyncBelts(tileM);
        SyncLaneItems(in frame);
        PushLaneInstances();
    }

    public override void _Process(double delta)
    {
        _ = delta;
        PushLaneInstances();
    }

    public void Clear()
    {
        if (_nodes.Count == 0
            && _beltBatches.Count == 0
            && _laneBatch is null
            && _beltLabel is null
            && _laneWorld.Count == 0)
            return;
        foreach (var visual in _nodes.Values)
            visual.Root.QueueFree();
        _nodes.Clear();
        foreach (var batch in _beltBatches.Values)
            batch.QueueFree();
        _beltBatches.Clear();
        _beltInstanceTotal = 0;
        _beltStamp = 0;
        if (_beltLabel is not null)
        {
            _beltLabel.QueueFree();
            _beltLabel = null;
        }

        if (_laneBatch is not null)
        {
            _laneBatch.QueueFree();
            _laneBatch = null;
        }

        _laneWorld.Clear();
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

        dump.Append("instances=");
        dump.Append(_beltInstanceTotal);
        dump.AppendLine();
        dump.Append("CONSTRUCT_DUMP_END");
        return dump.ToString();
    }

    private void SyncBelts(float tileM)
    {
        int stamp = _belts.Count;
        for (int i = 0; i < _belts.Count; i++)
        {
            var view = _belts[i];
            stamp = (stamp * 31) ^ (int)view.Id ^ view.Tile.X ^ (view.Tile.Y << 16) ^ (int)view.Rotation;
        }

        if (stamp == _beltStamp && _beltInstanceTotal == _belts.Count && _belts.Count > 0)
            return;
        if (stamp == _beltStamp && _beltInstanceTotal == 0 && _belts.Count == 0)
            return;

        _beltStamp = stamp;
        _beltGroups.Clear();
        for (int i = 0; i < _belts.Count; i++)
        {
            string key = BeltMeshKey(_belts[i].DefId);
            if (!_beltGroups.TryGetValue(key, out var indices))
            {
                indices = new List<int>();
                _beltGroups[key] = indices;
            }

            indices.Add(i);
        }

        _dropKeys.Clear();
        foreach (var key in _beltBatches.Keys)
        {
            if (!_beltGroups.ContainsKey(key))
                _dropKeys.Add(key);
        }

        for (int i = 0; i < _dropKeys.Count; i++)
        {
            _beltBatches[_dropKeys[i]].QueueFree();
            _beltBatches.Remove(_dropKeys[i]);
        }

        int instances = 0;
        foreach (var pair in _beltGroups)
        {
            var mesh = MeshForBeltKey(pair.Key, tileM, out float yLift);
            if (!_beltBatches.TryGetValue(pair.Key, out var batch))
            {
                batch = new MultiMeshInstance3D
                {
                    Name = BeltNodeName(pair.Key),
                    Multimesh = new MultiMesh
                    {
                        TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                    },
                };
                AddChild(batch);
                _beltBatches[pair.Key] = batch;
            }

            var mm = batch.Multimesh;
            mm.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
            mm.Mesh = mesh;
            batch.MaterialOverride = pair.Key == FallbackMeshKey ? BeltGoldMat : null;
            var indices = pair.Value;
            mm.InstanceCount = indices.Count;
            for (int i = 0; i < indices.Count; i++)
            {
                var belt = _belts[indices[i]];
                mm.SetInstanceTransform(i, InstanceXf(in belt, tileM, yLift));
            }

            instances += indices.Count;
        }

        _beltInstanceTotal = instances;
        SyncBeltLabel();
    }

    private void SyncBeltLabel()
    {
        if (_belts.Count == 0)
        {
            if (_beltLabel is not null)
            {
                _beltLabel.QueueFree();
                _beltLabel = null;
            }

            return;
        }

        if (_beltLabel is not null)
            return;

        _beltLabel = new Node3D { Name = Prefix + "Belt" };
        _beltLabel.AddChild(new Label3D
        {
            Name = "Label",
            Text = "Conveyor Belt",
            Position = new Vector3(0f, 0.5f, 0f),
            FontSize = 36,
            OutlineSize = LabelOutlineSize,
            PixelSize = LabelPixelSize,
            Modulate = Colors.White,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        });
        AddChild(_beltLabel);
    }

    private void SyncLaneItems(in ConstructFrame frame)
    {
        _laneWorld.Clear();
        EnsureLaneMesh();
        for (int i = 0; i < frame.LaneItems.Count; i++)
        {
            var item = frame.LaneItems[i];
            var at = ConstructPlacement.LaneItem(item.Tiles, item.Facing, item.PositionCm, item.Lane, frame.TileCm);
            _laneWorld.Add(new Vector3(at.X, at.Y + _laneYLift, at.Z));
        }

        if (_laneWorld.Count == 0)
        {
            if (_laneBatch is not null)
                _laneBatch.Multimesh.InstanceCount = 0;
            return;
        }

        if (_laneBatch is null)
        {
            _laneBatch = new MultiMeshInstance3D
            {
                Name = LanePrefix + "Batch",
                Multimesh = new MultiMesh
                {
                    TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                    Mesh = _laneMesh,
                },
                MaterialOverride = _lanePaper ? MailPaperMat : null,
            };
            AddChild(_laneBatch);
        }
        else
        {
            _laneBatch.Multimesh.Mesh = _laneMesh;
            _laneBatch.MaterialOverride = _lanePaper ? MailPaperMat : null;
        }
    }

    private void PushLaneInstances()
    {
        if (_laneBatch is null)
            return;

        var cam = GetViewport()?.GetCamera3D();
        _laneVisible.Clear();
        for (int i = 0; i < _laneWorld.Count; i++)
        {
            var at = _laneWorld[i];
            if (cam is not null)
            {
                float meters = ArtLod.HorizontalMeters(at.X, at.Z, cam.GlobalPosition.X, cam.GlobalPosition.Z);
                if (!LaneItemVisible(meters))
                    continue;
            }

            _laneVisible.Add(new Transform3D(Basis.Identity, at));
        }

        var mm = _laneBatch.Multimesh;
        mm.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
        mm.InstanceCount = _laneVisible.Count;
        for (int i = 0; i < _laneVisible.Count; i++)
            mm.SetInstanceTransform(i, _laneVisible[i]);
    }

    private void EnsureLaneMesh()
    {
        if (_laneMesh is not null)
            return;
        if (ArtMesh.TryMesh(ArtMesh.MailLetter) is { } letter)
        {
            _laneMesh = letter;
            _laneYLift = 0f;
            _lanePaper = false;
            return;
        }

        _laneMesh = new BoxMesh
        {
            Size = new Vector3(0.22f, ConstructPlacement.LaneItemHeightMeters, 0.28f),
        };
        _laneYLift = ConstructPlacement.LaneItemHeightMeters * 0.5f;
        _lanePaper = true;
    }

    private static string BeltNodeName(string key)
    {
        if (key == FallbackMeshKey)
            return Prefix + "Belt_box";
        int slash = key.LastIndexOf('/');
        string file = slash >= 0 ? key[(slash + 1)..] : key;
        return Prefix + "Belt_" + file;
    }

    private static string BeltMeshKey(string defId)
    {
        if (ArtMesh.TryPathForConstruct(defId, out var path) && ArtMesh.TryMesh(path) is not null)
            return path;
        return FallbackMeshKey;
    }

    private static Mesh MeshForBeltKey(string key, float tileM, out float yLift)
    {
        if (key != FallbackMeshKey && ArtMesh.TryMesh(key) is { } mesh)
        {
            yLift = ArtMesh.InstanceYOffset(key);
            return mesh;
        }

        var size = ConstructPlacement.BoxSize(BuildingBehaviour.Belt, 1, 1, Facing.North, tileM);
        yLift = size.Y * 0.5f;
        return new BoxMesh { Size = new Vector3(size.X, size.Y, size.Z) };
    }

    private static Transform3D InstanceXf(in ConstructView view, float tileM, float yLift)
    {
        var origin = ConstructPlacement.Origin(view.Tile, view.FootprintW, view.FootprintH, view.Rotation, tileM);
        var toward = ConstructPlacement.Toward(view.Rotation);
        var dir = new Vector3(toward.X, 0f, toward.Z);
        var basis = dir.LengthSquared() < 1e-8f
            ? Basis.Identity
            : Basis.LookingAt(dir, Vector3.Up, useModelFront: true);
        return new Transform3D(basis, new Vector3(origin.X, origin.Y + yLift, origin.Z));
    }

    private static bool LaneItemVisible(float meters)
    {
        switch (ArtLod.ForDistance(meters))
        {
            case ArtLodLevel.Lo0:
            case ArtLodLevel.Lo1:
                return true;
            case ArtLodLevel.Lo2:
                return false;
            default:
            {
                ArtLodLevel exhausted = ArtLod.ForDistance(meters);
                throw new ArgumentOutOfRangeException(nameof(meters), exhausted, null);
            }
        }
    }

    private static bool IsBatchedBelt(BuildingBehaviour behaviour)
    {
        switch (behaviour)
        {
            case BuildingBehaviour.Belt:
                return true;
            case BuildingBehaviour.Container:
            case BuildingBehaviour.Wall:
            case BuildingBehaviour.Gate:
            case BuildingBehaviour.Sorter:
            case BuildingBehaviour.Splitter:
            case BuildingBehaviour.Merger:
            case BuildingBehaviour.Inserter:
            case BuildingBehaviour.Pipe:
            case BuildingBehaviour.Spike:
            case BuildingBehaviour.Turret:
            case BuildingBehaviour.Alarm:
            case BuildingBehaviour.VehicleDepot:
            case BuildingBehaviour.Port:
            case BuildingBehaviour.Pump:
            case BuildingBehaviour.Pier:
                return false;
            default:
                throw new ArgumentOutOfRangeException(nameof(behaviour), behaviour, null);
        }
    }

    private static ConstructVisual Spawn(in ConstructView view, float tileM)
    {
        var root = new Node3D { Name = Prefix + view.Id };
        root.SetMeta("def", view.DefId);
        var size = ConstructPlacement.BoxSize(view.Behaviour, view.FootprintW, view.FootprintH, view.Rotation, tileM);
        float labelY = size.Y + 0.35f;
        if (ArtMesh.TryPathForConstruct(view.DefId, out var path) &&
            ArtMesh.TryInstantiate(path) is { } mesh)
        {
            mesh.Name = "Body";
            float sink = ArtMesh.InstanceYOffset(path);
            if (sink != 0f)
                mesh.Position = new Vector3(0f, sink, 0f);
            root.AddChild(mesh);
            var aabb = ArtMesh.LocalAabb(mesh);
            labelY = mesh.Position.Y + aabb.Position.Y + aabb.Size.Y + 0.35f;
        }
        else
        {
            root.AddChild(new MeshInstance3D
            {
                Name = "Body",
                Mesh = new BoxMesh { Size = new Vector3(size.X, size.Y, size.Z) },
                MaterialOverride = MaterialFor(view.Behaviour),
                Position = new Vector3(0f, size.Y * 0.5f, 0f),
            });
        }
        root.AddChild(new Label3D
        {
            Name = "Label",
            Text = view.Name,
            Position = new Vector3(0f, labelY, 0f),
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
