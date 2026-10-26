using Godot;
namespace Rookframe.Manager.App;

// Focused experiment checks, deliberately outside the production test suite.
public partial class RegionEditMock
{
    private async void CheckSnap()
    {
        if(DisplayServer.GetName()=="headless"){GD.PrintErr("Pixel-based snapping checks need a native window; omit --headless.");GetTree().Quit(2);return;}
        await ToSignal(GetTree().CreateTimer(.5),Godot.Timer.SignalName.Timeout);
        var cases=new List<object>();var failures=0;
        void check(string name,bool pass){cases.Add(new{name,pass});if(!pass)failures++;}
        float units(Vector2 direction,float pixels)=>pixels/Project(V(direction,0)).DistanceTo(Project(Vector3.Zero));
        var baseline=Copy();var four=units(Vector2.Right,4);var ten=units(Vector2.Right,10);var twenty=units(Vector2.Right,20);
        _snap=null;var snap=SnapMove(baseline,[1],new(four,0));check("close wall joins without overlap",snap.Length()<.001f&&_snap is not null&&!Collision(baseline[1] with{At=baseline[1].At+snap},baseline[0]));
        snap=SnapMove(baseline,[1],new(four,.5f));check("slide freely along wall",snap.DistanceTo(new(0,.5f))<.001f);
        snap=SnapMove(baseline,[1],new(ten,0));check("small pointer drift keeps contact",snap.Length()<.001f&&_snap is not null);
        snap=SnapMove(baseline,[1],new(twenty,0));check("pull away releases",snap.DistanceTo(new(twenty,0))<.001f&&_snap is null);
        snap=SnapMove(baseline,[1],new(ten,0));check("outside capture range stays free",snap.DistanceTo(new(ten,0))<.001f&&_snap is null);
        snap=SnapMove(baseline,[1],new(-four,0));check("small accidental overlap resolves to contact",snap.Length()<.001f&&_snap is not null);
        _snap=null;var angle=.43f;var rotated=baseline.Select(r=>r with{At=r.At.Rotated(angle),Angle=angle}).ToList();var direction=Vector2.Right.Rotated(angle);snap=SnapMove(rotated,[1],direction*units(direction,4));check("rotated parallel walls join",snap.Length()<.001f&&_snap is not null);
        _snap=null;var separate=baseline.Select(r=>r).ToList();separate[1]=separate[1] with{Floor=6};snap=SnapMove(separate,[1],new(four,0));check("different vertical levels do not attract",_snap is null);
        _snap=null;separate=baseline.Select(r=>r).ToList();separate[1]=separate[1] with{At=new(2,5)};SnapMove(separate,[1],new(four,0));check("corner-only contact does not join",_snap is null);
        _snap=null;var blocked=baseline.Select(r=>r).ToList();blocked.Add(new("Blocker",new(4.2f,0),Rectangle(1,1)));SnapMove(blocked,[1],new(four,0));check("reject contact that overlaps another room",_snap is null);
        _snap=null;var group=baseline.Select(r=>r).ToList();group.Add(new("Second moving room",new(2,4),Rectangle(4,4)));snap=SnapMove(group,[1,2],new(four,.2f));check("one offset keeps whole Building together",snap.DistanceTo(new(0,.2f))<.001f&&_snap is not null);
        var cornerFour=units(Vector2.Down,4);var cornerTen=units(Vector2.Down,10);var cornerTwenty=units(Vector2.Down,20);
        _snap=null;snap=SnapMove(baseline,[1],new(four,1-cornerFour));check("sliding aligns the front corners",snap.DistanceTo(new(0,1))<.001f&&_snap?.Corner is not null);
        snap=SnapMove(baseline,[1],new(four,1+cornerTen));check("small drift keeps corner alignment",snap.DistanceTo(new(0,1))<.001f&&_snap?.Corner is not null);
        snap=SnapMove(baseline,[1],new(four,1+cornerTwenty));check("pull past corner resumes wall sliding",snap.DistanceTo(new(0,1+cornerTwenty))<.001f&&_snap is{Corner:null});
        _snap=null;snap=SnapMove(baseline,[1],new(four,1+cornerTen));check("corner outside capture range stays free",snap.DistanceTo(new(0,1+cornerTen))<.001f&&_snap is{Corner:null});
        _snap=null;snap=SnapMove(baseline,[1],new(four,-1+cornerFour));check("sliding aligns the back corners",snap.DistanceTo(new(0,-1))<.001f&&_snap?.Corner is not null);
        _snap=null;var along=Vector2.Down.Rotated(angle);snap=SnapMove(rotated,[1],direction*units(direction,4)+along*(1-units(along,4)));check("rotated walls align corners",snap.DistanceTo(along)<.001f&&_snap?.Corner is not null);
        _snap=null;var obstructed=baseline.Select(r=>r).ToList();obstructed.Add(new("Corner blocker",new(2,3.5f-cornerFour/2),Rectangle(1,1)));SnapMove(obstructed,[1],new(four,.3f));snap=SnapMove(obstructed,[1],new(four,1-cornerFour));check("blocked corner keeps safe wall contact",snap.DistanceTo(new(0,1-cornerFour))<.001f&&_snap is{Corner:null}&&!Collision(obstructed[1] with{At=obstructed[1].At+snap},obstructed[2]));
        _snap=null;snap=SnapMove(group,[1,2],new(four,-1+cornerFour));check("corner alignment moves a Building together",snap.DistanceTo(new(0,-1))<.001f&&_snap?.Corner is not null);
        bool allAngles=true;
        for(int step=-8;step<=8;step++){_rotationSnap=null;var wanted=step*Mathf.Pi/4;allAngles&=Mathf.Abs(SnapRotation(wanted+Mathf.DegToRad(3))-wanted)<.0001f;}
        check("rotation catches every 45 degrees in both directions",allAngles);
        _rotationSnap=null;SnapRotation(Mathf.DegToRad(43));check("rotation holds through small drift",Mathf.Abs(SnapRotation(Mathf.DegToRad(51))-Mathf.Pi/4)<.0001f);
        check("pull past angle resumes free rotation",Mathf.Abs(SnapRotation(Mathf.DegToRad(54))-Mathf.DegToRad(54))<.0001f&&_rotationSnap is null);
        _rotationSnap=null;check("rotation between steps remains free",Mathf.Abs(SnapRotation(Mathf.DegToRad(23))-Mathf.DegToRad(23))<.0001f&&_rotationSnap is null);
        _rotationSnap=null;SnapRotation(Mathf.DegToRad(179));check("rotation snap crosses the angle wrap",Mathf.Abs(Mathf.AngleDifference(SnapRotation(Mathf.DegToRad(-179)),Mathf.Pi))<.0001f);
        var json=System.Text.Json.JsonSerializer.Serialize(new{size=Size.ToString(),failures,cases});GD.Print("REGION_SNAP_CHECK "+json);
        var path=OS.GetEnvironment("REGION_EVIDENCE");if(path.Length==0)path="/tmp/rfg401-region-edit";System.IO.Directory.CreateDirectory(path);System.IO.File.WriteAllText(path+"/snap-check.json",json);
        GetTree().Quit(failures==0?0:1);
    }
}
