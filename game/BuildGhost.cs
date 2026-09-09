using Godot;

namespace PerformativeMail.Game;

public partial class BuildGhost : MeshInstance3D
{
    public const string GhostName = "BuildGhost";

    private static readonly Color Valid = new(0.25f, 0.85f, 0.35f, 0.45f);
    private static readonly Color Invalid = new(0.9f, 0.2f, 0.2f, 0.45f);

    public override void _Ready()
    {
        Name = GhostName;
        Mesh = new BoxMesh { Size = new Vector3(2f, 1f, 2f) };
        CastShadow = ShadowCastingSetting.Off;
        Visible = false;
        MaterialOverride = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = Valid,
            Roughness = 1f,
        };
    }

    public void Bind(bool visible, Vector3 origin, float tileMeters, bool valid)
    {
        Visible = visible;
        if (!visible)
            return;

        float size = tileMeters <= 0f ? 2f : tileMeters;
        if (Mesh is BoxMesh box && (Mathf.Abs(box.Size.X - size) > 1e-4f || Mathf.Abs(box.Size.Z - size) > 1e-4f))
            box.Size = new Vector3(size, 1f, size);
        Position = origin + new Vector3(0f, 0.5f, 0f);
        if (MaterialOverride is StandardMaterial3D mat)
            mat.AlbedoColor = valid ? Valid : Invalid;
    }
}
