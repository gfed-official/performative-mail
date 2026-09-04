using Godot;
using PerformativeMail.App;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Game;

public partial class WorldStage : Node3D
{
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
        SpawnPostOffice(tables.PostOffice, tileM);
        SpawnStreets(tables.Streets, tileM);
        SpawnHouses(tables.Houses, tileM);
        SpawnMailboxes(tables.Houses);
        SpawnIntake(tables.PostOffice, tileM);
    }

    public void Clear()
    {
        for (int i = 0; i < _spawned.Count; i++)
            _spawned[i].QueueFree();
        _spawned.Clear();
        _bound = null;
    }

    private void SpawnPostOffice(PostOfficeRecord po, float tileM)
    {
        AddBox(
            FootprintOrigin(po.Tile, po.SizeTiles, tileM),
            new Vector3(po.SizeTiles.X * tileM, 2.4f, po.SizeTiles.Y * tileM),
            new Color(0.55f, 0.28f, 0.22f),
            1.2f);
        AddBox(
            TileCenter(po.SpawnPadTile, tileM),
            new Vector3(tileM * 0.9f, 0.12f, tileM * 0.9f),
            new Color(0.72f, 0.62f, 0.28f),
            0.06f);
    }

    private void SpawnIntake(PostOfficeRecord po, float tileM)
    {
        AddBox(
            TileCenter(po.IntakeTile, tileM),
            new Vector3(0.9f, 1.0f, 0.9f),
            new Color(0.95f, 0.82f, 0.2f),
            0.5f);
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
                var at = TileCenter(tiles[t], tileM);
                multi.SetInstanceTransform(i, new Transform3D(Basis.Identity, new Vector3(at.X, 0.04f, at.Z)));
                i++;
            }
        }

        var node = new MultiMeshInstance3D
        {
            Multimesh = multi,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.38f, 0.38f, 0.4f) },
        };
        AddChild(node);
        _spawned.Add(node);
    }

    private void SpawnHouses(HouseRecord[] houses, float tileM)
    {
        for (int i = 0; i < houses.Length; i++)
        {
            var house = houses[i];
            AddBox(
                FootprintOrigin(house.LotTile, house.LotSizeTiles, tileM),
                new Vector3(house.LotSizeTiles.X * tileM * 0.7f, 1.8f, house.LotSizeTiles.Y * tileM * 0.7f),
                new Color(0.78f, 0.7f, 0.55f),
                0.9f);
        }
    }

    private void SpawnMailboxes(HouseRecord[] houses)
    {
        for (int i = 0; i < houses.Length; i++)
        {
            var pose = houses[i].Mailbox;
            var view = ViewFrame.From(new PlayerPose(pose.XCm, pose.YCm, pose.ZCm, 0));
            AddBox(
                new Vector3(view.X, 0f, view.Z),
                new Vector3(0.28f, 1.15f, 0.28f),
                new Color(0.18f, 0.2f, 0.28f),
                0.57f);
        }
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

    private static Vector3 TileCenter(TileCoord tile, float tileM)
    {
        var view = ViewFrame.From(new PlayerPose(
            (int)((tile.X + 0.5f) * tileM * 100f),
            (int)((tile.Y + 0.5f) * tileM * 100f),
            0,
            0));
        return new Vector3(view.X, 0f, view.Z);
    }

    private static Vector3 FootprintOrigin(TileCoord tile, TileCoord sizeTiles, float tileM)
    {
        float cx = tile.X + sizeTiles.X * 0.5f;
        float cy = tile.Y + sizeTiles.Y * 0.5f;
        var view = ViewFrame.From(new PlayerPose(
            (int)(cx * tileM * 100f),
            (int)(cy * tileM * 100f),
            0,
            0));
        return new Vector3(view.X, 0f, view.Z);
    }
}
