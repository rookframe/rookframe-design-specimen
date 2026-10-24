using Godot;

namespace Rookframe.Manager.App;

// Throwaway inspection controls around the existing native content-preview world.
public partial class MockLookPreview : VBoxContainer
{
    internal event Action<bool>? ShapeChanged;
    internal event Action<bool>? CeilingChanged;
    private TabletopContentPreview _stage = null!;
    private Label _empty = null!;
    private Button _roof = null!;
    private RegionLook _look = new();
    private MockData _data = null!;
    private bool _circular, _ceiling;

    internal void Configure(RegionLook look, MockData data, bool circular, bool ceiling, bool compact)
    {
        _data = data; _circular = circular; _ceiling = ceiling;
        AddThemeConstantOverride("separation", compact ? 4 : 6);
        _stage = GD.Load<PackedScene>(MockUi.Ui + "TabletopContentPreview.tscn")
            .Instantiate<TabletopContentPreview>();
        _stage.CustomMinimumSize = new(0, compact ? 72 : 230);
        AddChild(_stage);
        _stage.MouseFilter = MouseFilterEnum.Stop;
        _stage.FocusMode = FocusModeEnum.All;
        _stage.AccessibilityName = "3D Look preview";
        _stage.TooltipText = "Drag to rotate · arrow keys to rotate and tilt";
        var focus = new Panel { MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        focus.AddThemeStyleboxOverride("panel", MockUi.Controls.GetStylebox("focus", "RookframeActionIcon"));
        _stage.AddChild(focus);
        focus.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _stage.FocusEntered += focus.Show;
        _stage.FocusExited += focus.Hide;
        _empty = new Label
        {
            Text = "Choose components to preview",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        AddChild(_empty);
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
                _circular = isCircle;
                ShapeChanged?.Invoke(_circular);
                Refresh();
            };
            tools.AddChild(button);
        }
        tools.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _roof = MockUi.Button("eye", "Show ceiling", () => { });
        _roof.Icon = AppearanceIcons.SubjectTexture("ceiling");
        _roof.ToggleMode = true;
        _roof.SetPressedNoSignal(ceiling);
        _roof.Toggled += shown =>
        {
            _ceiling = shown;
            CeilingChanged?.Invoke(shown);
            Refresh();
        };
        tools.AddChild(_roof);
        UpdateLook(look);
    }
    internal void UpdateLook(RegionLook look)
    {
        _look = look;
        Refresh();
    }
    internal void InspectRole(string role)
    {
        if (role is not ("wall" or "exterior_wall" or "floor" or "ceiling")) return;
        _ceiling = role is "exterior_wall" or "ceiling";
        _roof.SetPressedNoSignal(_ceiling);
        CeilingChanged?.Invoke(_ceiling);
        Refresh();
    }
    private void Refresh()
    {
        var complete = _look.Encode().Count == 5;
        _stage.Visible = complete;
        _empty.Visible = !complete;
        _roof.Disabled = !complete;
        _roof.TooltipText = _roof.AccessibilityName = _ceiling ? "Show interior" : "Show exterior and ceiling";
        if (complete) _stage.SetMockRegion(_look, _data, _circular, _ceiling);
    }
}
