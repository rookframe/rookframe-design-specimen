using Godot;
using Rookframe.Manager.Core;
using D = Godot.Collections.Dictionary;
namespace Rookframe.Manager.App;

// Added partial only: the production Manager scene and both source files are consumed unchanged.
public partial class AppearanceManagement
{
    private MockData _mock = null!;
    private HBoxContainer _copiedRow = null!;
    private Button _newLook = null!, _categorySettings = null!;
    private ScrollContainer _lookBrowser = null!;
    private VBoxContainer _lookGroups = null!;
    private bool _categoryEditing;
    private string _browserKey = "";
    private Control? _lookEditor;
    private string? _editingReference;
    private RegionLook _draft = new();
    private string _draftName = "", _draftCategory = "all";
    private bool _readOnly;
    private bool _previewCircular, _previewCeiling;
    private LineEdit? _lookName;
    private Button? _saveLook;
    private string _pickerRole = "wall";
    private static readonly string[] MockEditorRoles = ["wall", "exterior_wall", "floor", "ceiling", "foundation", "columns", "brackets"];
    private MockLookPreview? _regionPreview;
    private VBoxContainer? _componentPane;
    private Label? _componentTitle, _componentChoice;
    private readonly Dictionary<string, Button> _componentButtons = [];
    private readonly HashSet<ulong> _augmentedRows = [];
    internal bool MockEditing => _lookEditor is not null;

    internal void ConfigureMock(MockData data)
    {
        _mock = data;
        GetTree().ProcessFrame += UpdateMock;
        TreeExiting += () => GetTree().ProcessFrame -= UpdateMock;
        SubjectTabs.SetTabTitle(4, "Looks");
        SubjectTabs.SetTabHidden(5, true);
        SubjectNames["region"] = "Looks";
        _sources[MockData.Personal] = true;
        var toolbar = GetNode<HBoxContainer>(Styles + "/Toolbar");
        _newLook = MockUi.Button("add", "Compose a Look", () => EditMockLook());
        toolbar.AddChild(_newLook);
        _categorySettings=MockUi.Button("edit","Edit selected category",()=>{_categoryEditing=!_categoryEditing;_browserKey="";UpdateMock();});
        _categorySettings.ToggleMode=true;
        GetNode<HBoxContainer>(Categories+"/Heading").AddChild(_categorySettings);
        GetNode<Button>(Categories+"/Heading/Add").Pressed+=()=>{_categoryEditing=true;_browserKey="";};
        _lookBrowser=new ScrollContainer {Name="LooksBrowser",SizeFlagsVertical=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};
        _lookGroups=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};
        _lookGroups.AddThemeConstantOverride("separation",28);_lookBrowser.AddChild(_lookGroups);
        GetNode<VBoxContainer>(Styles).AddChild(_lookBrowser);
        _lookBrowser.GetVScrollBar().Theme=GD.Load<Theme>("res://rookframe/ui/theme/silkbound_theme.tres");
        _copiedRow = new HBoxContainer { Name = "CopiedLook", CustomMinimumSize = new(0,76) };
        _copiedRow.AddThemeConstantOverride("separation", 12);
        var styles = GetNode<VBoxContainer>(Styles); styles.AddChild(_copiedRow); styles.MoveChild(_copiedRow, 1);
        var mobileNew = MockUi.Button("layers", "Compose a Look", () => EditMockLook());
        mobileNew.Name = "ComposeLook"; MobileNode<HBoxContainer>("Header").AddChild(mobileNew);
        var mobileHeader=MobileNode<HBoxContainer>("Header");
        mobileHeader.MoveChild(MobileNode<Button>("Header/Close"),mobileHeader.GetChildCount()-1);
        Closed += EndMockEdit;
        Resized += () => { if (_lookEditor is not null) LayoutMockEditor(); };
        data.Changed += () =>
        {
            _entries = data.Entries.Select(v => v.AsGodotDictionary()).ToArray();
            if (Visible && _state is not null && _lookEditor is null)
            {
                RenderCategories(); RenderStyles();
                if (_mobileLayout) RenderMobile();
            }
        };
        RefreshMockCopy();
    }
    internal void RefreshMockCopy()
    {
        MockUi.Clear(_copiedRow);
        if (_mock.Copied is null || _mock.CopiedSaved) return;
        _copiedRow.AddChild(MockUi.LookDisc(_mock.Copied, _mock, 48));
        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        text.AddChild(MockUi.Label("Copied Look", 23));
        text.AddChild(MockUi.Label(_mock.CopiedSaved ? "Already in your collection" : "Not saved", 17));
        _copiedRow.AddChild(text);
        if (!_mock.CopiedSaved) _copiedRow.AddChild(MockUi.Button("save", "Save copied Look", () => EditMockLook(_mock.Copied)));
    }
    private void UpdateMock()
    {
        if (_mock is null || !Visible) return;
        if (_lookEditor is not null)
        {
            GetNode<Control>("Mobile").Hide();
            return;
        }
        var looks = _subject is "region" or "building";
        var copyParent = _mobileLayout ? GetNode<VBoxContainer>("Mobile/Inset/Column") : GetNode<VBoxContainer>(Styles);
        if (_copiedRow.GetParent()!=copyParent){_copiedRow.Reparent(copyParent);copyParent.MoveChild(_copiedRow,1);}
        _copiedRow.CustomMinimumSize=new(0,_mobileLayout?58:76);
        _newLook.Visible = looks;
        _copiedRow.Visible = looks && _mock.Copied is not null && !_mock.CopiedSaved && !_categoryEditing
            && (!_mobileLayout || _mobileLocation.Page is MobilePage.Categories or MobilePage.Styles or MobilePage.Sources);
        GetNode<Label>(Categories+"/Heading/Title").Text=looks?"Categories":"Wheel categories";
        _categorySettings.Visible=looks;
        _categorySettings.Disabled=_category==AppearancePalette.Pinned;
        if(_categorySettings.Disabled)_categoryEditing=false;
        _categorySettings.SetPressedNoSignal(_categoryEditing);
        GetNode<Control>(Styles+"/Toolbar/Filter").Visible=!looks;
        GetNode<Control>(Styles+"/Context").Visible=!looks || _categoryEditing;
        GetNode<Control>(Styles+"/Scroll").Visible=!looks || _categoryEditing;
        _lookBrowser.Visible=looks && !_categoryEditing;
        if(looks)GetNode<Control>(Categories+"/Properties").Visible=_categoryEditing;
        else GetNode<Control>(Categories+"/Properties").Visible=_category!=AppearancePalette.Pinned;
        Search.PlaceholderText=looks?"Find a Look…":"Search styles or sources…";
        if(looks && !_mobileLayout && !_categoryEditing)RenderLookBrowser();
        MobileNode<Button>("Header/ComposeLook").Visible = looks;
        if (!looks) return;
        foreach(var label in Mobile.FindChildren("*","Label",true,false).OfType<Label>())
            if(label.Text=="Content Package" && _mobileLocation.Source==MockData.Personal)label.Text="Saved in this World";
        foreach (var source in GetNode<Node>(Styles + "/Scroll/Sources").GetChildren())
        {
            var header = source.GetNodeOrNull<Label>("Header/Name");
            if (header?.Text == "My Looks") source.GetNode<Label>("Header/Detail").Text = "Saved in this World";
            var grid = source.GetNodeOrNull<GridContainer>("Styles"); if (grid is null) continue;
            // Inspector actions need space beside the unchanged name / pin / membership controls.
            grid.Columns = Size.X < 1500 ? 1 : 2;
            foreach (var row in grid.GetChildren().OfType<HBoxContainer>())
            {
                AugmentMockRow(row);
                row.GetNode<Control>("Pin").Hide();
                row.GetNode<LineEdit>("Name").Editable=false;
            }
        }
        foreach (var row in MobileRows.GetChildren().OfType<Control>())
        {
            var rowLabels=row.FindChildren("*","Label",true,false).OfType<Label>().ToArray();
            if(rowLabels.Any(l=>l.Text=="My Looks"))
                foreach(var detail in rowLabels.Where(l=>l.Text.StartsWith("Content Package")))
                    detail.Text=detail.Text.Replace("Content Package","Saved in this World");
            if (_augmentedRows.Contains(row.GetInstanceId())) continue;
            // Native mobile rows expose their preview as the inspection affordance.
            var name = row.FindChildren("*", "Label", true, false).OfType<Label>()
                .Select(l => l.Text).FirstOrDefault(t => _mock.AllLooks.Any(e => DisplayName(e) == t));
            if (name is null) continue;
            var entry = _mock.AllLooks.First(e => DisplayName(e) == name);
            var preview = row.GetNodeOrNull<Button>("Name");
            if (preview is not null)
            {
                foreach (var connection in preview.GetSignalConnectionList(Button.SignalName.Pressed))
                    preview.Disconnect(Button.SignalName.Pressed,connection["callable"].AsCallable());
                preview.TooltipText=preview.AccessibilityName="Inspect "+name;
                preview.MouseFilter = MouseFilterEnum.Stop; preview.FocusMode = FocusModeEnum.All;
                preview.Pressed += () => EditMockLook(_mock.Look(entry), MockData.Ref(entry));
            }
            row.GetNodeOrNull<Control>("Pin")?.Hide();
            row.GetNodeOrNull<Control>("Include")?.Hide();
            row.GetNodeOrNull<Control>("Name/Row/Edit")?.Hide();
            _augmentedRows.Add(row.GetInstanceId());
        }
    }
    private void AugmentMockRow(HBoxContainer row)
    {
        if (!_augmentedRows.Add(row.GetInstanceId())) return;
        var field = row.GetNode<LineEdit>("Name");
        var entry = _mock.AllLooks.FirstOrDefault(e => DisplayName(e) == field.Text);
        if (entry is null) return;
        var personal = MockData.Text(entry, "packageId") == MockData.Personal;
        field.Editable = personal;
        field.AddThemeColorOverride("font_uneditable_color",field.GetThemeColor("font_color"));
        var preview = row.GetNode<RegionPaletteButton>("Preview");
        preview.MouseFilter = MouseFilterEnum.Stop; preview.FocusMode = FocusModeEnum.All;
        preview.Pressed += () => EditMockLook(_mock.Look(entry), MockData.Ref(entry));
    }
    private void RenderLookBrowser()
    {
        if(_state is null)return;
        var selected=_state.Categories.FirstOrDefault(c=>c.Id==_category);
        if(selected is null)return;
        var entries=_mock.AllLooks.Where(e=>(_category=="all" || selected.Items.Contains(Reference(e)))
            && Search.Text.Split(' ',StringSplitOptions.RemoveEmptyEntries).All(term=>SearchText(e).Contains(term,StringComparison.OrdinalIgnoreCase)))
            .OrderBy(e=>Text(e,"packageId")==MockData.Personal?0:1).ThenBy(DisplayName).ToArray();
        var columns=Math.Clamp((int)(_lookBrowser.Size.X/210),2,4);
        var key=_category+"|"+Search.Text+"|"+columns+"|"+string.Join(";",entries.Select(e=>Reference(e)+"="+DisplayName(e)));
        if(key==_browserKey)return;_browserKey=key;MockUi.Clear(_lookGroups);
        foreach(var group in entries.GroupBy(e=>Text(e,"packageId")))
        {
            var section=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};section.AddThemeConstantOverride("separation",12);
            var heading=MockUi.Label(group.Key==MockData.Personal?"My Looks":Text(group.First(),"packageName"),22);
            heading.Modulate=new Color(1,1,1,.72f);section.AddChild(heading);
            var grid=new GridContainer {Columns=columns,SizeFlagsHorizontal=SizeFlags.ExpandFill};grid.AddThemeConstantOverride("h_separation",20);grid.AddThemeConstantOverride("v_separation",16);section.AddChild(grid);
            foreach(var entry in group)
            {
                var card=new Button {Theme=GD.Load<Theme>("res://rookframe/ui/theme/silkbound_theme.tres"),ThemeTypeVariation="RookframeQuietButton",
                    CustomMinimumSize=new(180,150),SizeFlagsHorizontal=SizeFlags.ExpandFill,TooltipText=DisplayName(entry),AccessibilityName="Open "+DisplayName(entry),MouseDefaultCursorShape=CursorShape.PointingHand};
                var inset=new MarginContainer {MouseFilter=MouseFilterEnum.Ignore};card.AddChild(inset);inset.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                foreach(var side in new[]{"left","right","top","bottom"})inset.AddThemeConstantOverride("margin_"+side,10);
                var content=new VBoxContainer {MouseFilter=MouseFilterEnum.Ignore};content.AddThemeConstantOverride("separation",8);inset.AddChild(content);
                var center=new CenterContainer {MouseFilter=MouseFilterEnum.Ignore};content.AddChild(center);
                var disc=MockUi.Disc(entry,_mock,68);disc.MouseFilter=MouseFilterEnum.Ignore;disc.FocusMode=FocusModeEnum.None;center.AddChild(disc);
                var name=MockUi.Label(DisplayName(entry),22);name.AutowrapMode=TextServer.AutowrapMode.WordSmart;name.HorizontalAlignment=HorizontalAlignment.Center;name.MaxLinesVisible=2;name.TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis;content.AddChild(name);
                card.Pressed+=()=>EditMockLook(_mock.Look(entry),Reference(entry));grid.AddChild(card);
            }
            _lookGroups.AddChild(section);
        }
        if(entries.Length==0)_lookGroups.AddChild(MockUi.Label(Search.Text.Length>0?"No matching Looks":"No Looks in this category",22));
    }
    internal void EditMockLook(RegionLook? look = null, string? reference = null)
    {
        _editingReference = reference; _draft = look ?? new();
        _readOnly = reference is not null && !reference.StartsWith(MockData.Personal);
        _draftName = reference is null ? "" : _mock.Display(_mock.Entry(reference));
        _draftCategory = _category == AppearancePalette.Pinned ? "all" : _category;
        _pickerRole = "wall";
        _previewCircular = OS.GetCmdlineUserArgs().Contains("--region-circle");
        _previewCeiling = OS.GetCmdlineUserArgs().Contains("--preview-ceiling");
        _browserKey="";
        BuildMockEditor();
    }
    private void RemovePickerBack()
    {
        var back=GetNodeOrNull<Control>("Header/Content/Row/PickerBack");
        if(back is not null){back.GetParent().RemoveChild(back);back.QueueFree();}
    }
    private void EndMockEdit()
    {
        RemovePickerBack();
        SetProcessInput(true);
        if (_lookEditor is null) return;
        _lookEditor.GetParent().RemoveChild(_lookEditor); _lookEditor.QueueFree(); _lookEditor = null;
        GetNode<Control>(Column).Show();
        GetNode<Label>("Header/Content/Row/Titles/Title").Text = "Manage styles";
        Layout();
        if (Visible && _state is not null) { RenderCategories(); RenderStyles(); if (_mobileLayout) RenderMobile(); }
        RefreshMockCopy();
    }
    private void BuildMockEditor()
    {
        RemovePickerBack();
        if (_lookEditor is not null) { _lookEditor.GetParent().RemoveChild(_lookEditor); _lookEditor.QueueFree(); }
        _componentButtons.Clear();
        _saveLook = null;
        GetNode<Control>(Column).Hide(); Mobile.Hide();
        GetNode<Control>("Header").Show(); GetNode<Control>("Body").Show();
        GetNode<Label>("Header/Content/Row/Titles/Title").Text = _readOnly ? "Inspect Look" : _editingReference is null ? "Create Look" : "Edit Look";
        SetProcessInput(false);
        var compact = Size.Y < 540;
        _lookEditor = new VBoxContainer { Name = "LookEditor" };
        _lookEditor.AddThemeConstantOverride("separation", compact ? 8 : 20);
        GetNode<Control>("Body/Content").AddChild(_lookEditor);
        LayoutMockEditor();

        var top = new HBoxContainer { Name = "Metadata" };
        top.AddThemeConstantOverride("separation", compact ? 6 : 12);
        top.AddChild(MockUi.Button("back", "Back to Looks", EndMockEdit));
        _lookName = new LineEdit { Text = _draftName, PlaceholderText = "Name this Look", MaxLength = 128, Theme = MockUi.Controls,
            CustomMinimumSize = new(120,44), SizeFlagsHorizontal = SizeFlags.ExpandFill, Editable = !_readOnly,
            AccessibilityName = "Look name", TooltipText = "Look name" };
        _lookName.TextChanged += value => { _draftName = value; ValidateMockLook(); };
        top.AddChild(_lookName);
        var categories = new OptionButton { Theme = MockUi.Controls, CustomMinimumSize = new(compact ? 140 : 210,44),
            Disabled = _readOnly, AccessibilityName = "Look category", TooltipText = "Look category" };
        foreach (var c in _mock.Categories.Where(c => c.Id != AppearancePalette.Pinned))
        {
            categories.AddItem(c.Name); categories.SetItemMetadata(categories.ItemCount-1,c.Id);
            if (c.Id == _draftCategory) categories.Select(categories.ItemCount-1);
        }
        categories.ItemSelected += index => _draftCategory = categories.GetItemMetadata((int)index).AsString();
        top.AddChild(categories);
        if(_editingReference is not null)
        {
            var pin=MockUi.Button("save","Pin Look",()=>{});pin.Icon=AppearanceIcons.Texture("pin");pin.ToggleMode=true;
            pin.ButtonPressed=_state.Categories.Single(c=>c.Id==AppearancePalette.Pinned).Items.Contains(_editingReference);
            pin.TooltipText=pin.AccessibilityName=pin.ButtonPressed?"Unpin Look":"Pin Look";
            pin.Toggled+=value=>{_preferences.Palette.Include("region",AppearancePalette.Pinned,_editingReference!,value);pin.TooltipText=pin.AccessibilityName=value?"Unpin Look":"Pin Look";_browserKey="";};
            top.AddChild(pin);
        }
        if (_readOnly) top.AddChild(MockUi.Button("copy", "Compose a copy", () => { _editingReference = null; _readOnly = false; _draftName += " copy"; BuildMockEditor(); }));
        else
        {
            if (_editingReference is not null) top.AddChild(MockUi.Button("delete", "Delete Look", DeleteMockLook));
            _saveLook = MockUi.Button("save", "Save Look", SaveMockLook); top.AddChild(_saveLook);
        }
        _lookEditor.AddChild(top);

        var workspace = new HBoxContainer { Name = "Workspace", SizeFlagsVertical = SizeFlags.ExpandFill };
        workspace.AddThemeConstantOverride("separation", compact ? 16 : 28); _lookEditor.AddChild(workspace);
        var inspection = new VBoxContainer { Name = "Inspection", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = compact ? 1 : 1.15f };
        inspection.AddThemeConstantOverride("separation", compact ? 6 : 12); workspace.AddChild(inspection);
        _regionPreview = new MockLookPreview { Name = "RegionPreview", SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new(compact ? 300 : 340,0) };
        inspection.AddChild(_regionPreview);
        _regionPreview.Configure(_draft, _mock, _previewCircular, _previewCeiling, compact);
        _regionPreview.ShapeChanged += value => _previewCircular = value;
        _regionPreview.CeilingChanged += value => _previewCeiling = value;
        var dockScroll = new ScrollContainer { Name = "ComponentDock", HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true, CustomMinimumSize = new(0,compact ? 44 : 56) };
        inspection.AddChild(dockScroll);
        var dock = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        dock.AddThemeConstantOverride("separation", compact ? 2 : 8); dockScroll.AddChild(dock);
        var group = new ButtonGroup();
        foreach (var role in MockEditorRoles)
        {
            var button = MockUi.Button("layers", MockRoleName(role), () => SelectMockRole(role));
            button.Name = "Role_" + role;
            button.Icon = role switch {
                "wall" => AppearanceIcons.SubjectTexture("region"), "exterior_wall" => AppearanceIcons.SubjectTexture("building"),
                "columns" => AppearanceIcons.Texture("greek-temple"), "brackets" => AppearanceIcons.Texture("wood-beam"),
                _ => AppearanceIcons.SubjectTexture(role) };
            button.CustomMinimumSize = Vector2.One * (compact ? 44 : 56);
            button.ToggleMode = true; button.ButtonGroup = group;
            button.SetPressedNoSignal(role == _pickerRole);
            _componentButtons[role] = button; dock.AddChild(button);
        }
        _componentPane = new VBoxContainer { Name = "Components", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _componentPane.AddThemeConstantOverride("separation",compact ? 4 : 10); workspace.AddChild(_componentPane);
        BuildMockPicker();
        RefreshMockEditorSelection();
        ValidateMockLook();
    }
    private void SelectMockRole(string role)
    {
        if (_pickerRole == role && _componentPane is not null) return;
        _pickerRole = role;
        _regionPreview?.InspectRole(role);
        BuildMockPicker();
        RefreshMockEditorSelection();
    }
    private void RefreshMockEditorSelection()
    {
        foreach (var (role,button) in _componentButtons)
        {
            var reference = DraftReference(role);
            var choice = reference is null ? (role is "columns" or "brackets" ? "No styles available" : "Not selected") : MockData.Text(_mock.Entry(reference),"displayName");
            button.TooltipText = button.AccessibilityName = MockRoleName(role) + " · " + choice;
            button.SetPressedNoSignal(role == _pickerRole);
        }
        if (_componentChoice is not null)
        {
            var reference = DraftReference(_pickerRole);
            _componentChoice.Text = reference is null ? "" : MockData.Text(_mock.Entry(reference),"displayName");
            _componentChoice.TooltipText = _componentChoice.Text;
        }
    }
    private void LayoutMockEditor()
    {
        if (_lookEditor is null) return;
        if (Size.Y < 540)
        {
            var header = GetNode<Control>("Header"); header.Position = new(12,8); header.Size = new(Size.X-24,62);
            GetNode<Control>("Header/Content/Row/Titles/Eyebrow").Hide();
            GetNode<Label>("Header/Content/Row/Titles/Title").AddThemeFontSizeOverride("font_size",26);
            var body = GetNode<Control>("Body"); body.Position = new(12,76); body.Size = new(Size.X-24,Size.Y-86);
            var inset = GetNode<Control>("Body/Content"); inset.OffsetLeft=20; inset.OffsetRight=-20; inset.OffsetTop=8; inset.OffsetBottom=-10;
            GetNode<Control>("LeftBinding").Hide(); GetNode<Control>("RightBinding").Hide();
        }
        _lookEditor.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }
    private static string MockRoleName(string role) => role switch { "wall" => "Interior walls", "exterior_wall" => "Exterior walls", _ => char.ToUpperInvariant(role[0]) + role[1..] };
    private string? DraftReference(string role) => role switch { "wall" => _draft.Wall, "exterior_wall" => _draft.ExteriorWall, "floor" => _draft.Floor, "ceiling" => _draft.Ceiling, "foundation" => _draft.Foundation, _ => null };
    private void ValidateMockLook()
    {
        if (_saveLook is not null && IsInstanceValid(_saveLook))
        {
            _saveLook.Disabled = _draftName.Trim().Length == 0 || RegionLook.Roles.Any(r => DraftReference(r) is null);
            _saveLook.TooltipText = _saveLook.Disabled ? "Fill all five slots and name this Look" : "Save Look";
        }
    }
    private void SaveMockLook()
    {
        if (_draftName.Trim().Length == 0 || RegionLook.Roles.Any(r => DraftReference(r) is null)) return;
        _mock.Save(_draftName.Trim(),_draft,_draftCategory,_editingReference); EndMockEdit();
    }
    private void DeleteMockLook()
    {
        if (_editingReference is null) return;
        var reference = _editingReference;
        _mock.Delete(reference); EndMockEdit();
    }
    internal bool HandleMockEditorKey(InputEventKey key)
    {
        if(!IsVisibleInTree() || _lookEditor is null || !key.Pressed)return false;
        if(key.Keycode==Key.Escape)
        {
            EndMockEdit();
            return true;
        }
        if(key.Keycode!=Key.Tab)return false;
        // Same native focus pattern as the Manager, scoped to the visible editor.
        var controls=FindChildren("*","Control",true,false).OfType<Control>()
            .Where(c=>c.IsVisibleInTree() && c.FocusMode==FocusModeEnum.All
                && (c is not BaseButton button || !button.Disabled)).ToArray();
        var index=Array.IndexOf(controls,GetViewport().GuiGetFocusOwner());
        if(controls.Length>0)controls[Mathf.PosMod(index+(key.ShiftPressed?-1:1),controls.Length)].GrabFocus();
        return true;
    }
    internal void OpenMockPicker(string role)
    {
        SelectMockRole(role);
    }
    private void BuildMockPicker()
    {
        if (_componentPane is null) return;
        MockUi.Clear(_componentPane);
        var compact = Size.Y < 540;
        var heading = new VBoxContainer(); heading.AddThemeConstantOverride("separation", 0);
        _componentTitle = MockUi.Label(MockRoleName(_pickerRole),compact ? 22 : 28); heading.AddChild(_componentTitle);
        _componentChoice = MockUi.Label("",compact ? 16 : 19);
        _componentChoice.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _componentChoice.Modulate = new Color(1,1,1,.7f); heading.AddChild(_componentChoice);
        _componentPane.AddChild(heading);
        var gallery=GD.Load<PackedScene>("res://rookframe/ui/components/content/miniature_browser.tscn").Instantiate<Control>();
        gallery.Name = "ComponentGallery";
        // A local layout adapter makes the same native gallery fit a split pane.
        gallery.SetScript(GD.Load<Script>("res://prototypes/saved-looks/inline_gallery.gd"));
        gallery.Theme=GD.Load<Theme>("res://rookframe/ui/theme/silkbound_theme.tres");
        if(OS.GetCmdlineUserArgs().Contains("--long") || TranslationServer.GetLocale().StartsWith("ja",StringComparison.Ordinal))
        {
            gallery.Theme=(Theme)gallery.Theme.Duplicate();
            var font=GD.Load<Font>("res://rookframe/ui/theme/locales/ja_regular.tres");
            foreach(var type in new[]{"RookframeBody","RookframeMeta","TooltipLabel"})gallery.Theme.SetFont("font",type,font);
        }
        gallery.Set("paginated",!compact);
        gallery.Set("reduced_motion",OS.GetCmdlineUserArgs().Contains("--reduced-motion"));
        _componentPane.AddChild(gallery);
        gallery.GetNode<Label>("Status").AddThemeFontSizeOverride("font_size",compact ? 16 : 19);
        gallery.GetNode<Label>("Pager/Range").AddThemeFontSizeOverride("font_size",18);
        var entries=new Godot.Collections.Array<D>();
        var componentType = _pickerRole is "wall" or "exterior_wall" ? "wall_style" : _pickerRole is "columns" or "brackets" ? "" : "surface_finish";
        foreach(var entry in _entries.Where(e=>Text(e,"type")==componentType))
            entries.Add(new D { ["id"]=Reference(entry),["title"]=Text(entry,"displayName")+(OS.GetCmdlineUserArgs().Contains("--long")?" — северная галерея / 北の回廊":""),["package"]=Text(entry,"packageName"),["available"]=true });
        gallery.Connect("preview_requested",Callable.From<D,Control>((choice,target)=>
        {
            var entry=_mock.Entry(choice["id"].AsString());
            var card=target.GetParent().GetParent().GetParent<Button>();
            var caption=card.GetNode<Label>("Content/Copy/Title");
            caption.MaxLinesVisible=2;caption.TextOverrunBehavior=TextServer.OverrunBehavior.TrimWordEllipsis;
            caption.AddThemeFontSizeOverride("font_size",compact ? 16 : 18);
            // Every fixture component has the same source; retain it in the tooltip.
            card.GetNode<Label>("Content/Copy/Package").Hide();
            card.TooltipText=choice["title"].AsString()+"\n"+choice["package"].AsString();
            var preview=GD.Load<PackedScene>(MockUi.Ui+"TabletopContentPreview.tscn").Instantiate<TabletopContentPreview>();
            target.AddChild(preview);preview.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            if(_pickerRole is "wall" or "exterior_wall")preview.SetMockWall(entry,_mock);
            else preview.SetMockMaterialSphere(entry,_mock);
        }));
        gallery.Connect("selection_changed",Callable.From<D>(choice=>
        {
            if(_readOnly || !choice.ContainsKey("id"))return;
            var reference=choice["id"].AsString();
            _draft=_pickerRole switch { "wall"=>_draft with {Wall=reference},"exterior_wall"=>_draft with {ExteriorWall=reference},"floor"=>_draft with {Floor=reference},"ceiling"=>_draft with {Ceiling=reference},"foundation"=>_draft with {Foundation=reference},_=>_draft };
            _regionPreview!.UpdateLook(_draft);
            RefreshMockEditorSelection();
            ValidateMockLook();
        }));
        gallery.Call("configure",entries,DraftReference(_pickerRole)??"",new D {
            ["search"]="Find a "+(_pickerRole is "wall" or "exterior_wall"?"wall style":_pickerRole=="columns"?"column":_pickerRole=="brackets"?"bracket":"material")+"…",
            ["no_match"]="No matching components",["empty"]=_pickerRole is "columns" or "brackets" ? "No "+(_pickerRole=="columns"?"column":"bracket")+" styles available" : "No components available",
            ["previous"]="",["next"]="" });
        foreach(var (path,icon,label) in new[]{("Previous","chevron-left","Previous page"),("Next","chevron-right","Next page")})
        {
            var button=gallery.GetNode<Button>("Pager/"+path);button.Icon=MockUi.Icon(icon);
            button.TooltipText=button.AccessibilityName=label;button.CustomMinimumSize=new(44,44);button.ExpandIcon=true;button.AddThemeConstantOverride("icon_max_width",22);
        }
        if (_readOnly)
            foreach (var card in gallery.GetNode<GridContainer>("Results/Rows").GetChildren().OfType<Button>()) card.Disabled=true;
    }
}
