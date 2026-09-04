using System.Text;
using Godot;
using PerformativeMail.App;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Game;

public partial class WorldStage : Node3D
{
    public const string RootName = "WorldStage";
    public const string PostOfficeName = "PostOffice";
    public const string MailIntakeName = "MailIntake";
    public const string StreetName = "Street";
    public const string HousePrefix = "House_";
    public const string MailboxPrefix = "Mailbox_";

    private WorldAtlas? _atlas;

    public WorldAtlas? Atlas => _atlas;

    public override void _Ready()
    {
        Name = RootName;
        Build(LoadAtlas());
    }

    public string Dump()
    {
        var text = new StringBuilder();
        text.AppendLine("WORLD_DUMP");
        text.Append("atlas=").Append(_atlas?.Id ?? "").Append('\n');
        foreach (var child in GetChildren())
        {
            if (child is not Node3D node)
                continue;
            text.Append(node.Name).Append(' ')
                .Append(FormatOrigin(node.Position)).Append('\n');
            if (node.GetNodeOrNull<Label3D>("Label") is Label3D label)
                text.Append("  label=").Append(label.Text).Append('\n');
        }

        text.AppendLine("WORLD_DUMP_END");
        return text.ToString();
    }

    private void Build(WorldAtlas atlas)
    {
        _atlas = atlas;
        foreach (var child in GetChildren())
            child.QueueFree();

        AddStreet(atlas);
        AddPostOffice(atlas);
        AddMailIntake(atlas);
        foreach (var house in atlas.Houses.Values)
        {
            AddHouse(atlas, house);
            AddMailbox(atlas, house);
        }
    }

    private void AddStreet(WorldAtlas atlas)
    {
        var rect = atlas.StreetRect;
        var size = TileSize(atlas, rect.Width, rect.Height);
        var center = TileCenter(atlas, rect.X, rect.Y, rect.Width, rect.Height);
        var mesh = Box(StreetName, size, new Color(0.35f, 0.35f, 0.38f), center, 0.05f);
        AddChild(mesh);
    }

    private void AddPostOffice(WorldAtlas atlas)
    {
        var po = atlas.PostOffice;
        var size = TileSize(atlas, po.SizeTiles.X, po.SizeTiles.Y);
        var center = TileCenter(atlas, po.Tile.X, po.Tile.Y, po.SizeTiles.X, po.SizeTiles.Y);
        var root = Box(PostOfficeName, size, new Color(0.72f, 0.55f, 0.32f), center, 2.4f);
        root.AddChild(LabelAt("Post Office", new Vector3(0f, 1.6f, 0f)));
        AddChild(root);
    }

    private void AddMailIntake(WorldAtlas atlas)
    {
        var intake = atlas.PostOffice.IntakeTile;
        var center = TileCenter(atlas, intake.X, intake.Y, 1, 1);
        var root = Box(MailIntakeName, new Vector3(0.9f, 1.2f, 0.9f), new Color(0.85f, 0.75f, 0.25f), center, 1.2f);
        root.AddChild(LabelAt("Mail", new Vector3(0f, 1.0f, 0f)));
        AddChild(root);
    }

    private void AddHouse(WorldAtlas atlas, HouseRecord house)
    {
        var lot = house.Lot;
        var size = TileSize(atlas, lot.Width, lot.Height);
        var center = TileCenter(atlas, lot.X, lot.Y, lot.Width, lot.Height);
        string address = FormatAddress(atlas, house.Address);
        var root = Box(HousePrefix + house.Address.Number, size, new Color(0.78f, 0.70f, 0.58f), center, 2.0f);
        root.AddChild(LabelAt(address, new Vector3(0f, 1.4f, 0f)));
        AddChild(root);
    }

    private void AddMailbox(WorldAtlas atlas, HouseRecord house)
    {
        string address = FormatAddress(atlas, house.Address);
        var pose = house.Mailbox;
        var feet = ViewFrame.From(new PlayerPose(pose.XCm, pose.YCm, pose.ZCm, 0));
        var root = new Node3D
        {
            Name = MailboxPrefix + house.Address.Number,
            Position = new Vector3(feet.X, 0f, feet.Z),
        };
        var post = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.35f, 1.1f, 0.35f) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.15f, 0.25f, 0.55f) },
            Position = new Vector3(0f, 0.55f, 0f),
        };
        root.AddChild(post);
        root.AddChild(LabelAt(address, new Vector3(0f, 1.35f, 0f)));
        AddChild(root);
    }

    private static Node3D Box(string name, Vector3 size, Color color, Vector3 center, float height)
    {
        var root = new Node3D { Name = name, Position = center };
        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(size.X, height, size.Z) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color },
            Position = new Vector3(0f, height * 0.5f, 0f),
        };
        root.AddChild(mesh);
        return root;
    }

    private static Label3D LabelAt(string text, Vector3 offset) =>
        new()
        {
            Name = "Label",
            Text = text,
            Position = offset,
            FontSize = 42,
            OutlineSize = 6,
            Modulate = Colors.White,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        };

    private static Vector3 TileSize(WorldAtlas atlas, int widthTiles, int heightTiles)
    {
        float meters = atlas.TileCm / 100f;
        return new Vector3(widthTiles * meters, 1f, heightTiles * meters);
    }

    private static Vector3 TileCenter(WorldAtlas atlas, int tileX, int tileY, int widthTiles, int heightTiles)
    {
        float meters = atlas.TileCm / 100f;
        float x = (tileX + widthTiles * 0.5f) * meters;
        float ySim = (tileY + heightTiles * 0.5f) * meters;
        var view = ViewFrame.From(PlayerPose.FromMeters(x, ySim, 0, 0));
        return new Vector3(view.X, 0f, view.Z);
    }

    private static string FormatAddress(WorldAtlas atlas, AddressId address) =>
        $"{address.Number} {atlas.StreetName}";

    private static string FormatOrigin(Vector3 origin) =>
        $"{origin.X:0.##},{origin.Y:0.##},{origin.Z:0.##}";

    private static WorldAtlas LoadAtlas()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "content", "world", "m0_test_map.json");
                if (File.Exists(candidate))
                    return WorldAtlasLoader.LoadFile(candidate);
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException("content/world/m0_test_map.json");
    }
}
