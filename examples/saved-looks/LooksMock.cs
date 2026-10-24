using Godot;
using Rookframe.Manager.Core;
using D=Godot.Collections.Dictionary;
namespace Rookframe.Manager.App;

public partial class LooksMock : Control
{
    private MockData _data=null!;
    private AppearanceManagement _manager=null!;
    private PaintingHud _painting=null!;
    private PaintingCopyIndicator _copy=null!;
    private RegionBrushHud _drawing=null!;
    private RegionPalette _wheel=null!;
    private ScrollContainer _left=null!,_right=null!,_builder=null!;
    private Button _saveCopy=null!,_clearCopy=null!;
    private PanelContainer _strip=null!;
    private TabBar _tabs=null!;
    private HBoxContainer _cards=null!;
    private Label _stripTitle=null!,_stripDetail=null!;
    private ScrollContainer _cardsScroll=null!;
    private MockTabletop _board=null!;
    private bool _paintingMode, _copying, _pending;
    private string _shape="Rectangle",_stripCategory="all";
    private RegionLook? _hover;
    private readonly List<RegionLook> _rooms=[];
    private readonly List<string> _roomShapes=["Rectangle","Rectangle"];
    private readonly Stack<(RegionLook[] Looks,string[] Shapes)> _undo=[],_redo=[];
    private readonly List<Button> _roomTargets=[];
    private Button _buildingTarget=null!;
    private int _target;
    private string _subject="region";
    private bool _fixtureLong, _renderingTabs;
    internal MockData Data=>_data;

    public override void _Ready()
    {
        _data=new(); _rooms.Add(_data.BuiltIn("Tavern") with { Ceiling=MockData.Source("dark-stone-tiles"), ExteriorWall=MockData.Source("rough-stone-wall") }); _rooms.Add(_data.BuiltIn("Castle"));
        _board=new MockTabletop { Name="TabletopFixture", MouseFilter=MouseFilterEnum.Stop, OwnerMock=this };
        AddChild(_board); _board.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        // Detach the actual authored rails before their host owner enters the tree.
        var shell=GD.Load<PackedScene>(MockUi.Ui+"TabletopWorkspace.tscn").Instantiate<Control>();
        ScrollContainer rail(string name)
        {
            var r=shell.GetNode<ScrollContainer>("WorkspaceSafeArea/RailsLayer/"+name); r.GetParent().RemoveChild(r); r.Owner=null; AddChild(r); r.Show();
            foreach(var b in r.FindChildren("*","Button",true,false).OfType<Button>()) b.Disabled=true;
            return r;
        }
        _left=rail("LeftRail"); _right=rail("RightRail"); _builder=rail("BuilderToolRail"); shell.Free();
        void bind(ScrollContainer rail,string node,Action action)
        {
            var b=rail.GetNode<Button>("RailPanel/Tools/"+node); b.Disabled=false; b.Pressed+=action;
        }
        bind(_builder,"RoomsEntry",()=>Mode(false)); bind(_builder,"PaletteEntry",()=>Mode(true));
        bind(_right,"BuilderEntry",()=>Mode(!_paintingMode));
        _right.GetNode<Button>("RailPanel/Tools/BuilderEntry").ButtonPressed=true;
        _drawing=GD.Load<PackedScene>(MockUi.Ui+"RegionBrushHud.tscn").Instantiate<RegionBrushHud>(); AddChild(_drawing); _drawing.SetEntries(_data.Entries);
        _drawing.GetNode<Control>("Looks").Hide(); _drawing.GetNode<Control>("Editing").Show();
        _drawing.GetNode<Control>("Editing/Sections/Visibility").Hide(); _drawing.GetNode<Control>("Editing/Sections/Divider").Hide();
        _drawing.DrawRequested+=()=>BeginFootprint(_drawing.Shape);
        _drawing.HistoryRequested+=History; _drawing.DeleteRequested+=DeleteRoom;
        _painting=GD.Load<PackedScene>(MockUi.Ui+"PaintingHud.tscn").Instantiate<PaintingHud>(); AddChild(_painting);
        _painting.PaintRequested+=()=>{_copying=false;Present();}; _painting.CopyRequested+=()=>{_copying=true;Present();};
        _painting.ManagementRequested+=()=>OpenManager(); _painting.HistoryRequested+=History;
        _copy=GD.Load<PackedScene>(MockUi.Ui+"PaintingCopyIndicator.tscn").Instantiate<PaintingCopyIndicator>(); AddChild(_copy); _copy.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _copy.MouseFilter=MouseFilterEnum.Ignore;
        _saveCopy=MockUi.Button("save","Save copied Look",()=>OpenManager(true)); AddChild(_saveCopy);
        _clearCopy=MockUi.Button("close","Clear copied appearance",ClearCopy); AddChild(_clearCopy);
        _wheel=GD.Load<PackedScene>(MockUi.Ui+"RegionPalette.tscn").Instantiate<RegionPalette>(); AddChild(_wheel); _wheel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); _wheel.Hide();
        _wheel.MouseFilter=MouseFilterEnum.Ignore;
        BuildStrip();
        _manager=GD.Load<PackedScene>(MockUi.Ui+"AppearanceManagement.tscn").Instantiate<AppearanceManagement>(); AddChild(_manager); _manager.ConfigureMock(_data);
        _manager.Closed+=()=>{_manager.RefreshMockCopy();Present();};
        for(var i=0;i<2;i++)
        {
            var index=i; var b=MockUi.Button("layers","Room · "+(i==0?"Tavern":"Castle"),()=>Target(_rooms.Count>2?_rooms.Count-1:index,"region"));
            b.Icon=AppearanceIcons.SubjectTexture("region"); AddChild(b); _roomTargets.Add(b);
        }
        _buildingTarget=MockUi.Button("world","Building",()=>Target(0,"building"));
        _buildingTarget.Icon=AppearanceIcons.SubjectTexture("building"); AddChild(_buildingTarget);
        _data.Changed+=()=>{if(_pending)RenderStrip();_manager.RefreshMockCopy();Present();};
        Resized+=Layout; Mode(false); Callable.From(Layout).CallDeferred();
        var args=OS.GetCmdlineUserArgs();
        if(args.Contains("--long")) _fixtureLong=true;
        if(args.Contains("--saved-copy")){_data.Copied=_rooms[0];_data.Save("Copper Kettle evening",_rooms[0],_data.Categories.First(c=>c.Name=="Timber").Id);_manager.RefreshMockCopy();}
        if(args.Contains("--manager")) Callable.From(()=>OpenManager()).CallDeferred();
        if(args.Contains("--copy")) Callable.From(()=>{Mode(true);_copying=true;Target(0,"region");}).CallDeferred();
        if(args.Contains("--strip")) Callable.From(()=>BeginFootprint("Rectangle")).CallDeferred();
        if(args.Contains("--compose")) Callable.From(()=>{OpenManager();_manager.EditMockLook();}).CallDeferred();
        if(args.Contains("--edit")) Callable.From(()=>{OpenManager();var e=_data.AllLooks.First(e=>MockData.Text(e,"packageId")==MockData.Personal);_manager.EditMockLook(_data.Look(e),MockData.Ref(e));}).CallDeferred();
        var picker=args.FirstOrDefault(a=>a.StartsWith("--picker-",StringComparison.Ordinal));
        if(picker is not null && RegionLook.Roles.Contains(picker[9..]))
            Callable.From(()=>{OpenManager();_manager.EditMockLook(_rooms[0]);_manager.OpenMockPicker(picker[9..]);}).CallDeferred();
        if(args.Contains("--capture")) CaptureLater(args);
    }
    private void Mode(bool paint)
    {
        _paintingMode=paint; _pending=false; _hover=null; _wheel?.Close(); Present();
    }
    private void Present()
    {
        if(_painting is null)return;
        _drawing.Visible=!_paintingMode&&!_pending; _painting.Visible=_paintingMode&&!_pending;
        _painting.Present(_copying,_data.Copied is null&&_data.CopiedAtomic is null);
        _painting.ShowContext(true,false,false,false);
        _strip.Visible=_pending;
        _copy.Present(_paintingMode,_copying,_data.Copied is null&&_data.CopiedAtomic is null);
        _saveCopy.Visible=_paintingMode&&_data.Copied is not null&&!_data.CopiedSaved;
        _clearCopy.Visible=_paintingMode&&(_data.Copied is not null||_data.CopiedAtomic is not null);
        _builder.GetNode<Button>("RailPanel/Tools/RoomsEntry").SetPressedNoSignal(!_paintingMode);
        _builder.GetNode<Button>("RailPanel/Tools/PaletteEntry").SetPressedNoSignal(_paintingMode);
        foreach(var b in _roomTargets)b.Visible=!_pending;
        _buildingTarget.Visible=!_pending;
        _drawing.GetNode<Button>("Editing/Sections/History/Undo").Disabled=_undo.Count==0;
        _drawing.GetNode<Button>("Editing/Sections/History/Redo").Disabled=_redo.Count==0;
        _drawing.GetNode<Button>("Editing/Sections/History/Delete").Disabled=_rooms.Count==0;
        _painting.GetNode<Button>("Sections/History/Undo").Disabled=_undo.Count==0;
        _painting.GetNode<Button>("Sections/History/Redo").Disabled=_redo.Count==0;
        Layout(); _board.QueueRedraw();
    }
    internal Rect2 RoomRect(int index)
    {
        if (_rooms.Count>2) return PendingRect();
        var compact=Size.Y<540;
        var w=Math.Min(340,(Size.X-260)/2.6f); var h=Math.Min(270,Size.Y*(compact?.34f:.31f));
        return new(new Vector2(Size.X*.43f+(index==0?-w-18:18),Size.Y*(compact?.35f:.38f)-h/2),new(w,h));
    }
    internal Rect2 PendingRect()=>new(new Vector2(Size.X*.43f-Math.Min(500,Size.X*.48f)/2,Size.Y*.32f-Math.Min(280,Size.Y*(Size.Y<540?.23f:.31f))/2),new(Math.Min(500,Size.X*.48f),Math.Min(280,Size.Y*(Size.Y<540?.23f:.31f))));
    internal string Shape=>_shape;
    internal string CommittedShape=>_roomShapes.LastOrDefault()??"Rectangle";
    internal bool Pending=>_pending;
    internal RegionLook? Hover=>_hover;
    internal RegionLook DisplayRoom(int i)=>_hover is not null && (_subject=="building" || i==_target) ? _hover : _rooms[i];
    internal IReadOnlyList<RegionLook> Rooms=>_rooms;
    internal void CanvasClick(Vector2 point)
    {
        if(_pending)return;
        if(_rooms.Count>2 && PendingRect().HasPoint(point)){ Target(_rooms.Count-1,"floor");return; }
        for(var i=0;i<Math.Min(2,_rooms.Count);i++) if(RoomRect(i).HasPoint(point)){ Target(i,"floor");return; }
    }
    private void Target(int index,string subject)
    {
        if(!_paintingMode||index>=_rooms.Count)return;
        _target=index; _subject=subject;
        if(_copying)
        {
            if(_data.Copied is null&&_data.CopiedAtomic is null)
            {
                if(subject is "region" or "building")
                {
                    _data.Copied=subject=="building"?RegionLook.Pickup(_rooms.Select(Plan),r=>MockData.Text(_data.Entry(r),"displayName")):RegionLook.Pickup([Plan(_rooms[index])],r=>MockData.Text(_data.Entry(r),"displayName"));
                    _data.Copied = _data.Copied with { Foundation = null };
                    _data.CopiedSubject=subject;
                    _copy.SetBuffer(new(Look:_data.Copied),subject,_data.Content);
                }
                else {_data.CopiedAtomic=_rooms[index].Floor;_data.CopiedSubject=subject;_copy.SetBuffer(new(Surface:subject,Reference:_data.CopiedAtomic!),subject,_data.Content);}
                _manager.RefreshMockCopy();Present();return;
            }
            Remember();
            foreach(var targetIndex in subject=="building" ? Enumerable.Range(0,_rooms.Count) : new[]{index})
            {
                if(_data.Copied is not null) _rooms[targetIndex]=RegionLook.Pickup(_data.Copied.Apply(Plan(_rooms[targetIndex])));
                else _rooms[targetIndex]=_rooms[targetIndex] with {Floor=_data.CopiedAtomic};
            }
            Present();return;
        }
        _wheel.Open(new("fixture-"+index,subject,Building:subject=="building"),RoomRect(index).GetCenter(),Plan(_rooms[index]),_data.Entries,_data.Content,
            paint=>{_hover=paint?.Look??(paint is null?null:_rooms[index] with {Floor=paint.Reference});_board.QueueRedraw();},
            paint=>{Remember();foreach(var targetIndex in subject=="building" ? Enumerable.Range(0,_rooms.Count) : new[]{index}){var after=paint.Apply(Plan(_rooms[targetIndex]));_rooms[targetIndex]=RegionLook.Pickup(after);}_hover=null;Present();},_data.Preferences);
    }
    private static RoomDraft Plan(RegionLook l)=>l.Apply(new RoomDraft(Transform3D.Identity,new(0,0,5,4),l.Wall!,l.Floor!,l.Ceiling!,.2,0,2.8,Foundation:l.Foundation!));
    private void ClearCopy(){_data.Copied=null;_data.CopiedAtomic=null;_copy.ClearBuffer();_manager.RefreshMockCopy();Present();}
    internal void OpenManager(bool copied=false)
    {
        _wheel.Close();_hover=null;
        _manager.Open(_data.Preferences,_data.Entries,_data.Content,"region");_manager.RefreshMockCopy();
        if(copied&&_data.Copied is not null)_manager.EditMockLook(_data.Copied);
    }
    internal void BeginFootprint(string shape)
    {
        _shape=shape;_pending=true;_hover=null;_stripCategory="all";RenderStrip();Present();
    }
    private void BuildStrip()
    {
        _strip=new PanelContainer { Name="LookStrip",Theme=GD.Load<Theme>(MockUi.Ui+"RegionBrushHudTheme.tres"),ThemeTypeVariation="settings_surface" };
        AddChild(_strip);
        var column=new VBoxContainer { Name="Column",Theme=MockUi.Controls };column.AddThemeConstantOverride("separation",8);var inset=new MarginContainer {Name="Inset"};foreach(var side in new[]{"left","right","top","bottom"})inset.AddThemeConstantOverride("margin_"+side,12);_strip.AddChild(inset);inset.AddChild(column);
        var top=new HBoxContainer();top.AddThemeConstantOverride("separation",12);column.AddChild(top);
        _tabs=new TabBar { SizeFlagsHorizontal=SizeFlags.ExpandFill, CustomMinimumSize=new(0,44), ScrollingEnabled=true, Theme=GD.Load<Theme>(MockUi.Ui+"manager/menu/silkbound/SilkboundTheme.tres") };
        _tabs.AddThemeFontSizeOverride("font_size",20);
        _tabs.TabChanged+=index=>{if(_renderingTabs||index<0||index>=_tabs.TabCount)return;_stripCategory=_tabs.GetTabMetadata((int)index).AsString();RenderCards();_board.QueueRedraw();};
        top.AddChild(_tabs);
        top.AddChild(MockUi.Button("close","Cancel new room",()=>{_pending=false;_hover=null;Present();}));
        _cardsScroll=new ScrollContainer { VerticalScrollMode=ScrollContainer.ScrollMode.Disabled,HorizontalScrollMode=ScrollContainer.ScrollMode.Auto,SizeFlagsHorizontal=SizeFlags.ExpandFill };
        _cards=new HBoxContainer();_cards.AddThemeConstantOverride("separation",16);_cardsScroll.AddChild(_cards);column.AddChild(_cardsScroll);
        var bottom=new HBoxContainer();bottom.AddThemeConstantOverride("separation",16);column.AddChild(bottom);
        _stripTitle=MockUi.Label("Choose a Look",22);bottom.AddChild(_stripTitle);
        _stripDetail=MockUi.Label("Hover to preview · click to create",18);_stripDetail.SizeFlagsHorizontal=SizeFlags.ExpandFill;_stripDetail.HorizontalAlignment=HorizontalAlignment.Right;bottom.AddChild(_stripDetail);
        _strip.Hide();
    }
    private void RenderStrip()
    {
        if(!_data.Categories.Any(c=>c.Id==_stripCategory))
            _stripCategory=_data.Categories.First().Id;
        _renderingTabs=true;
        _tabs.ClearTabs();
        foreach(var category in _data.Categories)
        {
            _tabs.AddTab(category.Name,AppearanceIcons.Texture(category.Icon));var index=_tabs.TabCount-1;
            _tabs.SetTabMetadata(index,category.Id);_tabs.SetTabIconMaxWidth(index,20);
            if(category.Id==_stripCategory)_tabs.CurrentTab=index;
        }
        _renderingTabs=false;
        RenderCards();
    }
    private void RenderCards()
    {
        MockUi.Clear(_cards);_hover=null;
        var selected=_data.Categories.First(c=>c.Id==_stripCategory);
        foreach(var entry in _data.AllLooks.Where(e=>selected.Items.Contains(MockData.Ref(e))))
        {
            var card=new VBoxContainer { CustomMinimumSize=new(Size.Y<540?94:126,0) };card.AddThemeConstantOverride("separation",0);
            var center=new CenterContainer();var disc=MockUi.Disc(entry,_data,Size.Y<540?50:72);center.AddChild(disc);card.AddChild(center);
            var title=MockUi.Label(_fixtureLong?"The old northern garrison’s winter hall":_data.Display(entry),Size.Y<540?17:21);
            title.AutowrapMode=TextServer.AutowrapMode.WordSmart;title.HorizontalAlignment=HorizontalAlignment.Center;title.CustomMinimumSize=new(Size.Y<540?94:126,0);title.MaxLinesVisible=Size.Y<540?1:2;title.TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis;card.AddChild(title);
            void preview(){_hover=_data.Look(entry);_stripTitle.Text=_data.Display(entry);_stripDetail.Text="Click to create room";_board.QueueRedraw();}
            void leave(){if(disc.HasFocus())return;_hover=null;_stripTitle.Text="Choose a Look";_stripDetail.Text="Hover to preview · click to create";_board.QueueRedraw();}
            disc.MouseEntered+=preview;disc.FocusEntered+=preview;disc.MouseExited+=leave;disc.FocusExited+=leave;
            disc.Pressed+=()=>{Remember();_rooms.Add(_data.Look(entry));_roomShapes.Add(_shape);_pending=false;_hover=null;Present();};_cards.AddChild(card);
        }
        if(_cards.GetChildCount()==0){var empty=MockUi.Label("No Looks in this category",20);empty.CustomMinimumSize=new(200,76);_cards.AddChild(empty);}
        _stripTitle.Text="Choose a Look";_stripDetail.Text="Hover to preview · click to create";
        Callable.From(Layout).CallDeferred();
    }
    private void Remember(){_undo.Push((_rooms.ToArray(),_roomShapes.ToArray()));_redo.Clear();}
    private void History(bool redo)
    {
        var from=redo?_redo:_undo;var to=redo?_undo:_redo;if(from.Count==0)return;to.Push((_rooms.ToArray(),_roomShapes.ToArray()));var state=from.Pop();_rooms.Clear();_rooms.AddRange(state.Looks);_roomShapes.Clear();_roomShapes.AddRange(state.Shapes);_hover=null;Present();
    }
    private void DeleteRoom(){if(_rooms.Count==0)return;Remember();_rooms.RemoveAt(_rooms.Count-1);_roomShapes.RemoveAt(_roomShapes.Count-1);Present();}
    private void Layout()
    {
        if(_manager is null)return;
        var compact=Size.Y<540;var tablet=Size.X<1200;
        foreach(var rail in new[]{_left,_right,_builder})rail.Call("set_compact",compact||tablet);
        var edge=_left.GetThemeConstant("inset");var top=_left.GetThemeConstant("top");var separation=_left.GetThemeConstant("separation");
        foreach(var rail in new[]{_left,_right,_builder})
        {
            var natural=rail.GetNode<Control>("RailPanel").GetCombinedMinimumSize();rail.Size=new(natural.X,Math.Min(natural.Y,Size.Y-top-edge));
        }
        _left.Position=new(edge,top);_right.Position=new(Size.X-edge-_right.Size.X,top);_builder.Position=new(_right.Position.X-_builder.Size.X-separation,top);
        var height=compact?68:104;_drawing.SetEditingHeight(height);_painting.LayoutFor(height);
        foreach(var hud in new Control[]{_drawing,_painting})
        {
            hud.Size=hud.GetCombinedMinimumSize();var scale=Math.Min(1,(Size.X-24)/Math.Max(1,hud.Size.X));hud.Scale=Vector2.One*scale;hud.Position=new((Size.X-hud.Size.X*scale)/2,Size.Y-(compact?10:82)-hud.Size.Y*scale);
        }
        var buffer=_copy.GetNode<Control>("Buffer");_saveCopy.Position=buffer.Position+new Vector2(-36,-8);_clearCopy.Position=buffer.Position+new Vector2(-42,38);
        for(var i=0;i<_roomTargets.Count;i++){var rect=RoomRect(i);_roomTargets[i].Position=new(rect.GetCenter().X-22,rect.Position.Y-58);_roomTargets[i].Visible=!_pending&&i<(_rooms.Count>2?1:_rooms.Count);}
        _buildingTarget.Position=new(Size.X*.43f-(_rooms.Count>2?80:22),Math.Max(10,RoomRect(0).Position.Y-110));
        var stripFrame=(StyleBoxTexture)_strip.Theme.GetStylebox("panel","settings_surface").Duplicate();
        if(compact)foreach(var side in new[]{Side.Left,Side.Top,Side.Right,Side.Bottom}){stripFrame.SetTextureMargin(side,stripFrame.GetTextureMargin(side)*.65f);stripFrame.SetContentMargin(side,Mathf.Ceil(stripFrame.GetTextureMargin(side)));}
        _strip.AddThemeStyleboxOverride("panel",stripFrame);
        var stripInset=_strip.GetNode<MarginContainer>("Inset");
        foreach(var side in new[]{"left","right","top","bottom"})stripInset.AddThemeConstantOverride("margin_"+side,compact?4:12);
        _strip.GetNode<VBoxContainer>("Inset/Column").AddThemeConstantOverride("separation",compact?4:8);
        _stripDetail.Visible=!compact;
        _strip.Size=new(Math.Min(Size.X-(compact?20:240),1120),0);_strip.Size=new(_strip.Size.X,_strip.GetCombinedMinimumSize().Y);
        _strip.Position=new((Size.X-_strip.Size.X)/2,Size.Y-(compact?8:40)-_strip.Size.Y);
        _board.QueueRedraw();
    }
    public override void _Input(InputEvent e)
    {
        if(e is InputEventKey key && _manager is not null && _manager.HandleMockEditorKey(key))
            GetViewport().SetInputAsHandled();
    }
    public override void _UnhandledKeyInput(InputEvent e)
    {
        if(e is not InputEventKey {Pressed:true} key)return;
        if(key.Keycode==Key.Escape&&_pending){_pending=false;_hover=null;Present();GetViewport().SetInputAsHandled();}
        if(key.Keycode==Key.F9)Capture("manual");
        if(key.Keycode==Key.F8){_fixtureLong=!_fixtureLong;if(_pending)RenderStrip();}
    }
    private async void CaptureLater(string[] args)
    {
        await ToSignal(GetTree().CreateTimer(2),Godot.Timer.SignalName.Timeout);
        var idx=Array.IndexOf(args,"--capture");Capture(idx+1<args.Length?args[idx+1]:"capture");
        if(args.Contains("--quit-after-capture"))GetTree().Quit();
    }
    public override void _ExitTree() => Input.SetCustomMouseCursor(null);
    private void Capture(string name)
    {
        var path=OS.GetEnvironment("LOOKS_EVIDENCE");if(path.Length==0)path="/tmp/rfg404-looks";System.IO.Directory.CreateDirectory(path);
        GetViewport().GetTexture().GetImage().SavePng(path+"/"+name+".png");
        System.IO.File.WriteAllText(path+"/"+name+".json",System.Text.Json.JsonSerializer.Serialize(new{size=Size.ToString(),pending=_pending,looks=_data.AllLooks.Count(),rooms=_rooms.Count,copied=_data.Copied is not null,manager=_manager.Visible}));
        GD.Print("MOCK_CAPTURE "+path+"/"+name+".png");
    }
}

// Flat test surface, deliberately not a recreation of the live 3D tabletop.
public partial class MockTabletop : Control
{
    internal LooksMock OwnerMock=null!;
    private readonly Dictionary<string,RoomAppearance> _materials=[];
    public override void _GuiInput(InputEvent e){if(e is InputEventMouseButton{Pressed:true,ButtonIndex:MouseButton.Left}m)OwnerMock.CanvasClick(m.Position);}
    public override void _Draw()
    {
        DrawRect(new(Vector2.Zero,Size),new Color("202a28"));
        for(var x=0f;x<Size.X;x+=48)DrawLine(new(x,0),new(x,Size.Y),new Color(.7f,.68f,.55f,.035f));
        for(var y=0f;y<Size.Y;y+=48)DrawLine(new(0,y),new(Size.X,y),new Color(.7f,.68f,.55f,.035f));
        if(OwnerMock.Pending){DrawRoom(OwnerMock.PendingRect(),OwnerMock.Hover,OwnerMock.Shape,true);return;}
        if(OwnerMock.Rooms.Count>2)DrawRoom(OwnerMock.PendingRect(),OwnerMock.DisplayRoom(OwnerMock.Rooms.Count-1),OwnerMock.CommittedShape,false);
        else for(var i=0;i<OwnerMock.Rooms.Count;i++)DrawRoom(OwnerMock.RoomRect(i),OwnerMock.DisplayRoom(i),"Rectangle",false);
    }
    private void DrawRoom(Rect2 rect,RegionLook? look,string shape,bool pending)
    {
        Vector2[] points=shape is "Circle" or "Ellipse"?Enumerable.Range(0,64).Select(i=>rect.GetCenter()+Vector2.FromAngle(i*Mathf.Tau/64)*(shape=="Circle"?Vector2.One*Math.Min(rect.Size.X,rect.Size.Y):rect.Size)/2).ToArray()
            :shape=="Polygon"?[rect.Position,new(rect.End.X-rect.Size.X*.23f,rect.Position.Y),new(rect.End.X,rect.Position.Y+rect.Size.Y*.35f),rect.End,new(rect.Position.X+rect.Size.X*.2f,rect.End.Y),new(rect.Position.X,rect.End.Y-rect.Size.Y*.25f)]
            :[rect.Position,new(rect.End.X,rect.Position.Y),rect.End,new(rect.Position.X,rect.End.Y)];
        var fill=new Color("ab9570");Texture2D? texture=null;
        if(look?.Floor is {} reference)
        {
            if(!_materials.TryGetValue(reference,out var material))_materials[reference]=material=RoomAppearance.Load(reference,"surface_finish",OwnerMock.Data.Content);
            texture=material.Material.AlbedoTexture;fill=material.Material.AlbedoColor;
        }
        if(look is null)
        {
            DrawColoredPolygon(points,new Color(.73f,.65f,.48f,.16f));
            var hatch=GD.Load<Texture2D>(MockUi.Ui+"assets/cavern-hatch.svg");
            TextureRepeat=TextureRepeatEnum.Enabled;DrawPolygon(points,new Color[]{new(.85f,.75f,.56f,.5f)},points.Select(p=>p/48).ToArray(),hatch);
        }
        else{TextureRepeat=TextureRepeatEnum.Enabled;DrawPolygon(points,new Color[]{fill},points.Select(p=>p/96).ToArray(),texture);}
        var closed=points.Append(points[0]).ToArray();
        DrawPolyline(closed,new Color(0,0,0,.40f),pending&&look is null?12:19,true);
        DrawPolyline(closed,look is null?new Color("d6bb86"):new Color("74664e"),look is null?4:12,true);
        if(look is null)foreach(var p in points)DrawCircle(p,5,new Color("ddc79c"));
        else DrawPolyline(closed,new Color("b4a181"),2,true);
    }
}
