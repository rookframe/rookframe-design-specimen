using Godot;

namespace Rookframe.Manager.App;

// Throwaway inspection controls around the existing native content-preview world.
public partial class MockLookPreview : VBoxContainer
{
    internal event Action<bool>? ShapeChanged;
    internal event Action<bool>? CeilingChanged;

    internal void Configure(RegionLook look, MockData data, bool circular, bool ceiling, bool compact)
    {
        AddThemeConstantOverride("separation", 6);
        var complete = look.Encode().Count == 4;
        var stage = GD.Load<PackedScene>(MockUi.Ui + "TabletopContentPreview.tscn")
            .Instantiate<TabletopContentPreview>();
        stage.CustomMinimumSize = new(0, compact ? 108 : 230);
        AddChild(stage);
        stage.MouseFilter = MouseFilterEnum.Stop;
        stage.FocusMode = FocusModeEnum.All;
        stage.AccessibilityName = "3D Look preview";
        stage.TooltipText = "Drag to rotate · arrow keys to rotate and tilt";
        if (complete) stage.SetMockRegion(look, data, circular, ceiling);
        else
        {
            stage.Hide();
            var empty = new Label
            {
                Text = "Choose components to preview",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            AddChild(empty);
        }
        var tools = new HBoxContainer();
        tools.AddThemeConstantOverride("separation", 6);
        AddChild(tools);
        var group = new ButtonGroup();
        foreach (var isCircle in new[] { false, true })
        {
            var button = MockUi.Button(isCircle ? "region-circle" : "region-rectangle",
                isCircle ? "Circular preview" : "Rectangular preview", () => { });
            button.ButtonGroup = group;
            button.ToggleMode = true;
            button.SetPressedNoSignal(circular == isCircle);
            button.Toggled += selected =>
            {
                if (!selected) return;
                circular = isCircle;
                ShapeChanged?.Invoke(circular);
                if (complete) stage.SetMockRegion(look, data, circular, ceiling);
            };
            tools.AddChild(button);
        }
        tools.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var roof = MockUi.Button("eye", "Show ceiling", () => { });
        roof.Icon = AppearanceIcons.SubjectTexture("ceiling");
        roof.ToggleMode = true;
        roof.Disabled = !complete;
        roof.SetPressedNoSignal(ceiling);
        roof.TooltipText = roof.AccessibilityName = ceiling ? "Hide ceiling" : "Show ceiling";
        roof.Toggled += shown =>
        {
            ceiling = shown;
            roof.TooltipText = roof.AccessibilityName = shown ? "Hide ceiling" : "Show ceiling";
            CeilingChanged?.Invoke(shown);
            if (complete) stage.SetMockRegion(look, data, circular, ceiling);
        };
        tools.AddChild(roof);
    }
}
