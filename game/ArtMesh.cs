using Godot;
using PerformativeMail.App;
using PerformativeMail.Client;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Vehicles;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Game;

public static class ArtMesh
{
    public const string Mailbox = "res://art/world/mailbox_01.glb";
    public const string Intake = "res://art/world/intake_01.glb";
    public const string PostOffice = "res://art/world/po_01.glb";
    public const string HouseA = "res://art/world/house_a.glb";
    public const string HouseB = "res://art/world/house_b.glb";
    public const string HouseC = "res://art/world/house_c.glb";
    public const string PawnRemote = "res://art/pawns/pawn_remote.glb";
    public const string MailLetter = "res://art/props/mail_letter.glb";
    public const string MailPkgS = "res://art/props/mail_pkg_s.glb";
    public const string MailPkgM = "res://art/props/mail_pkg_m.glb";
    public const string MailPkgL = "res://art/props/mail_pkg_l.glb";
    public const string StreetTile = "res://art/world/street_tile_01.glb";
    public const string StreetCurb = "res://art/world/street_curb_01.glb";
    public const string SpawnPad = "res://art/world/spawn_pad_01.glb";
    public const string GrassTile = "res://art/world/grass_tile_01.glb";
    public const string Crate = "res://art/props/crate_01.glb";
    public const string Cart = "res://art/props/cart_01.glb";
    public const string StreetPole = "res://art/world/street_pole_01.glb";
    public const string Bike = "res://art/props/bike_01.glb";
    public const string MailTruck = "res://art/props/truck_01.glb";
    public const string Rowboat = "res://art/props/rowboat_01.glb";
    public const string Motorboat = "res://art/props/motorboat_01.glb";
    public const string Pier = "res://art/world/pier_01.glb";
    public const string OilPump = "res://art/world/oil_pump_01.glb";
    public const string Port = "res://art/world/port_01.glb";
    public const string ResourceWood = "res://art/world/resource_wood_01.glb";
    public const string ResourceWoodStump = "res://art/world/resource_wood_stump_01.glb";
    public const string ResourceFiber = "res://art/world/resource_fiber_01.glb";
    public const string ResourceStone = "res://art/world/resource_stone_01.glb";
    public const string ResourceIronOre = "res://art/world/resource_iron_ore_01.glb";
    public const string ResourceSand = "res://art/world/resource_sand_01.glb";
    public const string ResourceBerries = "res://art/world/resource_berries_01.glb";

    public const string PawnVestMaterial = "mat_pawn_vest";
    public const string PawnHatMaterial = "mat_pawn_hat";
    public const string DistrictMaterial = "mat_district";
    public const string LodRootName = "ArtLod";
    public const string Lod0Name = "LO0";
    public const string Lod1Name = "LO1";
    public const string Lod2Name = "LO2";
    public const string LodPathMeta = "lod_path";
    public const string LodLevelMeta = "lod_level";
    public const float PostOfficeHeightMeters = 4.5f;
    public const float PierPileSinkMeters = 1.2f;

    private static readonly Dictionary<string, PackedScene> Packed = new();
    private static readonly Dictionary<string, Mesh> Meshes = new();
    private static readonly HashSet<string> Missing = new();

    public static string HouseVariant(int index) =>
        (index % 3) switch
        {
            0 => HouseA,
            1 => HouseB,
            _ => HouseC,
        };

    public static string PathForMail(MailKindId kind)
    {
        if (kind.Equals(MailKinds.SmallPackage))
            return MailPkgS;
        if (kind.Equals(MailKinds.MediumPackage))
            return MailPkgM;
        if (kind.Equals(MailKinds.LargePackage))
            return MailPkgL;
        return MailLetter;
    }

    public static Node3D? TryInstantiate(string path)
    {
        if (Missing.Contains(path))
            return null;

        if (!Packed.TryGetValue(path, out var packed))
        {
            packed = LoadPacked(path);
            if (packed is null)
            {
                Missing.Add(path);
                GD.PushWarning($"Art mesh missing: {path}");
                return null;
            }

            Packed[path] = packed;
        }

        var node = packed.Instantiate();
        if (node is Node3D root)
            return root;

        var wrap = new Node3D();
        wrap.AddChild(node);
        return wrap;
    }

    public static Node3D? TryInstantiateLod(string lo0Path)
    {
        var lo0 = TryInstantiate(lo0Path);
        if (lo0 is null)
            return null;

        var root = new Node3D { Name = LodRootName };
        lo0.Name = Lod0Name;
        root.AddChild(lo0);

        var max = ArtLod.MaxLevel(lo0Path);
        if (max >= ArtLodLevel.Lo1)
            TryAddLodChild(root, ArtLod.Path(lo0Path, ArtLodLevel.Lo1), Lod1Name);
        if (max >= ArtLodLevel.Lo2)
            TryAddLodChild(root, ArtLod.Path(lo0Path, ArtLodLevel.Lo2), Lod2Name);

        ShowLod(root, ArtLodLevel.Lo0);
        root.SetMeta(LodPathMeta, lo0Path);
        root.SetMeta(LodLevelMeta, (int)ArtLodLevel.Lo0);
        return root;
    }

    public static void ApplyLod(Node3D root, float cameraX, float cameraZ)
    {
        string lo0Path = root.HasMeta(LodPathMeta) ? root.GetMeta(LodPathMeta).AsString() : string.Empty;
        float meters = ArtLod.HorizontalMeters(root.GlobalPosition.X, root.GlobalPosition.Z, cameraX, cameraZ);
        ApplyLodLevel(root, ArtLod.Resolve(meters, lo0Path));
    }

    public static void ApplyLod(IReadOnlyList<Node3D> roots, float cameraX, float cameraZ)
    {
        for (int i = 0; i < roots.Count; i++)
            ApplyLod(roots[i], cameraX, cameraZ);
    }

    public static Mesh? TryMesh(string path)
    {
        if (Meshes.TryGetValue(path, out var cached))
            return cached;

        var node = TryInstantiate(path);
        if (node is null)
            return null;

        var mesh = BakeMesh(FindMeshInstance(node));
        node.Free();
        if (mesh is null)
            return null;

        Meshes[path] = mesh;
        return mesh;
    }

    public static string PathForVehicle(VehicleKind kind) => VehicleArt.PathForVehicle(kind);

    public static bool TryPathForConstruct(string defId, out string path)
    {
        switch (defId)
        {
            case "pier":
                path = Pier;
                return true;
            case "pump":
                path = OilPump;
                return true;
            case "port":
            case "small_port":
                path = Port;
                return true;
            default:
                path = "";
                return false;
        }
    }

    public static float InstanceYOffset(string path)
    {
        if (string.Equals(path, Pier, StringComparison.Ordinal))
            return -PierPileSinkMeters;
        return 0f;
    }

    public static string PathForProp(EnvPropKind kind)
    {
        switch (kind)
        {
            case EnvPropKind.Crate:
                return Crate;
            case EnvPropKind.Cart:
                return Cart;
            case EnvPropKind.StreetPole:
                return StreetPole;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    public static string PathForResource(ResourceKind kind, HarvestRemnant remnant)
    {
        if (remnant == HarvestRemnant.Stump && kind == ResourceKind.Wood)
            return ResourceWoodStump;

        switch (kind)
        {
            case ResourceKind.Wood:
                return ResourceWood;
            case ResourceKind.Fiber:
                return ResourceFiber;
            case ResourceKind.Stone:
                return ResourceStone;
            case ResourceKind.IronOre:
                return ResourceIronOre;
            case ResourceKind.Sand:
                return ResourceSand;
            case ResourceKind.Berries:
                return ResourceBerries;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    public static void Orient(Node3D node, float towardX, float towardZ, bool modelFrontIsPlusZ)
    {
        node.Basis = FaceToward(towardX, towardZ, modelFrontIsPlusZ);
    }

    public static void FitFootprint(
        Node3D node,
        Vector3 footprint,
        float towardX,
        float towardZ,
        bool modelFrontIsPlusZ,
        bool scaleY)
    {
        var aabb = LocalAabb(node);
        float nativeX = MathF.Max(aabb.Size.X, 0.001f);
        float nativeY = MathF.Max(aabb.Size.Y, 0.001f);
        float nativeZ = MathF.Max(aabb.Size.Z, 0.001f);
        bool alongX = MathF.Abs(towardX) >= MathF.Abs(towardZ) && (towardX != 0f || towardZ != 0f);
        float scaleX;
        float scaleZ;
        if (modelFrontIsPlusZ && alongX)
        {
            scaleX = footprint.Z / nativeX;
            scaleZ = footprint.X / nativeZ;
        }
        else
        {
            scaleX = footprint.X / nativeX;
            scaleZ = footprint.Z / nativeZ;
        }

        float scaleYAxis = scaleY && footprint.Y > 0f ? footprint.Y / nativeY : 1f;
        node.Basis = FaceToward(towardX, towardZ, modelFrontIsPlusZ)
            .Scaled(new Vector3(scaleX, scaleYAxis, scaleZ));
    }

    public static Aabb LocalAabb(Node3D root)
    {
        Aabb? merged = null;
        Accumulate(root, Transform3D.Identity, isRoot: true);
        return merged ?? new Aabb(Vector3.Zero, Vector3.Zero);

        void Accumulate(Node node, Transform3D xf, bool isRoot)
        {
            var next = xf;
            if (!isRoot && node is Node3D n3)
            {
                if (!n3.Visible)
                    return;
                next = xf * n3.Transform;
            }

            if (node is VisualInstance3D vis)
            {
                var box = XformAabb(next, vis.GetAabb());
                merged = merged is null ? box : merged.Value.Merge(box);
            }

            foreach (var child in node.GetChildren())
                Accumulate(child, next, isRoot: false);
        }
    }

    public static void ApplyPawnKitColor(Node root, Color color)
    {
        int named = 0;
        Tint(root, color, requireName: true, ref named);
        if (named == 0)
            Tint(root, color, requireName: false, ref named);
    }

    public static void ApplyDistrictColor(Node root, Color color)
    {
        int applied = 0;
        TintNamed(root, DistrictMaterial, color, ref applied);
    }

    private static void TryAddLodChild(Node3D root, string path, string name)
    {
        var child = TryInstantiate(path);
        if (child is null)
            return;
        child.Name = name;
        child.Visible = false;
        root.AddChild(child);
    }

    private static void ApplyLodLevel(Node3D root, ArtLodLevel level)
    {
        var shown = ArtLod.Fallback(
            level,
            root.GetNodeOrNull<Node3D>(Lod1Name) is not null,
            root.GetNodeOrNull<Node3D>(Lod2Name) is not null);
        if (root.HasMeta(LodLevelMeta) && (ArtLodLevel)root.GetMeta(LodLevelMeta).AsInt32() == shown)
            return;
        ShowLod(root, shown);
        root.SetMeta(LodLevelMeta, (int)shown);
    }

    private static void ShowLod(Node3D root, ArtLodLevel level)
    {
        var lo0 = root.GetNodeOrNull<Node3D>(Lod0Name);
        var lo1 = root.GetNodeOrNull<Node3D>(Lod1Name);
        var lo2 = root.GetNodeOrNull<Node3D>(Lod2Name);
        var shown = ArtLod.Fallback(level, lo1 is not null, lo2 is not null);
        if (lo0 is not null)
            lo0.Visible = shown == ArtLodLevel.Lo0;
        if (lo1 is not null)
            lo1.Visible = shown == ArtLodLevel.Lo1;
        if (lo2 is not null)
            lo2.Visible = shown == ArtLodLevel.Lo2;
    }

    private static MeshInstance3D? FindMeshInstance(Node node)
    {
        if (node is MeshInstance3D inst && inst.Mesh is not null)
            return inst;
        foreach (var child in node.GetChildren())
        {
            if (FindMeshInstance(child) is { } found)
                return found;
        }

        return null;
    }

    private static Mesh? BakeMesh(MeshInstance3D? inst)
    {
        if (inst?.Mesh is not { } mesh)
            return null;

        int surfaces = mesh.GetSurfaceCount();
        Mesh? baked = null;
        for (int i = 0; i < surfaces; i++)
        {
            var active = inst.GetActiveMaterial(i);
            var onMesh = mesh.SurfaceGetMaterial(i);
            if (active is null || ReferenceEquals(active, onMesh))
                continue;
            baked ??= (Mesh)mesh.Duplicate();
            baked.SurfaceSetMaterial(i, active);
        }

        return baked ?? mesh;
    }

    private static PackedScene? LoadPacked(string path)
    {
        if (ResourceLoader.Exists(path) && ResourceLoader.Load(path) is PackedScene imported)
            return imported;

        var doc = new GltfDocument();
        var state = new GltfState();
        if (doc.AppendFromFile(path, state) != Error.Ok)
            return null;
        if (doc.GenerateScene(state) is not Node generated)
            return null;

        OwnDescendants(generated, generated);
        var packed = new PackedScene();
        return packed.Pack(generated) == Error.Ok ? packed : null;
    }

    private static void OwnDescendants(Node node, Node owner)
    {
        foreach (var child in node.GetChildren())
        {
            child.Owner = owner;
            OwnDescendants(child, owner);
        }
    }

    private static Basis FaceToward(float towardX, float towardZ, bool modelFrontIsPlusZ)
    {
        var dir = new Vector3(towardX, 0f, towardZ);
        if (dir.LengthSquared() < 1e-8f)
            return Basis.Identity;
        return Basis.LookingAt(dir, Vector3.Up, modelFrontIsPlusZ);
    }

    private static Aabb XformAabb(Transform3D xf, Aabb box)
    {
        var result = new Aabb(xf * box.Position, Vector3.Zero);
        Vector3 size = box.Size;
        result = result.Expand(xf * (box.Position + new Vector3(size.X, 0f, 0f)));
        result = result.Expand(xf * (box.Position + new Vector3(0f, size.Y, 0f)));
        result = result.Expand(xf * (box.Position + new Vector3(0f, 0f, size.Z)));
        result = result.Expand(xf * (box.Position + new Vector3(size.X, size.Y, 0f)));
        result = result.Expand(xf * (box.Position + new Vector3(size.X, 0f, size.Z)));
        result = result.Expand(xf * (box.Position + new Vector3(0f, size.Y, size.Z)));
        return result.Expand(xf * (box.Position + size));
    }

    private static void Tint(Node node, Color color, bool requireName, ref int applied)
    {
        if (node is MeshInstance3D mesh)
        {
            int surfaces = mesh.Mesh?.GetSurfaceCount() ?? 0;
            for (int i = 0; i < surfaces; i++)
            {
                if (mesh.GetActiveMaterial(i) is not StandardMaterial3D std)
                    continue;
                bool kit = IsKitSlot(std.ResourceName) || IsKitSlot(mesh.Name);
                if (requireName && !kit)
                    continue;
                if (!requireName && i > 0)
                    continue;

                var copy = (StandardMaterial3D)std.Duplicate();
                copy.AlbedoColor = color;
                mesh.SetSurfaceOverrideMaterial(i, copy);
                applied++;
            }
        }

        foreach (var child in node.GetChildren())
            Tint(child, color, requireName, ref applied);
    }

    private static void TintNamed(Node node, string slot, Color color, ref int applied)
    {
        if (node is MeshInstance3D mesh)
        {
            int surfaces = mesh.Mesh?.GetSurfaceCount() ?? 0;
            for (int i = 0; i < surfaces; i++)
            {
                if (mesh.GetActiveMaterial(i) is not BaseMaterial3D mat)
                    continue;
                if (!IsNamedSlot(mat.ResourceName, slot)
                    && !IsNamedSlot(mesh.Name, slot)
                    && !IsNamedSlot(mesh.Mesh?.SurfaceGetMaterial(i)?.ResourceName, slot))
                    continue;

                var copy = (BaseMaterial3D)mat.Duplicate();
                copy.AlbedoColor = color;
                mesh.SetSurfaceOverrideMaterial(i, copy);
                applied++;
            }
        }

        foreach (var child in node.GetChildren())
            TintNamed(child, slot, color, ref applied);
    }

    private static bool IsKitSlot(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        return name.Contains(PawnVestMaterial, StringComparison.OrdinalIgnoreCase)
            || name.Contains(PawnHatMaterial, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNamedSlot(string? name, string slot)
    {
        return !string.IsNullOrEmpty(name)
            && name.Contains(slot, StringComparison.OrdinalIgnoreCase);
    }
}
