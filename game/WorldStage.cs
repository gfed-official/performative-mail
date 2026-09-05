using System.Text;
using Godot;
using PerformativeMail.App;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Game;

public partial class WorldStage : Node3D
{
    public const string PostOfficeName = "PostOffice";
    public const string MailIntakeName = "MailIntake";
    public const string HousePrefix = "House_";
    public const string MailboxPrefix = "Mailbox_";
    public const int LabelOutlineSize = 8;
    public const float LabelPixelSize = 0.01f;

    // Locked P0.2 palette (style-guide 0–1).
    private static readonly Color PostOfficeBrick = new(0.63f, 0.29f, 0.23f); // #A04B3A
    private static readonly Color SpawnPadGold = new(0.77f, 0.66f, 0.29f); // #C4A84A
    private static readonly Color MailIntakeYellow = new(0.95f, 0.82f, 0.29f); // #F2D24A
    private static readonly Color StreetAsphalt = new(0.35f, 0.36f, 0.40f); // #5A5C66
    private static readonly Color HouseStucco = new(0.88f, 0.81f, 0.66f); // #E0CFA8
    private static readonly Color HouseRoof = new(0.42f, 0.31f, 0.43f); // #6B4E6E
    private static readonly Color MailboxBlue = new(0.18f, 0.23f, 0.55f); // #2F3A8C
    private static readonly Color MailboxFlag = new(0.91f, 0.36f, 0.23f); // #E85D3A

    private WorldTables? _bound;
    private readonly List<Node> _spawned = new();

    public void Sync(WorldTables? tables)
    {
        if (ReferenceEquals(_bound, tables))
            return;

        Clear();
        _bound = tables;
        if (tables is null)
            return;

        float tileM = tables.TileCm / 100f;
        SpawnPostOffice(tables.PostOffice, tables.Streets, tileM);
        SpawnStreets(tables.Streets, tileM);
        SpawnHouses(tables.Houses, tables.Streets, tileM);
        SpawnMailboxes(tables.Houses, tables.Streets, tileM);
        SpawnIntake(tables.PostOffice, tables.Streets, tileM);
    }

    public void Clear()
    {
        for (int i = 0; i < _spawned.Count; i++)
            _spawned[i].QueueFree();
        _spawned.Clear();
        _bound = null;
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
            dump.AppendLine(label.Text);
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
        var visual = ArtMesh.TryInstantiate(ArtMesh.PostOffice);
        if (visual is not null)
        {
            ArtMesh.FitFootprint(visual, footprint, toward.X, toward.Z, modelFrontIsPlusZ: true, scaleY: true);
            AddLabeled(
                PostOfficeName,
                origin,
                footprint,
                footprint.Y * 0.5f,
                "Post Office",
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
                toward.X,
                toward.Z);
        }

        AddBox(
            Vec(WorldTilePlacement.TileCenter(po.SpawnPadTile, tileM)),
            new Vector3(tileM * 0.9f, 0.12f, tileM * 0.9f),
            SpawnPadGold,
            0.06f);
    }

    private void SpawnIntake(PostOfficeRecord po, StreetRecord[] streets, float tileM)
    {
        var origin = Vec(WorldTilePlacement.TileCenter(po.IntakeTile, tileM));
        var toward = WorldTilePlacement.TowardNearestStreet(origin.X, origin.Z, streets, tileM);
        var visual = ArtMesh.TryInstantiate(ArtMesh.Intake);
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
            toward.X,
            toward.Z);
    }

    private void SpawnStreets(StreetRecord[] streets, float tileM)
    {
        int count = 0;
        for (int s = 0; s < streets.Length; s++)
            count += streets[s].Tiles?.Length ?? 0;
        if (count == 0)
            return;

        var mesh = new BoxMesh { Size = new Vector3(tileM, 0.08f, tileM) };
        var multi = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = mesh,
            InstanceCount = count,
        };

        int i = 0;
        for (int s = 0; s < streets.Length; s++)
        {
            var tiles = streets[s].Tiles;
            if (tiles is null)
                continue;
            for (int t = 0; t < tiles.Length; t++)
            {
                var at = Vec(WorldTilePlacement.TileCenter(tiles[t], tileM));
                multi.SetInstanceTransform(i, new Transform3D(Basis.Identity, new Vector3(at.X, 0.04f, at.Z)));
                i++;
            }
        }

        var node = new MultiMeshInstance3D
        {
            Multimesh = multi,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = StreetAsphalt },
        };
        AddChild(node);
        _spawned.Add(node);
    }

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
            var visual = ArtMesh.TryInstantiate(ArtMesh.HouseVariant(i));
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
            var visual = ArtMesh.TryInstantiate(ArtMesh.Mailbox);
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
        float towardX = 0f,
        float towardZ = 0f,
        float stackHeight = 0f)
    {
        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color },
            Position = new Vector3(0f, heightCenter, 0f),
        };
        return AddLabeled(name, origin, size, heightCenter, labelText, towardX, towardZ, stackHeight, mesh);
    }

    private Node3D AddLabeled(
        string name,
        Vector3 origin,
        Vector3 size,
        float heightCenter,
        string labelText,
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
            Modulate = Colors.White,
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
            MaterialOverride = new StandardMaterial3D { AlbedoColor = HouseRoof },
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
            MaterialOverride = new StandardMaterial3D { AlbedoColor = MailboxFlag },
            Position = new Vector3(at.X, at.Y, at.Z),
        });
    }

    private void AddBox(Vector3 origin, Vector3 size, Color color, float heightCenter)
    {
        var node = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color },
            Position = origin + new Vector3(0f, heightCenter, 0f),
        };
        AddChild(node);
        _spawned.Add(node);
    }

    private static Vector3 VisualSize(Node3D visual, Vector3 fallback)
    {
        var size = ArtMesh.LocalAabb(visual).Size;
        return size.LengthSquared() > 0.01f ? size : fallback;
    }

    private static Vector3 Vec((float X, float Y, float Z) p) => new(p.X, p.Y, p.Z);
}
