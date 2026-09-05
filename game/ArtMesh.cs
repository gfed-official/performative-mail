using Godot;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Mail;

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

    public const string PawnVestMaterial = "mat_pawn_vest";
    public const string PawnHatMaterial = "mat_pawn_hat";
    public const float PostOfficeHeightMeters = 4.5f;

    private static readonly Dictionary<string, PackedScene> Packed = new();
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
                next = xf * n3.Transform;

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

    private static bool IsKitSlot(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        return name.Contains(PawnVestMaterial, StringComparison.OrdinalIgnoreCase)
            || name.Contains(PawnHatMaterial, StringComparison.OrdinalIgnoreCase);
    }
}
