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
    private LineEdit? _lookName;
    private Button? _saveLook;
    private string _pickerRole = "";
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
        _pickerRole = "";
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
        GetNode<Control>(Column).Hide(); Mobile.Hide();
        GetNode<Control>("Header").Show(); GetNode<Control>("Body").Show();
        GetNode<Label>("Header/Content/Row/Titles/Title").Text = _readOnly ? "Inspect Look" : _editingReference is null ? "Create Look" : "Edit Look";
        _lookEditor = new VBoxContainer { Name = "LookEditor" };
        _lookEditor.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _lookEditor.AddThemeConstantOverride("separation", 12);
        GetNode<Control>("Body/Content").AddChild(_lookEditor);
        LayoutMockEditor();
        if (_pickerRole.Length > 0) { BuildMockPicker(); return; }
        var compact = Size.Y < 540;
        var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 12);
        top.AddChild(MockUi.Button("back", "Back to Looks", EndMockEdit));
        var heading = MockUi.Label(_readOnly ? "Rookframe · Application Content" : _mock.Copied == _draft && _editingReference is null ? "Copied Look" : "My Looks", compact ? 20 : 24);
        heading.SizeFlagsHorizontal = SizeFlags.ExpandFill; top.AddChild(heading);
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
        var fields = new HBoxContainer(); fields.AddThemeConstantOverride("separation", 20);
        var naming = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        if (!compact) naming.AddChild(MockUi.Label("Name", 18));
        _lookName = new LineEdit { Text = _draftName, PlaceholderText = "Name this Look", MaxLength = 128, Theme = MockUi.Controls,
            CustomMinimumSize = new(160,44), SizeFlagsHorizontal = SizeFlags.ExpandFill, Editable = !_readOnly, AccessibilityName = "Look name" };
        _lookName.TextChanged += value => { _draftName = value; ValidateMockLook(); };
        naming.AddChild(_lookName); fields.AddChild(naming);
        var grouping = new VBoxContainer { CustomMinimumSize = new(compact ? 190 : 260,0) };
        if (!compact) grouping.AddChild(MockUi.Label("Category",18));
        var categories = new OptionButton { Theme = MockUi.Controls, CustomMinimumSize = new(0,44), Disabled = _readOnly, AccessibilityName = "Look category" };
        foreach (var c in _mock.Categories.Where(c => c.Id != AppearancePalette.Pinned)) { categories.AddItem(c.Name); categories.SetItemMetadata(categories.ItemCount-1,c.Id); if (c.Id == _draftCategory) categories.Select(categories.ItemCount-1); }
        categories.ItemSelected += index => _draftCategory = categories.GetItemMetadata((int)index).AsString();
        grouping.AddChild(categories); fields.AddChild(grouping); _lookEditor.AddChild(fields);
        var content = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; content.AddThemeConstantOverride("separation",32); _lookEditor.AddChild(content);
        if (!compact)
        {
            var preview = new VBoxContainer { CustomMinimumSize = new(270,0) };
            var center = new CenterContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; center.AddChild(MockUi.LookDisc(_draft,_mock,156)); preview.AddChild(center);
            var caption = MockUi.Label(_draft.Encode().Count==4?"A complete Look":"Look preview",24); caption.HorizontalAlignment = HorizontalAlignment.Center; preview.AddChild(caption);
            var detail = MockUi.Label("Walls · floor · ceiling · foundation",18); detail.HorizontalAlignment = HorizontalAlignment.Center; preview.AddChild(detail);
            content.AddChild(preview);
        }
        var grid = new GridContainer { Columns = compact ? 2 : 1, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        grid.AddThemeConstantOverride("h_separation",24); grid.AddThemeConstantOverride("v_separation",compact ? 6 : 18); content.AddChild(grid);
        foreach (var role in RegionLook.Roles)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new(0, compact ? 62 : 84) }; row.AddThemeConstantOverride("separation",12);
            var icon = new TextureRect { Texture = AppearanceIcons.SubjectTexture(role), CustomMinimumSize = new(26,26), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, SizeFlagsVertical = SizeFlags.ShrinkCenter };
            row.AddChild(icon);
            var reference = DraftReference(role);
            if (reference is not null) { var disc = MockUi.Disc(_mock.Entry(reference),_mock,compact ? 40 : 56); disc.MouseFilter = MouseFilterEnum.Ignore; row.AddChild(disc); }
            var labels = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
            labels.AddChild(MockUi.Label(char.ToUpperInvariant(role[0]) + role[1..],17));
            var name = MockUi.Label(reference is null ? "Choose a component" : MockData.Text(_mock.Entry(reference),"displayName"), compact ? 20 : 26);
            name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; name.CustomMinimumSize = new(80,0); labels.AddChild(name); row.AddChild(labels);
            var edit = MockUi.Button("edit", "Choose " + role, () => { _pickerRole = role; BuildMockEditor(); }); edit.Disabled = _readOnly; edit.SizeFlagsVertical = SizeFlags.ShrinkCenter; row.AddChild(edit); grid.AddChild(row);
        }
        if (!compact)
        {
            var hint = MockUi.Label(_readOnly ? "Application content is read-only. Compose a copy to change its components." : "Fill all four slots and name your Look to save it.",18);
            _lookEditor.AddChild(hint);
        }
        ValidateMockLook();
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
    private string? DraftReference(string role) => role switch { "wall" => _draft.Wall, "floor" => _draft.Floor, "ceiling" => _draft.Ceiling, _ => _draft.Foundation };
    private void ValidateMockLook()
    {
        if (_saveLook is not null && IsInstanceValid(_saveLook))
        {
            _saveLook.Disabled = _draftName.Trim().Length == 0 || RegionLook.Roles.Any(r => DraftReference(r) is null);
            _saveLook.TooltipText = _saveLook.Disabled ? "Fill all four slots and name this Look" : "Save Look";
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
    internal void OpenMockPicker(string role)
    {
        _pickerRole=role;
        BuildMockEditor();
    }
    private void BuildMockPicker()
    {
        GetNode<Label>("Header/Content/Row/Titles/Title").Text="Choose "+_pickerRole;
        var back=MockUi.Button("back","Back to Look",()=>{_pickerRole="";BuildMockEditor();});
        back.Name="PickerBack";
        var header=GetNode<HBoxContainer>("Header/Content/Row");header.AddChild(back);header.MoveChild(back,0);
        var gallery=GD.Load<PackedScene>("res://rookframe/ui/components/content/miniature_browser.tscn").Instantiate<Control>();
        gallery.Theme=GD.Load<Theme>("res://rookframe/ui/theme/silkbound_theme.tres");
        gallery.Set("paginated",true);
        gallery.Set("reduced_motion",OS.GetCmdlineUserArgs().Contains("--reduced-motion"));
        _lookEditor!.AddChild(gallery);
        var entries=new Godot.Collections.Array<D>();
        foreach(var entry in _entries.Where(e=>Text(e,"type")==(_pickerRole=="wall"?"wall_style":"surface_finish")))
            entries.Add(new D { ["id"]=Reference(entry),["title"]=Text(entry,"displayName")+(OS.GetCmdlineUserArgs().Contains("--long")?" — северная галерея / 北の回廊":""),["package"]=Text(entry,"packageName"),["available"]=true });
        gallery.Connect("preview_requested",Callable.From<D,Control>((choice,target)=>
        {
            var entry=_mock.Entry(choice["id"].AsString());
            // Bound gallery captions so long names cannot consume the 3D stage.
            // The complete identity remains in the native accessible name and tooltip.
            var card=target.GetParent().GetParent().GetParent<Button>();
            var caption=card.GetNode<Label>("Content/Copy/Title");
            caption.MaxLinesVisible=2;caption.TextOverrunBehavior=TextServer.OverrunBehavior.TrimWordEllipsis;
            var source=card.GetNode<Label>("Content/Copy/Package");
            source.MaxLinesVisible=1;source.TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis;
            card.TooltipText=choice["title"].AsString()+"\n"+choice["package"].AsString();
            var preview=GD.Load<PackedScene>(MockUi.Ui+"TabletopContentPreview.tscn").Instantiate<TabletopContentPreview>();
            target.AddChild(preview);preview.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            if(_pickerRole=="wall")preview.SetMockWall(entry,_mock);
            else preview.SetMockMaterialSphere(entry,_mock);
        }));
        gallery.Connect("selection_changed",Callable.From<D>(choice=>
        {
            if(!choice.ContainsKey("id"))return;
            var reference=choice["id"].AsString();
            _draft=_pickerRole switch { "wall"=>_draft with {Wall=reference},"floor"=>_draft with {Floor=reference},"ceiling"=>_draft with {Ceiling=reference},_=>_draft with {Foundation=reference} };
            // Let the native gallery finish its release signal before replacing it.
            Callable.From(()=>{_pickerRole="";BuildMockEditor();}).CallDeferred();
        }));
        gallery.Call("configure",entries,DraftReference(_pickerRole)??"",new D {
            ["search"]="Find a "+(_pickerRole=="wall"?"wall style":"material")+"…",["no_match"]="No matching components",["empty"]="No components available",["previous"]="",["next"]="" });
        foreach(var (path,icon,label) in new[]{("Previous","chevron-left","Previous page"),("Next","chevron-right","Next page")})
        {
            var button=gallery.GetNode<Button>("Pager/"+path);button.Icon=MockUi.Icon(icon);
            button.TooltipText=button.AccessibilityName=label;button.CustomMinimumSize=new(44,44);button.ExpandIcon=true;button.AddThemeConstantOverride("icon_max_width",22);
        }
    }
}
