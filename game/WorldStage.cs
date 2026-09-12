using System.Text;
using Godot;
using PerformativeMail.App;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Game;

public partial class WorldStage : Node3D
{
    public const string PostOfficeName = "PostOffice";
    public const string MailIntakeName = "MailIntake";
    public const string HousePrefix = "House_";
    public const string MailboxPrefix = "Mailbox_";
    public const string ResourcePrefix = WorldResourcePlacement.NodePrefix;
    public const string ConstructPrefix = "Construct_";
    public const int LabelOutlineSize = 8;
    public const float LabelPixelSize = 0.01f;

    // Locked P0.2 palette (style-guide 0–1).
    private static readonly Color PostOfficeBrick = new(0.63f, 0.29f, 0.23f); // #A04B3A
    private static readonly Color SpawnPadGold = new(0.77f, 0.66f, 0.29f); // #C4A84A
    private static readonly Color MailIntakeYellow = new(0.95f, 0.82f, 0.29f); // #F2D24A
    private static readonly Color StreetAsphalt = new(0.35f, 0.36f, 0.40f); // #5A5C66
    private static readonly Color StreetCurb = new(0.54f, 0.56f, 0.60f); // #8A8E9A
    private static readonly Color HouseStucco = new(0.88f, 0.81f, 0.66f); // #E0CFA8
    private static readonly Color HouseRoof = new(0.42f, 0.31f, 0.43f); // #6B4E6E
    private static readonly Color MailboxBlue = new(0.18f, 0.23f, 0.55f); // #2F3A8C
    private static readonly Color MailboxFlag = new(0.91f, 0.36f, 0.23f); // #E85D3A
    private static readonly Color ResourceWood = Rgb(WorldResourcePlacement.ColorRgb(ResourceKind.Wood, HarvestRemnant.Live)); // #3D6B2E 0.24f, 0.42f, 0.18f
    private static readonly Color ResourceWoodStump = Rgb(WorldResourcePlacement.ColorRgb(ResourceKind.Wood, HarvestRemnant.Stump)); // #593D24 0.35f, 0.24f, 0.14f
    private static readonly Color ResourceFiber = Rgb(WorldResourcePlacement.ColorRgb(ResourceKind.Fiber, HarvestRemnant.Live)); // #7A8F3A
    private static readonly Color ResourceStone = Rgb(WorldResourcePlacement.ColorRgb(ResourceKind.Stone, HarvestRemnant.Live)); // #8A8680
    private static readonly Color ResourceIronOre = Rgb(WorldResourcePlacement.ColorRgb(ResourceKind.IronOre, HarvestRemnant.Live)); // #6B3A32
    private static readonly Color ResourceSand = Rgb(WorldResourcePlacement.ColorRgb(ResourceKind.Sand, HarvestRemnant.Live)); // #C4A66B
    private static readonly Color ResourceBerries = Rgb(WorldResourcePlacement.ColorRgb(ResourceKind.Berries, HarvestRemnant.Live)); // #8C2F4A

    private static readonly StandardMaterial3D PostOfficeBrickMat = Solid(PostOfficeBrick);
    private static readonly StandardMaterial3D SpawnPadGoldMat = Solid(SpawnPadGold);
    private static readonly StandardMaterial3D MailIntakeYellowMat = Solid(MailIntakeYellow);
    private static readonly StandardMaterial3D StreetAsphaltMat = Solid(StreetAsphalt);
    private static readonly StandardMaterial3D StreetCurbMat = Solid(StreetCurb);
    private static readonly StandardMaterial3D HouseStuccoMat = Solid(HouseStucco);
    private static readonly StandardMaterial3D HouseRoofMat = Solid(HouseRoof);
    private static readonly StandardMaterial3D MailboxBlueMat = Solid(MailboxBlue);
    private static readonly StandardMaterial3D MailboxFlagMat = Solid(MailboxFlag);
    private static readonly StandardMaterial3D ResourceWoodMat = Solid(ResourceWood);
    private static readonly StandardMaterial3D ResourceWoodStumpMat = Solid(ResourceWoodStump);
    private static readonly StandardMaterial3D ResourceFiberMat = Solid(ResourceFiber);
    private static readonly StandardMaterial3D ResourceStoneMat = Solid(ResourceStone);
    private static readonly StandardMaterial3D ResourceIronOreMat = Solid(ResourceIronOre);
    private static readonly StandardMaterial3D ResourceSandMat = Solid(ResourceSand);
    private static readonly StandardMaterial3D ResourceBerriesMat = Solid(ResourceBerries);

    private WorldTables? _bound;
    private readonly List<Node> _spawned = new();
    private readonly List<Node3D> _lods = new();
    private readonly Dictionary<long, Node3D> _resourceMarkers = new();
    private readonly Dictionary<long, HarvestRemnant> _resourceRemnants = new();
    private readonly List<Node> _constructs = new();
    private int _constructHash;

    public void Sync(WorldTables? tables)
    {
        if (ReferenceEquals(_bound, tables))
            return;

        Clear();
        _bound = tables;
        if (tables is null)
            return;

        float tileM = tables.TileCm / 100f;
        SpawnGrassLots(tables.Lots, tables.PostOffice, tables.Streets, tileM);
        SpawnPostOffice(tables.PostOffice, tables.Streets, tileM);
        SpawnStreets(tables.Streets, tileM);
        SpawnHouses(tables.Houses, tables.Streets, tileM);
        SpawnMailboxes(tables.Houses, tables.Streets, tileM);
        SpawnIntake(tables.PostOffice, tables.Streets, tileM);
        SpawnPostalClutter(tables.PostOffice, tables.Streets, tileM);
        SpawnResourceNodes(tables.ResourceNodes, tables.Streets, tileM);
        ApplyCameraLod();
    }

    public override void _Process(double delta)
    {
        _ = delta;
        ApplyCameraLod();
    }

    // Style-guide bands: LO0 default, LO1 past ArtLod.Lo1Meters (30), LO2 past ArtLod.Lo2Meters (60).
    public void ApplyCameraLod()
    {
        if (_lods.Count == 0)
            return;
        var cam = GetViewport()?.GetCamera3D();
        if (cam is null)
            return;
        ArtMesh.ApplyLod(_lods, cam.GlobalPosition.X, cam.GlobalPosition.Z);
    }

    public void SyncHarvest(IReadOnlyList<ResourceNodeView> nodes)
    {
        if (_bound is null || nodes is null)
            return;

        float tileM = _bound.TileCm / 100f;
        for (int i = 0; i < nodes.Count; i++)
        {
            var view = nodes[i];
            long key = ResourceKey(view.Tile);
            if (!WorldResourcePlacement.IsMarkerVisible(view.Remnant))
            {
                RemoveResourceMarker(key);
                continue;
            }

            if (_resourceMarkers.TryGetValue(key, out _)
                && _resourceRemnants.TryGetValue(key, out var remnant)
                && remnant == view.Remnant)
                continue;

            RemoveResourceMarker(key);
            SpawnResource(view.Kind, view.Tile, view.Remnant, _bound.Streets, tileM);
        }
    }

    public void SyncConstructs(IReadOnlyList<ConstructRecord> rows, int tileCm)
    {
        int hash = rows.Count;
        for (int i = 0; i < rows.Count; i++)
            hash = hash * 31 + (int)rows[i].Id.Value;
        if (hash == _constructHash && _constructs.Count == rows.Count)
            return;

        ClearConstructs();
        _constructHash = hash;
        if (rows.Count == 0 || tileCm <= 0)
            return;

        float tileM = tileCm / 100f;
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var at = Vec(WorldTilePlacement.TileCenter(row.Tile, tileM));
            var node = new MeshInstance3D
            {
                Name = ConstructPrefix + row.Id.Value,
                Mesh = new BoxMesh { Size = new Vector3(tileM, 1f, tileM) },
                MaterialOverride = HouseStuccoMat,
                Position = at + new Vector3(0f, 0.5f, 0f),
            };
            AddChild(node);
            _constructs.Add(node);
        }
    }

    public void Clear()
    {
        for (int i = 0; i < _spawned.Count; i++)
            _spawned[i].QueueFree();
        _spawned.Clear();
        _lods.Clear();
        _resourceMarkers.Clear();
        _resourceRemnants.Clear();
        ClearConstructs();
        _bound = null;
    }

    private void ClearConstructs()
    {
        for (int i = 0; i < _constructs.Count; i++)
            _constructs[i].QueueFree();
        _constructs.Clear();
        _constructHash = 0;
    }

    public string Dump()
    {
        var dump = new StringBuilder();
        dump.AppendLine("WORLD_DUMP");
        foreach (var child in GetChildren())
        {
            if (child.GetNodeOrNull<Label3D>("Label") is not { } label)
                continue;
            dump.Append(child.Name);
            dump.Append(" Label=");
            dump.Append(label.Text);
            dump.Append(" district=");
            dump.AppendLine(DistrictSwatch.Hex(label.Modulate));
        }
        dump.Append("WORLD_DUMP_END");
        return dump.ToString();
    }

    private void SpawnPostOffice(PostOfficeRecord po, StreetRecord[] streets, float tileM)
    {
        var origin = Vec(WorldTilePlacement.FootprintOrigin(po.Tile, po.SizeTiles, tileM));
        var toward = WorldTilePlacement.TowardNearestStreet(origin.X, origin.Z, streets, tileM);
        var footprint = new Vector3(
            po.SizeTiles.X * tileM,
            ArtMesh.PostOfficeHeightMeters,
            po.SizeTiles.Y * tileM);
        var visual = TryLod(ArtMesh.PostOffice);
        if (visual is not null)
        {
            ArtMesh.FitFootprint(visual, footprint, toward.X, toward.Z, modelFrontIsPlusZ: true, scaleY: true);
            AddLabeled(
                PostOfficeName,
                origin,
                footprint,
                footprint.Y * 0.5f,
                "Post Office",
                PoDistrict(streets),
                toward.X,
                toward.Z,
                visual: visual);
        }
        else
        {
            AddLabeledBox(
                PostOfficeName,
                origin,
                new Vector3(footprint.X, 2.4f, footprint.Z),
                PostOfficeBrick,
                1.2f,
                "Post Office",
                PoDistrict(streets),
                toward.X,
                toward.Z);
        }

        var padAt = Vec(WorldTilePlacement.TileCenter(po.SpawnPadTile, tileM));
        var pad = ArtMesh.TryInstantiate(ArtMesh.SpawnPad);
        if (pad is not null)
        {
            float padScale = tileM / WorldEnvPlacement.ArtTileMeters;
            pad.Position = padAt;
            if (MathF.Abs(padScale - 1f) > 1e-4f)
                pad.Scale = new Vector3(padScale, 1f, padScale);
            AddChild(pad);
            _spawned.Add(pad);
        }
        else
        {
            AddBox(
                padAt,
                new Vector3(tileM * WorldEnvPlacement.SpawnPadScale, WorldEnvPlacement.SpawnPadHeightMeters, tileM * WorldEnvPlacement.SpawnPadScale),
                SpawnPadGold,
                WorldEnvPlacement.SpawnPadHeightMeters * 0.5f);
        }
    }

    private void SpawnIntake(PostOfficeRecord po, StreetRecord[] streets, float tileM)
    {
        var origin = Vec(WorldTilePlacement.TileCenter(po.IntakeTile, tileM));
        var toward = WorldTilePlacement.TowardNearestStreet(origin.X, origin.Z, streets, tileM);
        var visual = TryLod(ArtMesh.Intake);
        if (visual is not null)
        {
            ArtMesh.Orient(visual, toward.X, toward.Z, modelFrontIsPlusZ: false);
            var size = VisualSize(visual, new Vector3(0.9f, 1.0f, 0.9f));
            AddLabeled(
                MailIntakeName,
                origin,
                size,
                size.Y * 0.5f,
                "Mail",
                PoDistrict(streets),
                toward.X,
                toward.Z,
                visual: visual);
            return;
        }

        AddLabeledBox(
            MailIntakeName,
            origin,
            new Vector3(0.9f, 1.0f, 0.9f),
            MailIntakeYellow,
            0.5f,
            "Mail",
            PoDistrict(streets),
            toward.X,
            toward.Z);
    }

    private void SpawnStreets(StreetRecord[] streets, float tileM)
    {
        var tiles = WorldEnvPlacement.StreetTiles(streets, tileM);
        AddArtTiles(
            "StreetTiles",
            ArtMesh.StreetTile,
            tiles,
            new Vector3(tileM, WorldEnvPlacement.StreetHeightMeters, tileM),
            StreetAsphalt,
            scaleX: tileM / WorldEnvPlacement.ArtTileMeters,
            scaleZ: tileM / WorldEnvPlacement.ArtTileMeters);
        AddArtTiles(
            "StreetCurbs",
            ArtMesh.StreetCurb,
            WorldEnvPlacement.StreetCurbs(streets, tileM),
            new Vector3(tileM, WorldEnvPlacement.CurbHeightMeters, WorldEnvPlacement.CurbThicknessMeters),
            StreetCurb,
            scaleX: tileM / WorldEnvPlacement.ArtTileMeters,
            scaleZ: 1f);
    }

    private void SpawnGrassLots(LotRecord[] lots, PostOfficeRecord po, StreetRecord[] streets, float tileM)
    {
        var mesh = ArtMesh.TryMesh(ArtMesh.GrassTile);
        if (mesh is null)
            return;
        AddMultiMesh(
            "GrassTiles",
            mesh,
            WorldEnvPlacement.LotGrass(lots, po, streets, tileM),
            overlay: null,
            yLift: 0f,
            scaleX: tileM / WorldEnvPlacement.ArtTileMeters,
            scaleZ: tileM / WorldEnvPlacement.ArtTileMeters);
    }

    private void SpawnPostalClutter(PostOfficeRecord po, StreetRecord[] streets, float tileM)
    {
        var props = WorldEnvPlacement.PostalClutter(po, streets, tileM);
        for (int i = 0; i < props.Length; i++)
        {
            var prop = props[i];
            var visual = TryLod(ArtMesh.PathForProp(prop.Kind));
            if (visual is null)
                continue;
            visual.Position = new Vector3(prop.X, prop.Y, prop.Z);
            visual.Rotation = new Vector3(0f, prop.YawRadians, 0f);
            AddChild(visual);
            _spawned.Add(visual);
        }
    }

    private void SpawnResourceNodes(ResourceNodeRecord[] nodes, StreetRecord[] streets, float tileM)
    {
        for (int i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            SpawnResource(node.Kind, node.Tile, HarvestRemnant.Live, streets, tileM);
        }
    }

    private void SpawnResource(
        ResourceKind kind,
        TileCoord tile,
        HarvestRemnant remnant,
        StreetRecord[] streets,
        float tileM)
    {
        var origin = Vec(WorldTilePlacement.TileCenter(tile, tileM));
        var toward = WorldTilePlacement.TowardNearestStreet(origin.X, origin.Z, streets, tileM);
        byte district = DistrictNear(origin.X, origin.Z, streets, tileM);
        var box = WorldResourcePlacement.BoxSize(kind, remnant);
        var size = new Vector3(box.X, box.Y, box.Z);
        string name = WorldResourcePlacement.NodeName(tile);
        string label = WorldResourcePlacement.Label(kind, remnant);
        var visual = ArtMesh.TryInstantiate(ArtMesh.PathForResource(kind, remnant));
        Node3D root;
        if (visual is not null)
        {
            var visSize = VisualSize(visual, size);
            root = AddLabeled(
                name,
                origin,
                visSize,
                visSize.Y * 0.5f,
                label,
                district,
                toward.X,
                toward.Z,
                visual: visual);
        }
        else
        {
            root = AddLabeledBox(
                name,
                origin,
                size,
                ResourceColor(kind, remnant),
                size.Y * 0.5f,
                label,
                district,
                toward.X,
                toward.Z);
        }

        long key = ResourceKey(tile);
        _resourceMarkers[key] = root;
        _resourceRemnants[key] = remnant;
    }

    private void RemoveResourceMarker(long key)
    {
        if (!_resourceMarkers.TryGetValue(key, out var marker))
            return;

        _spawned.Remove(marker);
        _resourceMarkers.Remove(key);
        _resourceRemnants.Remove(key);
        marker.QueueFree();
    }

    private static long ResourceKey(TileCoord tile) => ((long)tile.X << 32) | (uint)tile.Y;

    private void SpawnHouses(HouseRecord[] houses, StreetRecord[] streets, float tileM)
    {
        for (int i = 0; i < houses.Length; i++)
        {
            var house = houses[i];
            string address = AddressText.Format(house.Address, streets);
            var origin = Vec(WorldTilePlacement.FootprintOrigin(house.LotTile, house.LotSizeTiles, tileM));
            var size = new Vector3(
                house.LotSizeTiles.X * tileM * 0.7f,
                1.8f,
                house.LotSizeTiles.Y * tileM * 0.7f);
            var toward = WorldTilePlacement.TowardNearestStreet(origin.X, origin.Z, streets, tileM);
            var visual = TryLod(ArtMesh.HouseVariant(i));
            if (visual is not null)
            {
                ArtMesh.FitFootprint(visual, size, toward.X, toward.Z, modelFrontIsPlusZ: true, scaleY: false);
                float height = MathF.Max(ArtMesh.LocalAabb(visual).Size.Y, size.Y);
                AddLabeled(
                    HousePrefix + house.Address.Number,
                    origin,
                    new Vector3(size.X, height, size.Z),
                    height * 0.5f,
                    address,
                    house.Address.District,
                    toward.X,
                    toward.Z,
                    visual: visual);
                continue;
            }

            var root = AddLabeledBox(
                HousePrefix + house.Address.Number,
                origin,
                size,
                HouseStucco,
                0.9f,
                address,
                house.Address.District,
                toward.X,
                toward.Z,
                WorldPropPlacement.HouseRoofHeightMeters);
            AddHouseRoof(root, size);
        }
    }

    private void SpawnMailboxes(HouseRecord[] houses, StreetRecord[] streets, float tileM)
    {
        for (int i = 0; i < houses.Length; i++)
        {
            var house = houses[i];
            var pose = house.Mailbox;
            var view = ViewFrame.From(new PlayerPose(pose.XCm, pose.YCm, pose.ZCm, 0));
            string address = AddressText.Format(house.Address, streets);
            var toward = WorldTilePlacement.TowardNearestStreet(view.X, view.Z, streets, tileM);
            var origin = new Vector3(view.X, 0f, view.Z);
            var visual = TryLod(ArtMesh.Mailbox);
            if (visual is not null)
            {
                ArtMesh.Orient(visual, toward.X, toward.Z, modelFrontIsPlusZ: false);
                var size = VisualSize(visual, new Vector3(0.28f, 1.15f, 0.28f));
                AddLabeled(
                    MailboxPrefix + house.Address.Number,
                    origin,
                    size,
                    size.Y * 0.5f,
                    address,
                    house.Address.District,
                    toward.X,
                    toward.Z,
                    visual: visual);
                continue;
            }

            var sizeBox = new Vector3(0.28f, 1.15f, 0.28f);
            var root = AddLabeledBox(
                MailboxPrefix + house.Address.Number,
                origin,
                sizeBox,
                MailboxBlue,
                0.57f,
                address,
                house.Address.District,
                toward.X,
                toward.Z);
            AddMailboxFlag(root, sizeBox, toward.X, toward.Z);
        }
    }

    private Node3D AddLabeledBox(
        string name,
        Vector3 origin,
        Vector3 size,
        Color color,
        float heightCenter,
        string labelText,
        byte district,
        float towardX = 0f,
        float towardZ = 0f,
        float stackHeight = 0f)
    {
        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = SolidFor(color),
            Position = new Vector3(0f, heightCenter, 0f),
        };
        return AddLabeled(name, origin, size, heightCenter, labelText, district, towardX, towardZ, stackHeight, mesh);
    }

    private Node3D AddLabeled(
        string name,
        Vector3 origin,
        Vector3 size,
        float heightCenter,
        string labelText,
        byte district,
        float towardX = 0f,
        float towardZ = 0f,
        float stackHeight = 0f,
        Node3D? visual = null)
    {
        var offset = WorldLabelPlacement.AboveStreetFace(
            size.X,
            size.Y + stackHeight,
            size.Z,
            heightCenter + stackHeight * 0.5f,
            towardX,
            towardZ);
        var root = new Node3D
        {
            Name = name,
            Position = origin,
        };
        if (visual is not null)
            root.AddChild(visual);
        root.AddChild(new Label3D
        {
            Name = "Label",
            Text = labelText,
            Position = new Vector3(offset.X, offset.Y, offset.Z),
            FontSize = 42,
            OutlineSize = LabelOutlineSize,
            PixelSize = LabelPixelSize,
            Modulate = DistrictSwatch.Of(district),
            OutlineModulate = Colors.Black,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        });
        AddChild(root);
        _spawned.Add(root);
        return root;
    }

    private static void AddHouseRoof(Node3D root, Vector3 bodySize)
    {
        var roof = WorldPropPlacement.RoofSize(bodySize.X, bodySize.Z);
        root.AddChild(new MeshInstance3D
        {
            Name = "Roof",
            Mesh = new BoxMesh { Size = new Vector3(roof.X, roof.Y, roof.Z) },
            MaterialOverride = HouseRoofMat,
            Position = new Vector3(0f, WorldPropPlacement.RoofCenterY(bodySize.Y), 0f),
        });
    }

    private static void AddMailboxFlag(Node3D root, Vector3 bodySize, float towardX, float towardZ)
    {
        var flag = WorldPropPlacement.MailboxFlagSize(towardX, towardZ);
        var at = WorldPropPlacement.MailboxFlagOffset(bodySize.X, bodySize.Z, towardX, towardZ);
        root.AddChild(new MeshInstance3D
        {
            Name = "Flag",
            Mesh = new BoxMesh { Size = new Vector3(flag.X, flag.Y, flag.Z) },
            MaterialOverride = MailboxFlagMat,
            Position = new Vector3(at.X, at.Y, at.Z),
        });
    }

    private void AddArtTiles(
        string name,
        string artPath,
        EnvInstancePose[] poses,
        Vector3 boxSize,
        Color boxColor,
        float scaleX,
        float scaleZ)
    {
        if (poses.Length == 0)
            return;

        var mesh = ArtMesh.TryMesh(artPath);
        float yLift = 0f;
        StandardMaterial3D? overlay = null;
        float artScaleX = scaleX;
        float artScaleZ = scaleZ;
        if (mesh is null)
        {
            mesh = new BoxMesh { Size = boxSize };
            yLift = boxSize.Y * 0.5f;
            overlay = SolidFor(boxColor);
            artScaleX = 1f;
            artScaleZ = 1f;
        }

        AddMultiMesh(name, mesh, poses, overlay, yLift, artScaleX, artScaleZ);
    }

    private void AddMultiMesh(
        string name,
        Mesh mesh,
        EnvInstancePose[] poses,
        Material? overlay,
        float yLift,
        float scaleX,
        float scaleZ)
    {
        if (poses.Length == 0)
            return;

        var multi = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = mesh,
            InstanceCount = poses.Length,
        };
        for (int i = 0; i < poses.Length; i++)
        {
            var pose = poses[i];
            var basis = Basis.FromEuler(new Vector3(0f, pose.YawRadians, 0f));
            if (MathF.Abs(scaleX - 1f) > 1e-4f || MathF.Abs(scaleZ - 1f) > 1e-4f)
                basis = basis.Scaled(new Vector3(scaleX, 1f, scaleZ));
            multi.SetInstanceTransform(i, new Transform3D(basis, new Vector3(pose.X, pose.Y + yLift, pose.Z)));
        }

        var node = new MultiMeshInstance3D
        {
            Name = name,
            Multimesh = multi,
        };
        if (overlay is not null)
            node.MaterialOverride = overlay;
        AddChild(node);
        _spawned.Add(node);
    }

    private void AddBox(Vector3 origin, Vector3 size, Color color, float heightCenter)
    {
        var node = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = SolidFor(color),
            Position = origin + new Vector3(0f, heightCenter, 0f),
        };
        AddChild(node);
        _spawned.Add(node);
    }

    private Node3D? TryLod(string path)
    {
        var visual = ArtMesh.TryInstantiateLod(path);
        if (visual is not null)
            _lods.Add(visual);
        return visual;
    }

    private static Vector3 VisualSize(Node3D visual, Vector3 fallback)
    {
        var size = ArtMesh.LocalAabb(visual).Size;
        return size.LengthSquared() > 0.01f ? size : fallback;
    }

    private static Vector3 Vec((float X, float Y, float Z) p) => new(p.X, p.Y, p.Z);

    private static byte PoDistrict(StreetRecord[] streets)
    {
        for (int i = 0; i < streets.Length; i++)
        {
            if (streets[i].District != 0)
                return streets[i].District;
        }

        return 1;
    }

    private static byte DistrictNear(float originX, float originZ, StreetRecord[] streets, float tileM)
    {
        float best = float.MaxValue;
        byte district = 0;
        for (int s = 0; s < streets.Length; s++)
        {
            var street = streets[s];
            var tiles = street.Tiles;
            if (tiles is null)
                continue;
            for (int t = 0; t < tiles.Length; t++)
            {
                var at = WorldTilePlacement.TileCenter(tiles[t], tileM);
                float ex = at.X - originX;
                float ez = at.Z - originZ;
                float d = ex * ex + ez * ez;
                if (d >= best)
                    continue;
                best = d;
                district = street.District;
            }
        }

        return district == 0 ? PoDistrict(streets) : district;
    }

    private static Color ResourceColor(ResourceKind kind, HarvestRemnant remnant)
    {
        if (remnant == HarvestRemnant.Stump)
            return ResourceWoodStump;
        return kind switch
        {
            ResourceKind.Wood => ResourceWood,
            ResourceKind.Fiber => ResourceFiber,
            ResourceKind.Stone => ResourceStone,
            ResourceKind.IronOre => ResourceIronOre,
            ResourceKind.Sand => ResourceSand,
            ResourceKind.Berries => ResourceBerries,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    private static Color Rgb((float R, float G, float B) rgb) => new(rgb.R, rgb.G, rgb.B);

    private static StandardMaterial3D SolidFor(Color color)
    {
        if (color == PostOfficeBrick)
            return PostOfficeBrickMat;
        if (color == SpawnPadGold)
            return SpawnPadGoldMat;
        if (color == MailIntakeYellow)
            return MailIntakeYellowMat;
        if (color == StreetAsphalt)
            return StreetAsphaltMat;
        if (color == StreetCurb)
            return StreetCurbMat;
        if (color == HouseStucco)
            return HouseStuccoMat;
        if (color == HouseRoof)
            return HouseRoofMat;
        if (color == MailboxBlue)
            return MailboxBlueMat;
        if (color == MailboxFlag)
            return MailboxFlagMat;
        if (color == ResourceWood)
            return ResourceWoodMat;
        if (color == ResourceWoodStump)
            return ResourceWoodStumpMat;
        if (color == ResourceFiber)
            return ResourceFiberMat;
        if (color == ResourceStone)
            return ResourceStoneMat;
        if (color == ResourceIronOre)
            return ResourceIronOreMat;
        if (color == ResourceSand)
            return ResourceSandMat;
        if (color == ResourceBerries)
            return ResourceBerriesMat;
        return Solid(color);
    }

    private static StandardMaterial3D Solid(Color color) => new() { AlbedoColor = color };
}
