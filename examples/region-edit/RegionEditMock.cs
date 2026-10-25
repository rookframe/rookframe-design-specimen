using Godot;
using Rookframe.Manager.Core;
using D = Godot.Collections.Dictionary;
namespace Rookframe.Manager.App;

// Standalone native specimen: production resources, temporary architecture and local history.
public partial class RegionEditMock : Control
{
    private const string Ui="res://rookframe/application/ui/";
    private readonly Godot.Collections.Array _entries=WorldApplicationRoot.ContentLibraryEntries(RookframeApplicationContent.CreateContent().Where(e=>e.Type is "wall_style" or "surface_finish" or "architectural_look").ToArray());
    private readonly LocalAppearancePalette _preferences=new();
    private ScrollContainer _left=null!,_right=null!,_rail=null!;
    private RegionBrushHud _rootHud=null!;
    private PaintingHud _paint=null!;
    private RegionLookPicker _looks=null!;
    private PanelContainer _editHud=null!;
    private Control _switcher=null!;
    private Label _caption=null!,_notice=null!;
    private Button _undoButton=null!,_redoButton=null!,_delete=null!,_commit=null!;
    private SubViewport _view=null!;
    private Camera3D _camera=null!;
    private Node3D _world=null!,_architecture=null!,_handles=null!;
    private RegionBrushOverlay _overlay=null!;
    private Node2D _foundation=null!;
    private MockOverlay _feedback=null!;
    private readonly List<Room> _rooms=[];
    private List<Room>? _before;
    private readonly Stack<List<Room>> _undo=[],_redo=[],_worldUndo=[],_worldRedo=[];
    private readonly List<(Vector2 Point,string Kind)> _hits=[];
    private string _tool="",_drag="",_scenario="room";
    private int _selected=1,_anchor=-1;
    private bool _longNames;
    private bool _building,_builder=true,_invalid,_roofHidden=true,_cutaway=true,_newRoom;
    private Vector2 _startPixel,_startPlane;
    private List<Room>? _dragStart;
    private float _startAngle;
    private Vector2? _wallPoint;
    private int _wallEdge;
    private record Room(string Name,Vector2 At,Vector2[] Ring,float Floor=0,float Height=2.8f,float Angle=0,bool Stacked=false,string Look="Tavern",float Base=.22f,string Shape="Rectangle",RegionLook? Appearance=null);
    private Room Current=>_rooms[Math.Clamp(_selected,0,_rooms.Count-1)];
    private bool Pending=>_before is not null;
    private D? Content(string package,string local)=>_entries.Select(v=>v.AsGodotDictionary()).FirstOrDefault(e=>LocalAppearancePalette.Text(e,"packageId")==package&&LocalAppearancePalette.Text(e,"localId")==local);
    private static Vector2[] Rectangle(float x,float y)=>[new(-x/2,-y/2),new(x/2,-y/2),new(x/2,y/2),new(-x/2,y/2)];
    private static Vector2[] DrawRing(string shape,float x,float y)
    {
        if(shape=="Rectangle")return Rectangle(x,y);
        if(shape=="Circle")x=y=Math.Max(x,y);
        return Enumerable.Range(0,40).Select(i=>new Vector2(Mathf.Cos(i*Mathf.Tau/40)*x/2,Mathf.Sin(i*Mathf.Tau/40)*y/2)).ToArray();
    }
    private RegionLook Look(string name)=>RegionLook.Load(RookframeApplicationContent.SourceId+":"+name.ToLowerInvariant()+"-look",Content);
    private Vector2[] Polygon(Room r)=>r.Ring.Select(p=>p.Rotated(r.Angle)+r.At).ToArray();
    private Vector2 Project(Vector3 p)=>_camera.UnprojectPosition(p);
    private Vector3 V(Vector2 p,float y)=>new(p.X,y,p.Y);
    private Vector2 Plane(Vector2 pixel,float height=0)
    {
        var o=_camera.ProjectRayOrigin(pixel);var d=_camera.ProjectRayNormal(pixel);
        var p=o+d*((height-o.Y)/d.Y);return new(p.X,p.Z);
    }
    public override void _Ready()
    {
        BuildStage(); BuildUi();
        Resized+=Arrange;
        var args=OS.GetCmdlineUserArgs();
        _longNames=args.Contains("--long");
        var state=Array.IndexOf(args,"--state");Reset(state>=0?args[state+1]:"room");
        Callable.From(Arrange).CallDeferred();
        if(args.Contains("--capture"))CaptureLater(args);
    }
    private void BuildStage()
    {
        var preview=GD.Load<PackedScene>(Ui+"TabletopContentPreview.tscn").Instantiate<Control>();
        // Keep the authored studio, viewport, camera and lights, without starting its content browser.
        var viewportControl=preview.GetNode<SubViewportContainer>("PreviewViewportContainer");preview.RemoveChild(viewportControl);preview.Free();AddChild(viewportControl);viewportControl.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _view=viewportControl.GetNode<SubViewport>("PreviewViewport");_view.RenderTargetUpdateMode=SubViewport.UpdateMode.Always;
        _world=_view.GetNode<Node3D>("PreviewWorld");_world.GetNode<Node3D>("FallbackPresentation").Hide();
        _camera=_world.GetNode<Camera3D>("PreviewCamera");_camera.Projection=Camera3D.ProjectionType.Orthogonal;_camera.Position=new(11,16,20);_camera.LookAt(new(-.2f,1,0));
        _architecture=new Node3D();_handles=new Node3D();_world.AddChild(_architecture);_world.AddChild(_handles);
        _world.AddChild(new MeshInstance3D{Mesh=new PlaneMesh{Size=new(90,90)},Position=new(0,-.22f,0),MaterialOverride=new StandardMaterial3D{AlbedoColor=new("343e3b"),Roughness=1}});
        _overlay=new RegionBrushOverlay{MouseFilter=MouseFilterEnum.Ignore};AddChild(_overlay);_overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _feedback=new MockOverlay{OwnerMock=this,MouseFilter=MouseFilterEnum.Ignore};AddChild(_feedback);_feedback.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _foundation=GD.Load<PackedScene>(Ui+"FoundationHandle.tscn").Instantiate<Node2D>();AddChild(_foundation);
    }
    private void BuildUi()
    {
        var shell=GD.Load<PackedScene>(Ui+"TabletopWorkspace.tscn").Instantiate<Control>();
        ScrollContainer rail(string name)
        {
            var r=shell.GetNode<ScrollContainer>("WorkspaceSafeArea/RailsLayer/"+name);r.GetParent().RemoveChild(r);r.Owner=null;AddChild(r);r.Show();
            foreach(var b in r.FindChildren("*","Button",true,false).OfType<Button>())b.Disabled=true;
            return r;
        }
        _left=rail("LeftRail");_right=rail("RightRail");_rail=rail("BuilderToolRail");shell.Free();
        _left.GetNode<Control>("RailPanel/Tools/TargetingEntry").Show();
        var packageSlot=_left.GetNode<Control>("RailPanel/Tools/PackageSlot");packageSlot.Show();
        var combat=GD.Load<PackedScene>("res://rookframe/ui/editor/EditorToolButton.tscn").Instantiate<Button>();combat.Icon=GD.Load<Texture2D>("res://rookframe/ui/icons/swords.svg");combat.TooltipText=combat.AccessibilityName="Combat · outside this mock";combat.Disabled=true;packageSlot.AddChild(combat);
        Bind(_rail,"PortalsEntry",()=>Tool("draw"));Bind(_rail,"PaletteEntry",()=>Tool("paint"));
        Bind(_right,"BuilderEntry",()=>{Cancel();_builder=!_builder;_tool="";Present();});
        _rootHud=GD.Load<PackedScene>(Ui+"RegionBrushHud.tscn").Instantiate<RegionBrushHud>();AddChild(_rootHud);
        _rootHud.ViewOptionsRequested+=(roof,cut)=>{_roofHidden=roof;_cutaway=cut;Rebuild();};
        _rootHud.DrawRequested+=()=>{if(Pending)Cancel();};
        var polygonButton=_rootHud.GetNode<Button>("Sections/Shapes/Row/Polygon");polygonButton.Disabled=true;polygonButton.TooltipText="Polygon drawing is unchanged; this mock exercises Rectangle, Circle and Ellipse";_rootHud.HistoryRequested+=History;_rootHud.DeleteRequested+=Delete;
        _paint=GD.Load<PackedScene>(Ui+"PaintingHud.tscn").Instantiate<PaintingHud>();AddChild(_paint);
        foreach(var b in _paint.FindChildren("*","Button",true,false).OfType<Button>())b.Disabled=true;
        _paint.TooltipText="Painting appearance controls are unchanged; click its rail icon again to return to Builder";
        _editHud=new PanelContainer{Theme=GD.Load<Theme>(Ui+"RegionBrushHudTheme.tres"),ThemeTypeVariation="settings_surface"};AddChild(_editHud);
        var row=new HBoxContainer();row.AddThemeConstantOverride("separation",0);_editHud.AddChild(row);
        _undoButton=HudButton("Undo","Undo pending change",()=>History(false),true);row.AddChild(_undoButton);
        _redoButton=HudButton("Redo","Redo pending change",()=>History(true),true);row.AddChild(_redoButton);
        _delete=HudButton("delete","Delete room · Cancel brings it back",Delete);row.AddChild(_delete);
        row.AddChild(new VSeparator());
        var cancel=HudButton("close","Cancel edit",Cancel);row.AddChild(cancel);
        _commit=HudButton("check","Commit edit",Commit);row.AddChild(_commit);
        _switcher=GD.Load<PackedScene>(Ui+"navigation/HudSwitcher.tscn").Instantiate<Control>();AddChild(_switcher);_switcher.Show();
        foreach(var b in _switcher.FindChildren("*","Button",true,false).OfType<Button>())b.Disabled=true;
        _caption=new Label{HorizontalAlignment=HorizontalAlignment.Center,MouseFilter=MouseFilterEnum.Ignore,TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis};_caption.AddThemeFontSizeOverride("font_size",18);_caption.AddThemeColorOverride("font_shadow_color",Colors.Black);_caption.AddThemeConstantOverride("shadow_offset_y",2);AddChild(_caption);
        _notice=new Label{HorizontalAlignment=HorizontalAlignment.Center,MouseFilter=MouseFilterEnum.Ignore};_notice.AddThemeFontSizeOverride("font_size",16);_notice.AddThemeColorOverride("font_color",new("ffb6a5"));_notice.AddThemeColorOverride("font_shadow_color",Colors.Black);_notice.AddThemeConstantOverride("shadow_outline_size",5);AddChild(_notice);
        _looks=GD.Load<PackedScene>(Ui+"RegionLookPicker.tscn").Instantiate<RegionLookPicker>();AddChild(_looks);_looks.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);_looks.Hide();
    }
    private void Bind(ScrollContainer rail,string node,Action action){var b=rail.GetNode<Button>("RailPanel/Tools/"+node);b.Disabled=false;b.Pressed+=action;}
    private Button HudButton(string icon,string hint,Action action,bool history=false)
    {
        var b=new Button{Theme=GD.Load<Theme>(Ui+"navigation/navigation_theme.tres"),ThemeTypeVariation="NavigationGlyph",ExpandIcon=true,IconAlignment=HorizontalAlignment.Center,CustomMinimumSize=new(48,48),TooltipText=hint,AccessibilityName=hint};
        if(icon.Length>0)b.Icon=GD.Load<Texture2D>(history?Ui+"icons/"+icon.ToLowerInvariant()+"-round.svg":"res://rookframe/ui/icons/"+icon+".svg");
        b.AddThemeConstantOverride("icon_max_width",26);b.Pressed+=action;return b;
    }
    private void Tool(string tool){Cancel();_tool=_tool==tool?"":tool;_wallPoint=null;Present();}
    private void Begin(){if(Pending)return;_before=Copy();_undo.Clear();_redo.Clear();}
    private List<Room> Copy()=>_rooms.Select(r=>r with{Ring=r.Ring.ToArray()}).ToList();
    private void Set(List<Room> state){_rooms.Clear();_rooms.AddRange(state.Select(r=>r with{Ring=r.Ring.ToArray()}));_selected=Math.Clamp(_selected,0,Math.Max(0,_rooms.Count-1));Rebuild();}
    private void Remember(){Begin();_undo.Push(Copy());_redo.Clear();}
    private void Cancel(){if(_before is{} before){_before=null;_newRoom=false;_looks?.Hide();Set(before);}_undo.Clear();_redo.Clear();_drag="";Present();}
    private void Commit()
    {
        if(!Pending||_invalid)return;
        _worldUndo.Push(_before!);_worldRedo.Clear();_before=null;_undo.Clear();_redo.Clear();_newRoom=false;_looks.Hide();Present();
    }
    private void History(bool redo)
    {
        var from=Pending?(redo?_redo:_undo):(redo?_worldRedo:_worldUndo);
        var to=Pending?(redo?_undo:_redo):(redo?_worldUndo:_worldRedo);
        if(from.Count==0)return;to.Push(Copy());Set(from.Pop());
    }
    private void Delete(){if(_rooms.Count==0)return;Remember();_rooms.RemoveAt(_selected);_selected=Math.Clamp(_selected,0,Math.Max(0,_rooms.Count-1));Rebuild();}
    private void Reset(string state)
    {
        _before=null;_undo.Clear();_redo.Clear();_worldUndo.Clear();_worldRedo.Clear();_tool="";_building=false;_newRoom=false;_selected=1;_anchor=-1;_wallPoint=null;_scenario=state;_looks.Hide();_rooms.Clear();
        _rooms.Add(new("Great hall",new(-3,0),Rectangle(6,6),Look:"Tavern"));
        _rooms.Add(new("East wing",new(2,0),Rectangle(4,4)));
        if(state is "lift" or "stacked")_rooms.Add(new("Upper chamber",new(2,0),Rectangle(4,4),3,2.8f,Stacked:true));
        if(state=="overlap"){Remember();_rooms[1]=Current with{At=new(.7f,0),Angle=.24f};}
        if(state=="lift"){Remember();_rooms[1]=_rooms[1] with{Floor=1.5f};_rooms[2]=_rooms[2] with{Floor=4.5f};}
        if(state=="stacked")_selected=2;
        if(state=="anchor"){Remember();_anchor=2;_rooms[1]=Current with{Ring=[new(-2,-2),new(2,-2),new(3,0),new(2,2),new(-2,2)]};}
        if(state=="drawing"){_tool="draw";Remember();_newRoom=true;_rooms.Add(new("New room",new(-.5f,3),Rectangle(4,3)));_selected=2;}
        if(state=="building"){_building=true;_rooms.Add(new("Workshop",new(-5,-6.5f),Rectangle(3,3)));}
        if(_longNames)for(int i=0;i<_rooms.Count;i++)_rooms[i]=_rooms[i] with{Name=_rooms[i].Name+" — Northern observatory / Северная галерея / 北の展望室"};
        Rebuild();
    }
    private void Rebuild()
    {
        foreach(var n in _architecture.GetChildren()){_architecture.RemoveChild(n);n.QueueFree();}
        foreach(var r in _rooms)BuildRoom(r);
        Present();
    }
    private void BuildRoom(Room r)
    {
        var anchors=r.Ring.Select((p,i)=>new ContourAnchor("a"+i,p)).ToArray();
        var contour=new MixedContour(anchors,anchors.Select((a,i)=>new ContourSpan("w"+i,a.Id,anchors[(i+1)%anchors.Length].Id)).ToArray());
        var look=r.Appearance??Look(r.Look);
        var plan=look.Apply(new RoomDraft(Transform3D.Identity,new(-2,-2,4,4),look.Wall!,look.Floor!,look.Ceiling!,.18,0,r.Height,Contour:contour,FoundationThickness:r.Base,Foundation:look.Foundation!));
        var shell=RoomShell.Build(plan,Content);
        var root=new Node3D{Position=V(r.At,r.Floor),Rotation=new(0,-r.Angle,0)};_architecture.AddChild(root);
        var structure=new MeshInstance3D{Mesh=shell.Mesh};var decor=new MeshInstance3D{Mesh=shell.Decoration};root.AddChild(structure);root.AddChild(decor);
        var ceiling=InteriorRevealMaterial.Create((BaseMaterial3D)shell.Mesh.SurfaceGetMaterial(1),groupOnly:true);ceiling.SetShaderParameter("group_visibility",_roofHidden?0f:1f);structure.SetSurfaceOverrideMaterial(1,ceiling);
        if(!_cutaway)return;
        void bind(MeshInstance3D mesh,int index){var m=InteriorRevealMaterial.Create((BaseMaterial3D)mesh.Mesh.SurfaceGetMaterial(index));m.SetShaderParameter("cutaway_ranges",shell.Cutaway.Texture);m.SetShaderParameter("cutaway_floor",0f);m.SetShaderParameter("cutaway_slope",RoomCutaway.Slope);m.SetShaderParameter("context_cutaway",1f);mesh.SetSurfaceOverrideMaterial(index,m);}
        bind(structure,2);for(int i=4;i<shell.Mesh.GetSurfaceCount();i++)bind(structure,i);for(int i=0;i<(decor.Mesh?.GetSurfaceCount()??0);i++)bind(decor,i);
        shell.Cutaway.Update(root.Basis,_camera.GlobalBasis.Z,null);root.AddChild(new MeshInstance3D{Mesh=shell.Cutaway.Caps()});
    }
    private bool Collision(Room a,Room b)=>Math.Min(a.Floor+a.Height,b.Floor+b.Height)-Math.Max(a.Floor,b.Floor)>.03f&&Geometry2D.IntersectPolygons(Polygon(a),Polygon(b)).Any(p=>Area(p)>.005f);
    private static float Area(Vector2[] p)=>Math.Abs(Enumerable.Range(0,p.Length).Sum(i=>p[i].Cross(p[(i+1)%p.Length])))/2;
    internal List<Vector2[]> Overlaps=[];
    private void Validate()
    {
        Overlaps.Clear();
        for(int i=0;i<_rooms.Count;i++)for(int j=i+1;j<_rooms.Count;j++)
        {
            if(!Collision(_rooms[i],_rooms[j]))continue;
            foreach(var p in Geometry2D.IntersectPolygons(Polygon(_rooms[i]),Polygon(_rooms[j])))
                if(Area(p)>.005f)Overlaps.Add(p.Select(v=>Project(V(v,Math.Max(_rooms[i].Floor,_rooms[j].Floor)+.08f))).ToArray());
        }
        _invalid=Overlaps.Count>0;
    }
    private string Storey(Room r)
    {
        if(r.Floor<=1)return "Ground floor";if(r.Floor>=2&&r.Floor<=4)return "Upper floor";return "New storey";
    }
    private static bool Connected(Room a,Room b)
    {
        var ap=a.Ring.Select(p=>p.Rotated(a.Angle)+a.At).ToArray();var bp=b.Ring.Select(p=>p.Rotated(b.Angle)+b.At).ToArray();
        bool stacked=Math.Abs(a.Floor+a.Height-b.Floor)<.25f||Math.Abs(b.Floor+b.Height-a.Floor)<.25f;
        if(stacked&&Geometry2D.IntersectPolygons(ap,bp).Any(p=>Area(p)>.05f))return true;
        if(Math.Min(a.Floor+a.Height,b.Floor+b.Height)-Math.Max(a.Floor,b.Floor)<=.02f)return false;
        for(int i=0;i<ap.Length;i++)for(int j=0;j<bp.Length;j++)
        {
            var av=ap[(i+1)%ap.Length]-ap[i];var bv=bp[(j+1)%bp.Length]-bp[j];
            if(Math.Abs(av.Normalized().Cross(bv.Normalized()))>.01f)continue;
            if(Math.Abs((bp[j]-ap[i]).Cross(av.Normalized()))>.05f)continue;
            var p=(bp[j]-ap[i]).Dot(av.Normalized());var q=(bp[(j+1)%bp.Length]-ap[i]).Dot(av.Normalized());
            if(Math.Min(av.Length(),Math.Max(p,q))-Math.Max(0,Math.Min(p,q))>.1f)return true;
        }
        return false;
    }
    private int[] BuildingMembers(List<Room> rooms,int seed=-1)
    {
        var found=new HashSet<int>{Math.Clamp(seed<0?_selected:seed,0,rooms.Count-1)};bool changed=true;
        while(changed){changed=false;for(int i=0;i<rooms.Count;i++)if(!found.Contains(i)&&found.Any(j=>Connected(rooms[i],rooms[j]))){found.Add(i);changed=true;}}
        return found.Order().ToArray();
    }
    private bool Attached(Room r)=>BuildingMembers(_rooms).Contains(0);
    private void Present()
    {
        if(_rootHud is null)return;
        Validate();_rail.Visible=_builder;
        _right.GetNode<Button>("RailPanel/Tools/BuilderEntry").SetPressedNoSignal(_builder);
        _rail.GetNode<Button>("RailPanel/Tools/PortalsEntry").SetPressedNoSignal(_tool=="draw");_rail.GetNode<Button>("RailPanel/Tools/PaletteEntry").SetPressedNoSignal(_tool=="paint");
        _rootHud.Visible=_builder&&_tool!="paint"&&!Pending&&!_looks.Visible;_rootHud.ShowContext(_tool=="draw",_rooms.Count>0,true,_roofHidden,_cutaway,false);
        _paint.Visible=_builder&&_tool=="paint";_editHud.Visible=_builder&&Pending&&!_looks.Visible;
        _undoButton.Disabled=_undo.Count==0;_redoButton.Disabled=_redo.Count==0;_delete.Disabled=_rooms.Count==0;_commit.Disabled=_invalid;
        _commit.TooltipText=_commit.AccessibilityName=_invalid?"Rooms overlap · move apart to commit":"Commit edit";
        _caption.Text=_rooms.Count==0?"Room deleted · Commit or Cancel":(_building?"Building":Current.Name)+"  ·  "+Storey(Current)+(Current.Stacked?"  ·  Stacked":"")+(Pending?"  ·  "+(_building||_invalid?"Preview":Attached(Current)?"Same building":"New building"):"");
        _caption.TooltipText=_caption.Text;_caption.Visible=_builder&&_tool=="";
        _notice.Text=_invalid?(_newRoom?"Rooms overlap. Draw in empty space.":"Rooms overlap. Move them apart."):"";
        _notice.Visible=_builder&&_notice.Text.Length>0&&!_looks.Visible;
        Arrange();
    }
    private void Arrange()
    {
        if(_camera is null||_right is null)return;
        var compact=Size.Y<540;var small=compact||Size.X<1200;
        foreach(var rail in new[]{_left,_right,_rail}){rail.Call("set_compact",small);var s=rail.GetNode<Control>("RailPanel").GetCombinedMinimumSize();rail.Size=new(s.X,Math.Min(s.Y,Size.Y));}
        float edge=_left.GetThemeConstant("inset"),top=_left.GetThemeConstant("top"),sep=_left.GetThemeConstant("separation");
        _left.Position=new(edge,top);_right.Position=new(Size.X-edge-_right.Size.X,top);_rail.Position=new(_right.Position.X-_rail.Size.X-sep,top);
        _camera.Size=compact?(_rooms.Any(r=>r.Stacked)?19:13.5f):18;_camera.Position=new(11,16,20);_camera.LookAt(new(-.2f,1,0));
        _rootHud.SetEditingHeight(compact?68:104);_paint.LayoutFor(compact?68:104);
        // The proposal uses the existing HUD surface and icon sizes at both breakpoints.
        foreach(var b in _editHud.FindChildren("*","Button",true,false).OfType<Button>())b.CustomMinimumSize=Vector2.One*(compact?44:64);
        var frame=(StyleBoxTexture)_rootHud.GetThemeStylebox("panel").Duplicate();_editHud.AddThemeStyleboxOverride("panel",frame);
        foreach(var h in new Control[]{_rootHud,_paint,_editHud}){h.Size=h.GetCombinedMinimumSize();h.Position=new((Size.X-h.Size.X)/2,Size.Y-h.Size.Y-(compact?10:36));}
        var active=Pending?(Control)_editHud:_tool=="paint"?_paint:_rootHud;
        _switcher.Visible=_builder&&!_looks.Visible;_switcher.Scale=Vector2.One*(compact?.65f:1);_switcher.Position=new(active.Position.X+active.Size.X+12,active.Position.Y);_switcher.Size=new(44,104);
        _caption.Position=new(100,compact?8:22);_caption.Size=new(Size.X-300,30);
        _notice.Position=new(90,active.Position.Y-28);_notice.Size=new(Size.X-180,26);
        _right.Visible=_rail.Visible=!(_looks.Visible&&compact);_rail.Visible&=_builder;
        Controls();Validate();_feedback.QueueRedraw();
    }
    private void Controls()
    {
        _hits.Clear();_overlay.Markers.Clear();_overlay.Outlines.Clear();foreach(var n in _handles.GetChildren()){_handles.RemoveChild(n);n.QueueFree();}
        bool active=_builder&&_tool==""&&_rooms.Count>0&&!_looks.Visible;
        _foundation.Visible=active;
        if(!active){_overlay.QueueRedraw();return;}
        foreach(var (room,i) in _rooms.Select((r,i)=>(r,i)))
        {
            if(i!=_selected&&Current.Stacked&&room.Floor<Current.Floor&&Geometry2D.IntersectPolygons(Polygon(room),Polygon(Current)).Any(p=>Area(p)>.05f))continue;
            var marker=Project(V(room.At,room.Floor+room.Height+.45f));_overlay.Markers.Add((marker,false,_selected==i&&!_building,false,room.Name));_hits.Add((marker,"select"+i));
        }
        var seen=new HashSet<int>();var groups=_invalid&&_before is not null?_before:_rooms;
        for(int i=0;i<groups.Count;i++)
        {
            if(seen.Contains(i)||i>=_rooms.Count)continue;var members=BuildingMembers(groups,i);seen.UnionWith(members);
            var points=members.Where(j=>j<_rooms.Count).Select(j=>Project(V(_rooms[j].At,_rooms[j].Floor+_rooms[j].Height+.45f))).ToArray();
            var marker=new Vector2(points.Average(p=>p.X),Math.Max(56,points.Min(p=>p.Y)-55));
            _overlay.Markers.Add((marker,true,_building&&members.Contains(_selected),false,""));_hits.Add((marker,"building"+i));
        }
        var r=Current;
        foreach(var room in _building?BuildingMembers(_rooms).Select(i=>_rooms[i]):new[]{r})_overlay.Outlines.Add((Polygon(room).Select(p=>Project(V(p,room.Floor+.03f))).ToArray(),true));
        var at=V(r.At,r.Floor+.12f);
        var source=new CavePlanVisual{HandleMaterial=new StandardMaterial3D{AlbedoColor=new("e3d2a3"),ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,NoDepthTest=true}};
        foreach(var axis in new[]{Vector3.Left,Vector3.Right,Vector3.Forward,Vector3.Back})
        {
            var arrow=source.MockArrow(axis,false);arrow.Position=at+axis*1.05f;OrientArrow(arrow,axis);_handles.AddChild(arrow);_hits.Add((Project(arrow.Position),"move"));
        }
        if(!r.Stacked){var lift=source.MockArrow(Vector3.Up,true);lift.Position=at+Vector3.Up*1.65f;OrientArrow(lift,Vector3.Up);_handles.AddChild(lift);_hits.Add((Project(lift.Position),"lift"));}
        source.Free();
        var rotationAt=V(r.At+new Vector2(r.Ring.Max(p=>p.X)+.9f,0).Rotated(r.Angle),r.Floor+.25f);
        var rotate=new Sprite3D{Texture=GD.Load<Texture2D>(Ui+"assets/cavern-rotate.svg"),Position=rotationAt,PixelSize=1.25f/Math.Max(1,Project(rotationAt+_camera.GlobalBasis.X).DistanceTo(Project(rotationAt))),Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,NoDepthTest=true};_handles.AddChild(rotate);_hits.Add((Project(rotationAt),"rotate"));
        var low=Project(V(Polygon(r)[1],r.Floor));var high=Project(V(Polygon(r)[1],r.Floor+r.Height));
        _foundation.Position=low+new Vector2(38,0);
        _foundation.GetNode<Polygon2D>("Grip").Position=Vector2.Zero;_foundation.GetNode<Polygon2D>("Grip").Visible=!r.Stacked;
        _foundation.GetNode<Polygon2D>("UpperGrip").Position=high-low;
        _foundation.GetNode<Polygon2D>("BottomGrip").Hide();
        _foundation.GetNode<FoundationGuide>("Guide").Hide();var guide=_foundation.GetNode<FoundationGuide>("UpperGuide");guide.Start=Vector2.Zero;guide.End=high-low;guide.Show();
        if(!r.Stacked)_hits.Add((_foundation.Position,"foundation"));_hits.Add((_foundation.Position+high-low,"height"));
        if(_wallPoint is{} wall){var point=V(wall,r.Floor+.2f);var dot=new MeshInstance3D{Position=point,Mesh=new SphereMesh{Radius=.1f,Height=.2f},MaterialOverride=new StandardMaterial3D{AlbedoColor=new("e7e7dd"),ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,NoDepthTest=true}};_handles.AddChild(dot);_hits.Add((Project(point),"insert"));}
        if(_anchor>=0&&_anchor<r.Ring.Length){var point=V(Polygon(r)[_anchor],r.Floor+.2f);var p=Project(point);_hits.Add((p,"anchor"));var anchor=GD.Load<PackedScene>(Ui+"RoomCornerHandle.tscn").Instantiate<Node3D>();_handles.AddChild(anchor);anchor.Position=point;anchor.GetNode<Node3D>("Selected").Show();}_feedback.AnchorPoint=null;
        _overlay.QueueRedraw();
    }
    private void OrientArrow(Node3D arrow,Vector3 direction)
    {
        var across=direction.Cross(_camera.GlobalPosition-arrow.Position).Normalized();if(across.IsZeroApprox())across=_camera.GlobalBasis.X;
        arrow.Basis=new Basis(across,direction,across.Cross(direction));
        var pixels=Project(arrow.Position+_camera.GlobalBasis.X).DistanceTo(Project(arrow.Position));
        foreach(var mesh in arrow.GetChildren().OfType<MeshInstance3D>())mesh.Scale=Vector3.One*(24/Math.Max(1,pixels));
    }
    private void InsertAnchor()
    {
        if(_wallPoint is not{} wall)return;Remember();var ring=Current.Ring.ToList();_anchor=_wallEdge+1;ring.Insert(_anchor,(wall-Current.At).Rotated(-Current.Angle));_rooms[_selected]=Current with{Ring=ring.ToArray()};_wallPoint=null;Rebuild();
    }
    private void HandleButton(InputEvent e,string kind){if(e is InputEventMouseButton{ButtonIndex:MouseButton.Left} m){if(m.Pressed)StartDrag(kind,GetGlobalMousePosition());else EndDrag();AcceptEvent();}}
    private void StartDrag(string kind,Vector2 at)
    {
        if(_rooms.Count==0)return;TooltipText="";Remember();_drag=kind;_startPixel=at;_startPlane=Plane(at,Current.Floor);_dragStart=Copy();_startAngle=(_startPlane-Current.At).Angle();
    }
    private void EndDrag()
    {
        if(_drag=="draw"&&!_invalid)OpenLooks();_drag="";TooltipText="";Present();
    }
    private void OpenLooks()
    {
        _looks.Open(_preferences,_entries,Content,l=>{_rooms[_selected]=Current with{Appearance=l};Rebuild();},l=>{if(_invalid)return;_rooms[_selected]=Current with{Appearance=l};var named=_entries.Select(v=>v.AsGodotDictionary()).FirstOrDefault(e=>LocalAppearancePalette.Text(e,"type")=="architectural_look"&&RegionLook.Load(LocalAppearancePalette.Reference(e),Content)==l);_rooms[_selected]=Current with{Look=LocalAppearancePalette.Text(named??new D(),"localId").Split('-')[0] switch{"castle"=>"Castle","dungeon"=>"Dungeon",_=>"Tavern"}};Rebuild();Commit();},Cancel);Arrange();
    }
    public override void _GuiInput(InputEvent e)
    {
        if(!_builder||_tool=="paint"||_looks.Visible)return;
        if(e is InputEventMouseMotion hover&&_drag.Length==0){var h=_hits.Where(h=>h.Point.DistanceTo(hover.Position)<24).OrderBy(h=>h.Point.DistanceTo(hover.Position)).FirstOrDefault();TooltipText=h.Kind switch{"move"=>"Drag to move","rotate"=>"Drag to rotate","lift"=>"Lift room and rooms stacked above","height"=>"Change room height","foundation"=>"Change foundation height","anchor"=>"Pull wall anchor to create a corner","insert"=>"Add wall anchor; drag it to create a corner",_=>""};}
        if(e is InputEventMouseButton{ButtonIndex:MouseButton.Left} m)
        {
            if(!m.Pressed){EndDrag();return;}
            if(_tool=="draw")
            {
                if(_newRoom){Cancel();_tool="draw";}Remember();_newRoom=true;var point=Plane(m.Position);_rooms.Add(new("New room",point,Rectangle(.2f,.2f),Shape:_rootHud.Shape));_selected=_rooms.Count-1;_drag="draw";_startPixel=m.Position;_startPlane=point;Rebuild();return;
            }
            var hits=_hits.Where(h=>h.Point.DistanceTo(m.Position)<23).OrderBy(h=>h.Point.DistanceTo(m.Position)).ToArray();
            if(hits.Length>0){var kind=hits[0].Kind;if(kind.StartsWith("select")){_selected=int.Parse(kind[6..]);_building=false;_anchor=-1;Present();}else if(kind.StartsWith("building")){_selected=int.Parse(kind[8..]);_building=true;Present();}else if(kind=="insert"){InsertAnchor();StartDrag("anchor",m.Position);}else StartDrag(kind,m.Position);return;}
            if(_rooms.Count>0)
            {
                var r=Current;var poly=Polygon(r);var point=Plane(m.Position,r.Floor);
                var closest=Enumerable.Range(0,poly.Length).Select(i=>(I:i,P:Geometry2D.GetClosestPointToSegment(point,poly[i],poly[(i+1)%poly.Length]))).MinBy(p=>p.P.DistanceTo(point));
                if(closest.P.DistanceTo(point)<.55f){_wallPoint=closest.P;_wallEdge=closest.I;StartDrag("wall",m.Position);Present();return;}
            }
        }
    }
    public override void _Input(InputEvent e)
    {
        if(e is InputEventKey{Pressed:true,Echo:false} k)
        {
            if(k.Keycode is >=Key.Key1 and <=Key.Key7){Reset(new[]{"room","overlap","lift","stacked","anchor","drawing","building"}[(int)(k.Keycode-Key.Key1)]);GetViewport().SetInputAsHandled();}
            if(k.Keycode==Key.Escape){Cancel();GetViewport().SetInputAsHandled();}
            if(k.Keycode==Key.Enter&&!_looks.Visible){if(_newRoom){if(!_invalid)OpenLooks();}else Commit();GetViewport().SetInputAsHandled();}
            if(k.Keycode==Key.Delete||k.Keycode==Key.Backspace)Delete();
            if(k.Keycode==Key.Z&&(k.CtrlPressed||k.MetaPressed))History(k.ShiftPressed);
            if(k.Keycode==Key.F9)Capture("manual");
        }
        if(e is InputEventMouseButton{Pressed:false,ButtonIndex:MouseButton.Left}&&_drag.Length>0){EndDrag();return;}
        if(e is not InputEventMouseMotion motion||_drag.Length==0)return;
        var now=Plane(motion.Position,_rooms.Count>0?Current.Floor:0);
        if(_drag=="draw")
        {
            var d=now-_startPlane;_rooms[_selected]=Current with{At=(now+_startPlane)/2,Ring=DrawRing(_rootHud.Shape,Math.Max(.2f,Math.Abs(d.X)),Math.Max(.2f,Math.Abs(d.Y)))};
        }
        else if(_dragStart is{} start)
        {
            var original=start[_selected];
            var targets=_building?BuildingMembers(start):new[]{_selected};
            if(_drag=="lift"&&!original.Stacked)targets=Enumerable.Range(0,start.Count).Where(i=>i==_selected||start[i].Stacked&&start[i].At.DistanceTo(original.At)<.1f).ToArray();
            foreach(var i in targets)
            {
                var r=start[i];
                switch(_drag)
                {
                    case "move":var delta=Plane(motion.Position,original.Floor)-_startPlane;var at=r.At+delta;if(Math.Abs(at.X-2)<.15f)at.X=2;if(Math.Abs(at.Y)<.15f)at.Y=0;_rooms[i]=r with{At=at};break;
                    case "rotate":var a=(now-original.At).Angle()-_startAngle;_rooms[i]=r with{Angle=r.Angle+a,At=_building?original.At+(r.At-original.At).Rotated(a):r.At};break;
                    case "lift":_rooms[i]=r with{Floor=Math.Max(0,r.Floor+(_startPixel.Y-motion.Position.Y)/35)};break;
                    case "height":_rooms[i]=r with{Height=Math.Max(.5f,r.Height+(_startPixel.Y-motion.Position.Y)/35)};break;
                    case "foundation":var df=Math.Max(-r.Base+.1f,(_startPixel.Y-motion.Position.Y)/35);_rooms[i]=r with{Floor=r.Floor+df,Base=r.Base+df};break;
                    case "wall":var ring=r.Ring.ToArray();var shift=(now-_startPlane).Rotated(-r.Angle);ring[_wallEdge]+=shift;ring[(_wallEdge+1)%ring.Length]+=shift;_rooms[i]=r with{Ring=ring};break;
                    case "anchor":if(_anchor>=0){var points=r.Ring.ToArray();points[_anchor]=(now-r.At).Rotated(-r.Angle);_rooms[i]=r with{Ring=points};}break;
                }
            }
        }
        Rebuild();
    }
    private async void CaptureLater(string[] args){await ToSignal(GetTree().CreateTimer(3),Godot.Timer.SignalName.Timeout);Arrange();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);var i=Array.IndexOf(args,"--capture");Capture(args[i+1]);if(args.Contains("--quit-after-capture"))GetTree().Quit();}
    private void Capture(string name)
    {
        var path=OS.GetEnvironment("REGION_EVIDENCE");if(path.Length==0)path="/tmp/rfg401-region-edit";System.IO.Directory.CreateDirectory(path);GetViewport().GetTexture().GetImage().SavePng(path+"/"+name+".png");
        System.IO.File.WriteAllText(path+"/"+name+".json",System.Text.Json.JsonSerializer.Serialize(new{scenario=_scenario,size=Size.ToString(),pending=Pending,invalid=_invalid,commitEnabled=!_commit.Disabled,tool=_tool,rooms=_rooms.Select(r=>new{r.Name,r.Floor,r.Height,r.Angle,at=r.At.ToString(),corners=r.Ring.Length}),hits=_hits.Select(h=>new{kind=h.Kind,at=h.Point.ToString()})}));GD.Print("MOCK_CAPTURE "+path+"/"+name+".png");
    }
}
public partial class MockOverlay:Control
{
    internal RegionEditMock OwnerMock=null!;
    internal Vector2? AnchorPoint;
    public override void _Draw()
    {
        foreach(var poly in OwnerMock.Overlaps){DrawColoredPolygon(poly,new(1,.16f,.1f,.55f));DrawPolyline([..poly,poly[0]],new(1,.3f,.2f),2,true);}
        if(AnchorPoint is{} p){DrawCircle(p,8,new("e8d7a8"));DrawArc(p,11,0,Mathf.Tau,32,new("1c282c"),2,true);}
    }
}
