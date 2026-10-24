using Godot;
using D = Godot.Collections.Dictionary;

namespace Rookframe.Manager.App;

public partial class TabletopContentPreview
{
    private sealed record MockRegionModel(Node3D Root, RoomShell Shell,
        MeshInstance3D Structure, MeshInstance3D Caps, ShaderMaterial Ceiling,
        ShaderMaterial[] Walls);
    private readonly Dictionary<string, MockRegionModel> _mockRegions = [];
    private MockRegionModel? _mockRegion;
    private bool _mockCeiling, _mockOrbitBound;
    private float _mockYaw = .55f, _mockElevation = .65f;

    internal void SetMockRegion(RegionLook look, MockData data, bool circular, bool ceiling)
    {
        var key = string.Join("|", circular, look.Wall, look.ExteriorWall, look.Floor, look.Ceiling, look.Foundation);
        if (!_mockRegions.TryGetValue(key, out var model))
        {
            var plan = new RoomDraft(Transform3D.Identity, new(-3, -2.4f, 6, 4.8f),
                look.Wall!, look.Floor!, look.Ceiling!, .2, 0, 2.8,
                Curve: circular ? new RoomCurve("Circle", Vector2.Zero, Vector2.One * 2.8f) : null,
                FoundationThickness: .4, Foundation: look.Foundation!);
            plan = look.Apply(plan);
            var shell = RoomShell.Build(plan, data.Content);
            var root = new Node3D { Name = circular ? "CircularRegion" : "RectangularRegion" };
            var structure = new MeshInstance3D { Mesh = shell.Mesh };
            var decoration = new MeshInstance3D { Mesh = shell.Decoration };
            var caps = new MeshInstance3D();
            root.AddChild(structure); root.AddChild(decoration); root.AddChild(caps);
            var roof = InteriorRevealMaterial.Create((BaseMaterial3D)shell.Mesh.SurfaceGetMaterial(1), groupOnly: true);
            structure.SetSurfaceOverrideMaterial(1, roof);
            var walls = new List<ShaderMaterial>();
            void bind(MeshInstance3D mesh, int surface)
            {
                var material = InteriorRevealMaterial.Create((BaseMaterial3D)mesh.Mesh.SurfaceGetMaterial(surface));
                material.SetShaderParameter("cutaway_ranges", shell.Cutaway.Texture);
                material.SetShaderParameter("cutaway_floor", 0f);
                material.SetShaderParameter("cutaway_slope", RoomCutaway.Slope);
                mesh.SetSurfaceOverrideMaterial(surface, material);
                walls.Add(material);
            }
            bind(structure, 2);
            for (var surface = 4; surface < shell.Mesh.GetSurfaceCount(); surface++) bind(structure, surface);
            for (var surface = 0; surface < (decoration.Mesh?.GetSurfaceCount() ?? 0); surface++) bind(decoration, surface);
            _presentation!.AddChild(root);
            model = new(root, shell, structure, caps, roof, walls.ToArray());
            _mockRegions.Add(key, model);
            RegisterMockModel(new D { ["reference"] = key }, root);
        }
        foreach (var cached in _mockRegions.Values) cached.Root.Visible = cached == model;
        _mockRegion = model;
        _mockCeiling = ceiling;
        _entry = new D { ["reference"] = key };
        _activePresentationKey = PreviewKey(_entry);
        _fallback!.Hide();
        if (!_mockOrbitBound)
        {
            _mockOrbitBound = true;
            GuiInput += OrbitMockRegion;
            _previewViewport!.SizeChanged += UpdateMockRegionView;
        }
        UpdateMockRegionView();
    }

    private void OrbitMockRegion(InputEvent input)
    {
        var change = Vector2.Zero;
        if (input is InputEventMouseMotion motion && motion.ButtonMask.HasFlag(MouseButtonMask.Left))
            change = motion.Relative * .008f;
        else if (input is InputEventKey { Pressed: true } key)
            change = key.Keycode switch
            {
                Key.Left => new(-.15f, 0), Key.Right => new(.15f, 0),
                Key.Up => new(0, -.12f), Key.Down => new(0, .12f), _ => Vector2.Zero
            };
        else if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) GrabFocus();
        if (change == Vector2.Zero) return;
        _mockYaw -= change.X;
        _mockElevation = Mathf.Clamp(_mockElevation + change.Y, .18f, 1.3f);
        UpdateMockRegionView();
        AcceptEvent();
    }

    private void UpdateMockRegionView()
    {
        if (_mockRegion is not { } model || _camera is null || !_camera.IsInsideTree()) return;
        var center = new Vector3(0, 1.2f, 0);
        _camera.Position = center + new Vector3(Mathf.Sin(_mockYaw) * Mathf.Cos(_mockElevation),
            Mathf.Sin(_mockElevation), Mathf.Cos(_mockYaw) * Mathf.Cos(_mockElevation)) * 15;
        _camera.LookAt(center);
        model.Ceiling.SetShaderParameter("group_visibility", _mockCeiling ? 1f : 0f);
        foreach (var material in model.Walls) material.SetShaderParameter("context_cutaway", _mockCeiling ? 0f : 1f);
        model.Caps.Visible = !_mockCeiling;
        if (!_mockCeiling)
        {
            model.Shell.Cutaway.Update(Basis.Identity, _camera.GlobalBasis.Z, null);
            model.Caps.Mesh = model.Shell.Cutaway.Caps();
        }
        RequestPreviewFrame();
    }
}
