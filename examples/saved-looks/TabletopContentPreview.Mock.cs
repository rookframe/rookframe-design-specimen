using Godot;
using D = Godot.Collections.Dictionary;

namespace Rookframe.Manager.App;

// Keep the application's authored preview world, lighting and camera framing.
// Only the sample mesh changes for material inspection in this prototype.
public partial class TabletopContentPreview
{
    internal void SetMockWall(D entry, MockData data)
    {
        // Reuse the painter's actual wall assembly: full-height Surface and Top
        // placement. Raw authored scenes contain UV samples and unplaced Top parts.
        var swatch = GD.Load<PackedScene>(MockUi.Ui + "RegionPaletteSwatch.tscn")
            .Instantiate<RegionPaletteSwatch>();
        swatch.Hide();
        AddChild(swatch);
        swatch.SetEntry(entry, data.Content);
        var model = swatch.GetNode<Node3D>("Preview/Model");
        model.Owner = null;
        model.Reparent(_presentation!, false);
        swatch.QueueFree();
        RegisterMockModel(entry, model);
    }

    internal void SetMockMaterialSphere(D entry, MockData data)
    {
        var appearance = RoomAppearance.Load(MockData.Ref(entry), "surface_finish", data.Content);
        var sphere = new MeshInstance3D
        {
            Name = "MaterialSphere",
            Mesh = new SphereMesh
            {
                Radius = .75f,
                Height = 1.5f,
                RadialSegments = 64,
                Rings = 32,
                Material = appearance.Material
            }
        };
        _presentation!.AddChild(sphere);
        RegisterMockModel(entry, sphere);
    }

    private void RegisterMockModel(D entry, Node3D model)
    {
        _entry = entry.Duplicate(true);
        var key = PreviewKey(_entry);
        _presentationsByKey[key] = model;
        _activePresentationKey = key;
        MarkPresentationUsed(key);
        _fallback!.Hide();
        RequestPreviewFrame();
    }
}
